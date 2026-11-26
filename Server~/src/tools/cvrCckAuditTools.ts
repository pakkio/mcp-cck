import * as z from 'zod';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { getToolTimeout } from '../utils/timeouts.js';
import { bytesToMb, fetchCvrCckInfo } from '../utils/cvrCckInfo.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// ============================================================================
// inspect_cvr_cck
// ============================================================================

const toolName = 'inspect_cvr_cck';
const toolDescription = 'Audits and validates ChilloutVR CCK content (Worlds, Avatars, Props) and project setup (Unity version vs live CCK API, Built-In Render Pipeline, CCK installation, folder hygiene) for upload readiness and performance budgets.';
const paramsSchema = z.object({
  action: z.enum(['validate_content', 'get_stats', 'check_setup']).default('validate_content').describe('Audit action to execute ("validate_content", "get_stats", or "check_setup" to verify Unity version, Built-In RP, and CCK installation)'),
  contentType: z.enum(['world', 'avatar', 'spawnable']).default('world').describe('Type of ChilloutVR content to audit (used for validate_content / get_stats)'),
  instanceId: z.number().optional().describe('Instance ID of target avatar or prop root object (required for avatar/spawnable)'),
  objectPath: z.string().optional().describe('Hierarchy path of target avatar or prop root object (required for avatar/spawnable)')
});

export function registerInspectCvrCckTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  let forwardParams: Record<string, unknown> = { ...params };

  if (params.action === 'check_setup') {
    const liveCckInfo = await fetchCvrCckInfo();
    forwardParams = {
      ...forwardParams,
      expectedUnityVersion: liveCckInfo.unityVersion,
      expectedCckVersion: liveCckInfo.cckVersion,
      cckInfoSource: liveCckInfo.source,
      maxAvatarBundleMb: bytesToMb(liveCckInfo.contentLimits.maxAvatarBundleBytes),
      maxSpawnableBundleMb: bytesToMb(liveCckInfo.contentLimits.maxSpawnableBundleBytes),
      maxWorldBundleMb: bytesToMb(liveCckInfo.contentLimits.maxWorldBundleBytes)
    };
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: forwardParams
  }, { timeout: getToolTimeout(toolName) });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to audit CVR CCK content`
    );
  }

  return {
    content: [{
      type: response.type || 'text',
      text: response.message || `Successfully audited CVR CCK content`
    }],
    structuredContent: {
      status: response.status,
      contentType: response.contentType,
      errors: response.errors,
      warnings: response.warnings,
      suggestions: response.suggestions,
      metrics: response.metrics,
      environment: response.environment
    }
  };
}
