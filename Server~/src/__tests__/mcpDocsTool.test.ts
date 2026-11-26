import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import {
  discoverBundledDocs,
  handleGetMcpDocs,
  htmlToReadableText,
  registerGetMcpDocsTool,
} from '../tools/mcpDocsTool.js';

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

describe('get_mcp_docs tool', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('registers get_mcp_docs with the MCP server', () => {
    registerGetMcpDocsTool(mockServer as any, mockLogger as any);

    expect(mockServerTool).toHaveBeenCalledWith(
      'get_mcp_docs',
      expect.any(String),
      expect.objectContaining({
        action: expect.any(Object),
        doc: expect.any(Object),
        query: expect.any(Object),
      }),
      expect.any(Function)
    );
  });

  it('lists all bundled markdown and HTML documentation files', () => {
    const entries = discoverBundledDocs();
    const paths = entries.map((e) => e.path);

    expect(paths).toContain('INSTALL.md');
    expect(paths).toContain('AGENTS.md');
    expect(paths).toContain('README.md');
    expect(paths).toContain('docs/chilloutvr-cck-integration.md');
    expect(paths).toContain('docs/mcp-unity-sketchfab-howto.html');

    const result = handleGetMcpDocs({ action: 'list' });
    const structured = result.structuredContent as any;
    expect(structured.action).toBe('list');
    expect(structured.count).toBeGreaterThanOrEqual(5);
  });

  it('reads markdown and HTML docs cleanly with pagination', () => {
    const mdRes = handleGetMcpDocs({
      action: 'read',
      doc: 'chilloutvr-cck-integration.md',
      maxChars: 1200,
    });
    const mdStructured = mdRes.structuredContent as any;
    expect(mdStructured.doc).toBe('docs/chilloutvr-cck-integration.md');
    expect(mdStructured.returnedChars).toBeLessThanOrEqual(1200);
    expect(mdStructured.hasMore).toBe(true);

    const htmlRes = handleGetMcpDocs({
      action: 'read',
      doc: 'docs/mcp-unity-sketchfab-howto.html',
      maxChars: 2000,
    });
    const htmlText = (htmlRes.content[0] as any).text as string;
    expect(htmlText).not.toContain('<script');
    expect(htmlText).not.toContain('<style');
  });

  it('rejects path traversal and unknown doc names', () => {
    expect(() =>
      handleGetMcpDocs({ action: 'read', doc: '../../package.json' })
    ).toThrow(/not found/i);
  });

  it('searches across bundled docs for CCK and workflow topics', () => {
    const res = handleGetMcpDocs({ action: 'search', query: 'PlayerLoop' });
    const structured = res.structuredContent as any;

    expect(structured.action).toBe('search');
    expect(structured.count).toBeGreaterThan(0);
    expect(structured.matches.some((m: any) => m.doc === 'docs/chilloutvr-cck-integration.md')).toBe(true);
  });

  it('strips scripts, styles, and tags cleanly in htmlToReadableText', () => {
    const out = htmlToReadableText(
      '<html><head><style>body{color:red}</style><script>alert(1)</script></head><body><h1>Hello &amp; Welcome</h1><p>Line 1<br>Line 2</p></body></html>'
    );
    expect(out).toContain('Hello & Welcome');
    expect(out).toContain('Line 1');
    expect(out).toContain('Line 2');
    expect(out).not.toContain('alert');
    expect(out).not.toContain('color:red');
  });
});
