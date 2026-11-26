export interface CvrCckInfo {
  cckVersion: string;
  cckDownloadUrl: string;
  unityVersion: string;
  unityDownloadUrl: string;
  unityHubInstallUrl: string;
  legacyCckVersion: string;
  legacyUnityVersion: string;
  clientUnityVersion: string;
  contentLimits: {
    maxAvatarBundleBytes: number;
    maxSpawnableBundleBytes: number;
    maxWorldBundleBytes: number;
  };
  infoLinks: {
    discord: string;
    support: string;
    contentManageHub: string;
    cckLanding: string;
  };
  source: 'live' | 'fallback';
  fetchedAt: string;
}

export const CVR_CCK_INFO_API_URL = 'https://api.chilloutvr.net/1/public/cck/info';
const CACHE_TTL_MS = 15 * 60 * 1000;
const FETCH_TIMEOUT_MS = 4000;

export const FALLBACK_CVR_CCK_INFO: Omit<CvrCckInfo, 'source' | 'fetchedAt'> = {
  cckVersion: '4.0.1',
  cckDownloadUrl: 'https://files.chilloutvr.net/cck/CCK_4.0.1_Release.unitypackage',
  unityVersion: '2022.3.58f1',
  unityDownloadUrl: 'https://unity.com/releases/editor/whats-new/2022.3.58f1#installs',
  unityHubInstallUrl: 'unityhub://2022.3.58f1/ed7f6eacb62e',
  legacyCckVersion: '3.16.4',
  legacyUnityVersion: '2021.3.45f2',
  clientUnityVersion: '6.3.26f1',
  contentLimits: {
    maxAvatarBundleBytes: 209715200,
    maxSpawnableBundleBytes: 209715200,
    maxWorldBundleBytes: 1073741824,
  },
  infoLinks: {
    discord: 'https://discord.gg/ChilloutVR',
    support: 'https://discord.gg/ChilloutVR',
    contentManageHub: 'https://hub.chilloutvr.net',
    cckLanding: 'https://docs.chilloutvr.net/cck/setup/',
  },
};

interface CvrCckApiPayload {
  data?: {
    infoLinks?: Partial<CvrCckInfo['infoLinks']>;
    cckInfo?: {
      cckVersion?: string;
      cckDownloadUrl?: string;
      unityVersion?: string;
      unityDownloadUrl?: string;
      unityHubInstallUrl?: string;
    };
    contentLimits?: Partial<CvrCckInfo['contentLimits']>;
  };
}

let cachedInfo: { value: CvrCckInfo; expiresAt: number } | null = null;

export function resetCvrCckInfoCache(): void {
  cachedInfo = null;
}

export async function fetchCvrCckInfo(options?: {
  forceRefresh?: boolean;
  fetchImpl?: typeof fetch;
}): Promise<CvrCckInfo> {
  const now = Date.now();
  if (!options?.forceRefresh && cachedInfo && now < cachedInfo.expiresAt) {
    return cachedInfo.value;
  }

  const fetchFn = options?.fetchImpl ?? globalThis.fetch;
  if (typeof fetchFn === 'function') {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), FETCH_TIMEOUT_MS);
    try {
      const res = await fetchFn(CVR_CCK_INFO_API_URL, {
        headers: { Accept: 'application/json' },
        signal: controller.signal,
      });
      if (res.ok) {
        const json = (await res.json()) as CvrCckApiPayload;
        const apiCck = json?.data?.cckInfo;
        const apiLinks = json?.data?.infoLinks;
        const apiLimits = json?.data?.contentLimits;

        if (apiCck?.cckVersion && apiCck?.unityVersion) {
          const liveInfo: CvrCckInfo = {
            cckVersion: apiCck.cckVersion,
            cckDownloadUrl: apiCck.cckDownloadUrl || FALLBACK_CVR_CCK_INFO.cckDownloadUrl,
            unityVersion: apiCck.unityVersion,
            unityDownloadUrl: apiCck.unityDownloadUrl || FALLBACK_CVR_CCK_INFO.unityDownloadUrl,
            unityHubInstallUrl: apiCck.unityHubInstallUrl || FALLBACK_CVR_CCK_INFO.unityHubInstallUrl,
            legacyCckVersion: FALLBACK_CVR_CCK_INFO.legacyCckVersion,
            legacyUnityVersion: FALLBACK_CVR_CCK_INFO.legacyUnityVersion,
            clientUnityVersion: FALLBACK_CVR_CCK_INFO.clientUnityVersion,
            contentLimits: {
              maxAvatarBundleBytes:
                apiLimits?.maxAvatarBundleBytes ?? FALLBACK_CVR_CCK_INFO.contentLimits.maxAvatarBundleBytes,
              maxSpawnableBundleBytes:
                apiLimits?.maxSpawnableBundleBytes ?? FALLBACK_CVR_CCK_INFO.contentLimits.maxSpawnableBundleBytes,
              maxWorldBundleBytes:
                apiLimits?.maxWorldBundleBytes ?? FALLBACK_CVR_CCK_INFO.contentLimits.maxWorldBundleBytes,
            },
            infoLinks: {
              discord: apiLinks?.discord || FALLBACK_CVR_CCK_INFO.infoLinks.discord,
              support: apiLinks?.support || FALLBACK_CVR_CCK_INFO.infoLinks.support,
              contentManageHub: apiLinks?.contentManageHub || FALLBACK_CVR_CCK_INFO.infoLinks.contentManageHub,
              cckLanding: apiLinks?.cckLanding || FALLBACK_CVR_CCK_INFO.infoLinks.cckLanding,
            },
            source: 'live',
            fetchedAt: new Date(now).toISOString(),
          };
          cachedInfo = { value: liveInfo, expiresAt: now + CACHE_TTL_MS };
          return liveInfo;
        }
      }
    } catch {
      // Offline or transient error -> fall back cleanly below
    } finally {
      clearTimeout(timer);
    }
  }

  return {
    ...FALLBACK_CVR_CCK_INFO,
    contentLimits: { ...FALLBACK_CVR_CCK_INFO.contentLimits },
    infoLinks: { ...FALLBACK_CVR_CCK_INFO.infoLinks },
    source: 'fallback',
    fetchedAt: new Date(now).toISOString(),
  };
}

