import { jest, describe, it, expect, beforeEach, afterEach } from '@jest/globals';
import {
  fetchCvrCckInfo,
  formatCckSetupGuide,
  resetCvrCckInfoCache,
  FALLBACK_CVR_CCK_INFO,
} from '../utils/cvrCckInfo.js';
import { registerHowtoCckTool } from '../tools/cvrHowToTools.js';
import { registerInspectCvrCckTool } from '../tools/cvrCckAuditTools.js';

const mockSendRequest = jest.fn<any>();
const mockMcpUnity = {
  sendRequest: mockSendRequest,
};

const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn(),
};

const mockServerTool = jest.fn();
const mockServer = {
  tool: mockServerTool,
};

describe('ChilloutVR CCK Setup & Audit Tools', () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    jest.clearAllMocks();
    resetCvrCckInfoCache();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  describe('cvrCckInfo live lookup & fallback', () => {
    it('fetches and caches live CCK info from api.chilloutvr.net', async () => {
      const mockFetch = jest.fn<typeof fetch>().mockResolvedValue({
        ok: true,
        json: async () => ({
          data: {
            infoLinks: {
              discord: 'https://discord.gg/ChilloutVR',
              support: 'https://discord.gg/ChilloutVR',
              contentManageHub: 'https://hub.chilloutvr.net',
              cckLanding: 'https://docs.chilloutvr.net/cck/setup/',
            },
            cckInfo: {
              cckVersion: '4.1.0',
              cckDownloadUrl: 'https://files.chilloutvr.net/cck/CCK_4.1.0_Release.unitypackage',
              unityVersion: '6000.3.26f1',
              unityDownloadUrl: 'https://unity.com/releases/editor/whats-new/6000.3.26f1#installs',
              unityHubInstallUrl: 'unityhub://6000.3.26f1/test',
            },
            contentLimits: {
              maxAvatarBundleBytes: 209715200,
              maxSpawnableBundleBytes: 209715200,
              maxWorldBundleBytes: 1073741824,
            },
          },
        }),
      } as unknown as Response);

      const first = await fetchCvrCckInfo({ fetchImpl: mockFetch });
      const second = await fetchCvrCckInfo({ fetchImpl: mockFetch });

      expect(first.source).toBe('live');
      expect(first.cckVersion).toBe('4.1.0');
      expect(first.unityVersion).toBe('6000.3.26f1');
      expect(second).toBe(first);
      expect(mockFetch).toHaveBeenCalledTimes(1);
    });

    it('falls back cleanly to cached defaults when offline', async () => {
      const failingFetch = jest.fn<typeof fetch>().mockRejectedValue(new Error('offline'));

      const info = await fetchCvrCckInfo({ fetchImpl: failingFetch });

      expect(info.source).toBe('fallback');
      expect(info.cckVersion).toBe(FALLBACK_CVR_CCK_INFO.cckVersion);
      expect(info.unityVersion).toBe(FALLBACK_CVR_CCK_INFO.unityVersion);

      const guide = formatCckSetupGuide(info);
      expect(guide).toContain('docs.chilloutvr.net/cck/setup/');
      expect(guide).toContain('Assets/ABI.CCK');
      expect(guide).toContain('Assets/CVR.CCK');
      expect(guide).toContain('Master Key');
      expect(guide).toContain('200 MB');
      expect(guide).toContain('1024 MB');
    });
  });

  describe('howto_cck setup topic', () => {
    it('forwards live CCK info to Unity for topic="setup"', async () => {
      globalThis.fetch = jest.fn<typeof fetch>().mockRejectedValue(new Error('offline'));

      registerHowtoCckTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;

      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        topic: 'setup',
        guide: 'Setup guide from Unity',
        message: 'Setup guide from Unity',
        cckInfo: { cckVersion: '4.0.1', unityVersion: '2022.3.58f1' },
      });

      const result = await handler({ topic: 'setup', scaffold: false });

      expect(mockSendRequest).toHaveBeenCalledWith(
        {
          method: 'howto_cck',
          params: expect.objectContaining({
            topic: 'setup',
            cckVersion: '4.0.1',
            unityVersion: '2022.3.58f1',
            maxAvatarBundleMb: 200,
            maxWorldBundleMb: 1024,
          }),
        },
        expect.any(Object)
      );
      expect(result.structuredContent.topic).toBe('setup');
    });

    it('serves official setup guide directly from Node if Unity is disconnected', async () => {
      globalThis.fetch = jest.fn<typeof fetch>().mockRejectedValue(new Error('offline'));

      registerHowtoCckTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;

      mockSendRequest.mockRejectedValue(new Error('Not connected to Unity'));

      const result = await handler({ topic: 'setup', scaffold: false });

      expect(result.content[0].text).toContain('ChilloutVR Unity & CCK Setup Guide');
      expect(result.content[0].text).toContain('2022.3.58f1');
      expect(result.structuredContent.servedWithoutUnity).toBe(true);
    });
  });

  describe('inspect_cvr_cck check_setup action', () => {
    it('forwards live expected CCK and Unity version and returns environment audit', async () => {
      globalThis.fetch = jest.fn<typeof fetch>().mockRejectedValue(new Error('offline'));

      registerInspectCvrCckTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;

      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        status: 'pass',
        contentType: 'setup',
        message: 'CCK Setup Audit: PASS',
        errors: [],
        warnings: [],
        suggestions: [],
        environment: {
          unityVersion: '2022.3.58f1',
          expectedUnityVersion: '2022.3.58f1',
          isBuiltInRenderPipeline: true,
          cckInstalled: true,
          cckTrack: 'cck4',
        },
      });

      const result = await handler({ action: 'check_setup', contentType: 'world' });

      expect(mockSendRequest).toHaveBeenCalledWith(
        {
          method: 'inspect_cvr_cck',
          params: expect.objectContaining({
            action: 'check_setup',
            expectedUnityVersion: '2022.3.58f1',
            expectedCckVersion: '4.0.1',
          }),
        },
        expect.any(Object)
      );
      expect(result.structuredContent.environment).toMatchObject({
        unityVersion: '2022.3.58f1',
        cckTrack: 'cck4',
      });
    });
  });
});
