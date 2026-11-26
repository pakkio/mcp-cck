import * as z from 'zod';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { getToolTimeout } from '../utils/timeouts.js';
import { bytesToMb, fetchCvrCckInfo, formatCckSetupGuide } from '../utils/cvrCckInfo.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// ============================================================================
// howto_cck
// ============================================================================

const toolName = 'howto_cck';
const toolDescription = 'Provides expert ChilloutVR CCK recipes, official setup instructions (docs.chilloutvr.net/cck/setup/ with live CCK version info), local testing/build troubleshooting, Sketchfab/Blender FBX workflow best practices, and optional GameObject scaffolding. Supports wildcard searches ("*", "veh*", "door*", "list").';
const paramsSchema = z.object({
  topic: z.string().default('*').describe('ChilloutVR CCK recipe topic or search pattern (e.g. "*", "list", "setup", "testing", "sketchfab", "veh*", "vehicles", "door", "mirror", "elevator", "pickup", "aas", "video_player", "portals", "optimization")'),
  scaffold: z.boolean().default(false).describe('If true, automatically creates and sets up a pre-configured GameObject hierarchy in the active Unity scene'),
  objectName: z.string().optional().describe('Optional custom name for the scaffolded GameObject hierarchy'),
  reason: z.string().optional().describe('Optional explanation of why the CCK recipe is being consulted')
});

const SETUP_TOPIC_ALIASES = new Set([
  'setup',
  'install',
  'installation',
  'cck4',
  'cck3',
  'unity_version',
  'upgrade',
  'login',
  'master_key',
  'publish',
  'getting_started'
]);

export function registerHowtoCckTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, params: z.infer<typeof paramsSchema>): Promise<CallToolResult> {
  const normalizedTopic = (params.topic || '*').trim().toLowerCase();
  const isSetupQuery = SETUP_TOPIC_ALIASES.has(normalizedTopic);

  let forwardParams: Record<string, unknown> = { ...params };
  let liveCckInfo: Awaited<ReturnType<typeof fetchCvrCckInfo>> | null = null;

  if (isSetupQuery) {
    liveCckInfo = await fetchCvrCckInfo();
    forwardParams = {
      ...forwardParams,
      cckVersion: liveCckInfo.cckVersion,
      unityVersion: liveCckInfo.unityVersion,
      cckDownloadUrl: liveCckInfo.cckDownloadUrl,
      unityHubInstallUrl: liveCckInfo.unityHubInstallUrl,
      unityDownloadUrl: liveCckInfo.unityDownloadUrl,
      cckInfoSource: liveCckInfo.source,
      maxAvatarBundleMb: bytesToMb(liveCckInfo.contentLimits.maxAvatarBundleBytes),
      maxSpawnableBundleMb: bytesToMb(liveCckInfo.contentLimits.maxSpawnableBundleBytes),
      maxWorldBundleMb: bytesToMb(liveCckInfo.contentLimits.maxWorldBundleBytes)
    };
  }

  let response: any;
  try {
    response = await mcpUnity.sendRequest({
      method: toolName,
      params: forwardParams
    }, { timeout: getToolTimeout(toolName) });
  } catch (error) {
    // Allow consulting official CCK setup instructions even before Unity is open/connected
    if (isSetupQuery && !params.scaffold && liveCckInfo) {
      const guide = formatCckSetupGuide(liveCckInfo);
      return {
        content: [{
          type: 'text',
          text: guide
        }],
        structuredContent: {
          topic: 'setup',
          guide,
          cckInfo: {
            cckVersion: liveCckInfo.cckVersion,
            unityVersion: liveCckInfo.unityVersion,
            cckDownloadUrl: liveCckInfo.cckDownloadUrl,
            unityHubInstallUrl: liveCckInfo.unityHubInstallUrl,
            source: liveCckInfo.source,
            maxAvatarBundleMb: bytesToMb(liveCckInfo.contentLimits.maxAvatarBundleBytes),
            maxSpawnableBundleMb: bytesToMb(liveCckInfo.contentLimits.maxSpawnableBundleBytes),
            maxWorldBundleMb: bytesToMb(liveCckInfo.contentLimits.maxWorldBundleBytes)
          },
          servedWithoutUnity: true
        }
      };
    }
    throw error;
  }

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to consult howto_cck`
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || response.guide || `Successfully fetched CCK recipe for ${params.topic}`
    }],
    structuredContent: {
      topic: response.topic,
      guide: response.guide,
      scaffoldedObject: response.scaffoldedObject,
      instanceId: response.instanceId,
      count: response.count,
      topics: response.topics,
      cckInfo: response.cckInfo
    }
  };
}