export function bytesToMb(bytes: number): number {
  return Math.round(bytes / (1024 * 1024));
}

export function formatCckSetupGuide(info: CvrCckInfo): string {
  const maxAvatarMb = bytesToMb(info.contentLimits.maxAvatarBundleBytes);
  const maxSpawnableMb = bytesToMb(info.contentLimits.maxSpawnableBundleBytes);
  const maxWorldMb = bytesToMb(info.contentLimits.maxWorldBundleBytes);
  const sourceLabel =
    info.source === 'live'
      ? `live from ${CVR_CCK_INFO_API_URL}`
      : 'cached defaults (offline fallback)';

  return [
    '### ChilloutVR Unity & CCK Setup Guide (Official docs.chilloutvr.net/cck/setup/)',
    `*(Version data: ${sourceLabel})*`,
    '',
    '1. **Which CCK Version Should I Use?**',
    `   - **CCK 4 (Release — Recommended):** \`v${info.cckVersion}\` using **Unity \`${info.unityVersion}\`** (**3D Built-In Render Pipeline** — URP/HDRP are not supported). Modern build panel, content browser, account switcher, reworked build pipeline (no Play Mode required to upload).`,
    `   - **CCK 3 (Legacy):** \`v${info.legacyCckVersion}\` using **Unity \`${info.legacyUnityVersion}\`**. Development discontinued; use CCK 4 for active projects.`,
    '   - **Never mix CCK 3 and CCK 4** in the same project — they conflict. CCK 4 includes all CCK 3 functionality.',
    `   - **Client vs. CCK Version Note:** The ChilloutVR game client (\`2026.3.0+\`) runs on **Unity \`${info.clientUnityVersion}\`** (still Built-In RP), while CCK content creation uses **Unity \`${info.unityVersion}\`** until an updated CCK is published via \`${CVR_CCK_INFO_API_URL}\`.`,
    '',
    '2. **Downloads & Unity Hub Security Notice**',
    `   - **CCK 4 Package:** ${info.cckDownloadUrl}`,
    `   - **Unity Hub Direct Install:** \`${info.unityHubInstallUrl}\``,
    `   - **Unity Editor Download:** ${info.unityDownloadUrl}`,
    `   - **Unity Security Advisory Note:** Unity Hub may flag \`${info.unityVersion}\` under a Unity Security Update Advisory. Per ChilloutVR docs, this only affects player builds and **not** the Unity Editor, so it is safe for creating ChilloutVR content.`,
    '',
    '3. **Clean Import Required on Every CCK 4 Update**',
    '   - **Back up your project** before updating.',
    '   - Delete all existing CCK folders from `Assets/` before importing the new package:',
    '     - `Assets/ABI.CCK`',
    '     - `Assets/ABI.MODS`',
    '     - `Assets/ABI.QA`',
    '     - `Assets/CVR.CCK`',
    '   - Import the downloaded CCK 4 `.unitypackage` (`Assets > Import Package > Custom Package...`).',
    '',
    '4. **Signing In to the CCK**',
    '   - Sign in with your ChilloutVR **username** and **Content Creation Kit Master Key** (*not* your account password).',
    `   - Retrieve your Master Key on the Community Hub (${info.infoLinks.contentManageHub}) under **Profile Settings > Access Key Management**.`,
    '',
    '5. **Publishing Content & Official Bundle Limits**',
    '   - After uploading, publish from the Community Hub content details page (**Submit for Publication**), reachable via:',
    '     - Community Hub > **My Content** > **Manage**',
    '     - CCK Builder Tab > top-right `▼` utility menu > **Open in Browser**',
    '     - CCK Browser Tab (CCK 4.x+) > **View on Hub**',
    '     - In-game content details page > **Open in Hub**',
    `   - **Official Bundle Size Limits:** Avatars <= **${maxAvatarMb} MB**, Spawnables <= **${maxSpawnableMb} MB**, Worlds <= **${maxWorldMb} MB**.`,
    `   - Official Setup Docs: ${info.infoLinks.cckLanding} | Discord: ${info.infoLinks.discord}`,
  ].join('\n');
}
