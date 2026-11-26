import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'manage_cck_test_mode';
const toolDescription = "Enters/exits ChilloutVR CCK's built-in in-Editor Test Mode (spawns a walkable player and builds a " +
  "temporary version of the selected World/Avatar/Prop inside Unity PlayMode — does NOT require ChilloutVR installed), " +
  "or reports whether it's currently active. Distinct from CCK's 'Test Your Content Locally', which launches external " +
  "ChilloutVR via chilloutvr:// (requires Steam ChilloutVR installed and run once). Note: 'PlayerLoop internal function " +
  "has been called recursively' and 'Build Finished, Result: Failure' during CCK builds are known benign Unity logs; " +
  "consult howto_cck(topic='testing') for bundle verification.";
const paramsSchema = z.object({
  action: z.enum(['enter', 'exit', 'status']).default('enter').describe('Which Test Mode action to perform'),
  objectPath: z.string().optional().describe('Hierarchy path of a specific CVRAssetInfo-bearing GameObject to test (defaults to auto-picking the World in the scene, or the only CVRAssetInfo found)'),
  instanceId: z.number().optional().describe('Instance ID alternative to objectPath')
});

/**
 * Creates and registers the CCK Test Mode tool with the MCP server
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerCvrTestModeTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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

/**
 * Handles requests to enter/exit CCK Test Mode or query its status
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>
): Promise<CallToolResult> {
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      action: params.action,
      objectPath: params.objectPath,
      instanceId: params.instanceId
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || `Failed to ${params.action} CCK Test Mode`
    );
  }

  return {
    content: [{
      type: 'text',
      text: response.message || JSON.stringify(response, null, 2)
    }]
  };
}
