import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// Constants for the tool
const toolName = 'click_ui_element';
const toolDescription = 'Clicks a Button (or Button-like) UI Toolkit element inside an open Editor window, matched ' +
  'by elementName (VisualElement.name) or elementText (visible label). Use list_editor_windows first to discover ' +
  'available windows/elements. Set dryRun to true to list matching candidates without clicking.';
const paramsSchema = z.object({
  windowTitle: z.string().describe('Substring to match against the target window\'s title or type name'),
  elementName: z.string().optional().describe('The VisualElement.name of the button to click'),
  elementText: z.string().optional().describe('Substring of the visible text/label of the button to click'),
  dryRun: z.boolean().optional().describe('If true, lists matching elements without clicking any of them')
});

/**
 * Creates and registers the Click UI Element tool with the MCP server
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerClickUiElementTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
 * Handles requests to click a UI Toolkit element inside an editor window
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
  if (!params.elementName && !params.elementText) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      "Provide at least one of 'elementName' or 'elementText'"
    );
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      windowTitle: params.windowTitle,
      elementName: params.elementName,
      elementText: params.elementText,
      dryRun: params.dryRun
    }
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || 'Failed to click UI element'
    );
  }

  return {
    content: [{
      type: 'text',
      text: response.message || JSON.stringify(response, null, 2)
    }]
  };
}
