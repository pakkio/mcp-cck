// Import MCP SDK components
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { McpUnity } from './unity/mcpUnity.js';
import { Logger, LogLevel } from './utils/logger.js';
import { registerCreateSceneTool } from './tools/createSceneTool.js';
import { registerMenuItemTool } from './tools/menuItemTool.js';
import { registerSelectGameObjectTool } from './tools/selectGameObjectTool.js';
import { registerAddPackageTool } from './tools/addPackageTool.js';
import { registerRunTestsTool } from './tools/runTestsTool.js';
import { registerSendConsoleLogTool } from './tools/sendConsoleLogTool.js';
import { registerGetConsoleLogsTool } from './tools/getConsoleLogsTool.js';
import { registerUpdateComponentTool } from './tools/updateComponentTool.js';
import { registerRemoveComponentTool } from './tools/removeComponentTool.js';
import { registerAddAssetToSceneTool } from './tools/addAssetToSceneTool.js';
import { registerUpdateGameObjectTool } from './tools/updateGameObjectTool.js';
import { registerCreatePrefabTool } from './tools/createPrefabTool.js';
import { registerDeleteSceneTool } from './tools/deleteSceneTool.js';
import { registerLoadSceneTool } from './tools/loadSceneTool.js';
import { registerSaveSceneTool } from './tools/saveSceneTool.js';
import { registerGetSceneInfoTool } from './tools/getSceneInfoTool.js';
import { registerGetEditorStateTool } from './tools/getEditorStateTool.js';
import { registerGetPlayModeStatusTool } from './tools/getPlayModeStatusTool.js';
import { registerSetPlayModeStatusTool } from './tools/setPlayModeStatusTool.js';
import { registerUnloadSceneTool } from './tools/unloadSceneTool.js';
import { registerRecompileScriptsTool } from './tools/recompileScriptsTool.js';
import { registerExportPackageTool } from './tools/exportPackageTool.js';
import { registerAssetManagementTools } from './tools/assetManagementTools.js';
import { registerBuildAndAnalysisTools } from './tools/buildAndAnalysisTools.js';
import { registerPhysicsAndComponentTools } from './tools/physicsAndComponentTools.js';
import { registerDebugDrawTool } from './tools/debugDrawTool.js';
import { registerGetGameObjectTool } from './tools/getGameObjectTool.js';
import { registerTransformTools } from './tools/transformTools.js';
import { registerCreateMaterialTool, registerAssignMaterialTool, registerModifyMaterialTool, registerGetMaterialInfoTool } from './tools/materialTools.js';
import { registerDuplicateGameObjectTool, registerDeleteGameObjectTool, registerReparentGameObjectTool, registerSetSiblingIndexTool } from './tools/gameObjectTools.js';
import { registerCaptureScreenshotTool } from './tools/captureScreenshotTool.js';
import { registerGetBoundsTool, registerPlaceNextToTool, registerFindLocalAssetsTool, registerImportLocalFileTool, registerMeasureDistanceTool, registerGetFloorHeightTool, registerGetNearbyObjectsTool, registerFrameCameraOnTool } from './tools/spatialTools.js';
import { registerApplyPrefabOverridesTool, registerRevertPrefabOverridesTool, registerUnpackPrefabTool } from './tools/prefabTools.js';
import { registerManageTagsAndLayersTool } from './tools/tagsAndLayersTools.js';
import { registerCreateScriptableObjectTool } from './tools/scriptableObjectTools.js';
import { registerSetRectTransformTool } from './tools/rectTransformTools.js';
import { registerManageTerrainTool } from './tools/terrainTools.js';
import { registerProBuilderCreateShapeTool, registerProBuilderMeshOpTool } from './tools/probuilderTools.js';
import { registerManageLightingTool, registerConfigureLightProbeGroupTool, registerCreateReflectionProbeTool } from './tools/lightingTools.js';
import { registerManageOcclusionCullingTool, registerConfigureLODGroupTool, registerConfigureCameraCullingTool } from './tools/cullingTools.js';
import { registerConfigureCollidersTool } from './tools/colliderTools.js';
import { registerConfigureTextureSettingsTool } from './tools/textureTools.js';
import { registerManageNavMeshTool } from './tools/navmeshTools.js';
import { registerConfigurePostProcessingTool } from './tools/postProcessingTools.js';
import { registerCreateVirtualCameraTool } from './tools/cinemachineTools.js';
import { registerManageCvrWorldTool } from './tools/cvrWorldTools.js';
import { registerConfigureCvrInteractivityTool } from './tools/cvrInteractivityTools.js';
import { registerManageCvrAvatarTool } from './tools/cvrAvatarTools.js';
import { registerInspectCvrCckTool } from './tools/cvrCckAuditTools.js';
import { registerHowtoCckTool } from './tools/cvrHowToTools.js';
import { registerConfigureCvrVehicleTool } from './tools/cvrVehicleTools.js';
import { registerListEditorWindowsTool } from './tools/listEditorWindowsTool.js';
import { registerClickUiElementTool } from './tools/clickUiElementTool.js';
import { registerBatchExecuteTool } from './tools/batchExecuteTool.js';
import { registerShowUnityDashboardTool } from './tools/showUnityDashboardTool.js';
import { registerGetScenesHierarchyTool } from './tools/getScenesHierarchyTool.js';
import { registerGetMenuItemsResource } from './resources/getMenuItemResource.js';
import { registerGetConsoleLogsResource } from './resources/getConsoleLogsResource.js';
import { registerGetHierarchyResource } from './resources/getScenesHierarchyResource.js';
import { registerGetPackagesResource } from './resources/getPackagesResource.js';
import { registerGetAssetsResource } from './resources/getAssetsResource.js';
import { registerGetTestsResource } from './resources/getTestsResource.js';
import { registerGetGameObjectResource } from './resources/getGameObjectResource.js';
import { registerUnityDashboardAppResource } from './resources/unityDashboardAppResource.js';
import { registerGameObjectHandlingPrompt } from './prompts/gameobjectHandlingPrompt.js';
import { registerUnityDashboardPrompt } from './prompts/unityDashboardPrompt.js';

// Initialize loggers
const serverLogger = new Logger('Server', LogLevel.INFO);
const unityLogger = new Logger('Unity', LogLevel.INFO);
const toolLogger = new Logger('Tools', LogLevel.INFO);
const resourceLogger = new Logger('Resources', LogLevel.INFO);

// Initialize the MCP server
const server = new McpServer (
  {
    name: "MCP Unity Server",
    version: "1.7.1"
  },
  {
    capabilities: {
      tools: {},
      resources: {},
      prompts: {},
    },
  }
);

// Initialize MCP HTTP bridge with Unity editor
const mcpUnity = new McpUnity(unityLogger);

// Register all tools into the MCP server
registerMenuItemTool(server, mcpUnity, toolLogger);
registerSelectGameObjectTool(server, mcpUnity, toolLogger);
registerAddPackageTool(server, mcpUnity, toolLogger);
registerRunTestsTool(server, mcpUnity, toolLogger);
registerSendConsoleLogTool(server, mcpUnity, toolLogger);
registerGetConsoleLogsTool(server, mcpUnity, toolLogger);
registerUpdateComponentTool(server, mcpUnity, toolLogger);
registerRemoveComponentTool(server, mcpUnity, toolLogger);
registerAddAssetToSceneTool(server, mcpUnity, toolLogger);
registerUpdateGameObjectTool(server, mcpUnity, toolLogger);
registerCreatePrefabTool(server, mcpUnity, toolLogger);
registerCreateSceneTool(server, mcpUnity, toolLogger);
registerDeleteSceneTool(server, mcpUnity, toolLogger);
registerLoadSceneTool(server, mcpUnity, toolLogger);
registerSaveSceneTool(server, mcpUnity, toolLogger);
registerGetSceneInfoTool(server, mcpUnity, toolLogger);
registerGetEditorStateTool(server, mcpUnity, toolLogger);
registerGetPlayModeStatusTool(server, mcpUnity, toolLogger);
registerSetPlayModeStatusTool(server, mcpUnity, toolLogger);
registerShowUnityDashboardTool(server, toolLogger);
registerGetScenesHierarchyTool(server, mcpUnity, toolLogger);
registerUnloadSceneTool(server, mcpUnity, toolLogger);
registerRecompileScriptsTool(server, mcpUnity, toolLogger);
registerExportPackageTool(server, mcpUnity, toolLogger);
registerAssetManagementTools(server, mcpUnity, toolLogger);
registerBuildAndAnalysisTools(server, mcpUnity, toolLogger);
registerPhysicsAndComponentTools(server, mcpUnity, toolLogger);
registerDebugDrawTool(server, mcpUnity, toolLogger);
registerGetGameObjectTool(server, mcpUnity, toolLogger);
registerTransformTools(server, mcpUnity, toolLogger);
registerDuplicateGameObjectTool(server, mcpUnity, toolLogger);
registerDeleteGameObjectTool(server, mcpUnity, toolLogger);
registerReparentGameObjectTool(server, mcpUnity, toolLogger);
registerSetSiblingIndexTool(server, mcpUnity, toolLogger);
registerCaptureScreenshotTool(server, mcpUnity, toolLogger);
registerGetBoundsTool(server, mcpUnity, toolLogger);
registerPlaceNextToTool(server, mcpUnity, toolLogger);
registerFindLocalAssetsTool(server, mcpUnity, toolLogger);
registerImportLocalFileTool(server, mcpUnity, toolLogger);
registerMeasureDistanceTool(server, mcpUnity, toolLogger);
registerGetFloorHeightTool(server, mcpUnity, toolLogger);
registerGetNearbyObjectsTool(server, mcpUnity, toolLogger);
registerFrameCameraOnTool(server, mcpUnity, toolLogger);

// Register Material Tools
registerCreateMaterialTool(server, mcpUnity, toolLogger);
registerAssignMaterialTool(server, mcpUnity, toolLogger);
registerModifyMaterialTool(server, mcpUnity, toolLogger);
registerGetMaterialInfoTool(server, mcpUnity, toolLogger);

// Register Prefab Tools
registerApplyPrefabOverridesTool(server, mcpUnity, toolLogger);
registerRevertPrefabOverridesTool(server, mcpUnity, toolLogger);
registerUnpackPrefabTool(server, mcpUnity, toolLogger);

// Register Tags & Layers Tools
registerManageTagsAndLayersTool(server, mcpUnity, toolLogger);

// Register ScriptableObject Tools
registerCreateScriptableObjectTool(server, mcpUnity, toolLogger);

// Register RectTransform Tools
registerSetRectTransformTool(server, mcpUnity, toolLogger);

// Register Terrain Tools
registerManageTerrainTool(server, mcpUnity, toolLogger);

// Register ProBuilder Tools
registerProBuilderCreateShapeTool(server, mcpUnity, toolLogger);
registerProBuilderMeshOpTool(server, mcpUnity, toolLogger);

// Register Lighting Tools
registerManageLightingTool(server, mcpUnity, toolLogger);
registerConfigureLightProbeGroupTool(server, mcpUnity, toolLogger);
registerCreateReflectionProbeTool(server, mcpUnity, toolLogger);

// Register Culling & Optimization Tools
registerManageOcclusionCullingTool(server, mcpUnity, toolLogger);
registerConfigureLODGroupTool(server, mcpUnity, toolLogger);
registerConfigureCameraCullingTool(server, mcpUnity, toolLogger);

// Register Collider and Texture Tools
registerConfigureCollidersTool(server, mcpUnity, toolLogger);
registerConfigureTextureSettingsTool(server, mcpUnity, toolLogger);

// Register AI Navigation, Post-Processing, and Cinemachine Tools
registerManageNavMeshTool(server, mcpUnity, toolLogger);
registerConfigurePostProcessingTool(server, mcpUnity, toolLogger);
registerCreateVirtualCameraTool(server, mcpUnity, toolLogger);

// Register ChilloutVR CCK Tools
registerManageCvrWorldTool(server, mcpUnity, toolLogger);
registerConfigureCvrInteractivityTool(server, mcpUnity, toolLogger);
registerManageCvrAvatarTool(server, mcpUnity, toolLogger);
registerInspectCvrCckTool(server, mcpUnity, toolLogger);
registerHowtoCckTool(server, mcpUnity, toolLogger);
registerConfigureCvrVehicleTool(server, mcpUnity, toolLogger);

// Register Editor UI interaction tools
registerListEditorWindowsTool(server, mcpUnity, toolLogger);
registerClickUiElementTool(server, mcpUnity, toolLogger);

// Register Batch Execute Tool (high-priority for performance)
registerBatchExecuteTool(server, mcpUnity, toolLogger);

// Register all resources into the MCP server
registerGetTestsResource(server, mcpUnity, resourceLogger);
registerGetGameObjectResource(server, mcpUnity, resourceLogger);
registerGetMenuItemsResource(server, mcpUnity, resourceLogger);
registerGetConsoleLogsResource(server, mcpUnity, resourceLogger);
registerGetHierarchyResource(server, mcpUnity, resourceLogger);
registerGetPackagesResource(server, mcpUnity, resourceLogger);
registerGetAssetsResource(server, mcpUnity, resourceLogger);
registerUnityDashboardAppResource(server, resourceLogger);

// Register all prompts into the MCP server
registerGameObjectHandlingPrompt(server);
registerUnityDashboardPrompt(server);

// Server startup function
async function startServer() {
  try {
    // Initialize STDIO transport for MCP client communication
    const stdioTransport = new StdioServerTransport();
    
    // Connect the server to the transport
    await server.connect(stdioTransport);

    serverLogger.info('MCP Server started');
    
    // Get the client name from the MCP server
    const clientName = server.server.getClientVersion()?.name || 'Unknown MCP Client';
    serverLogger.info(`Connected MCP client: ${clientName}`);
    
    // Start Unity Bridge connection with client name in headers
    await mcpUnity.start(clientName);
    
  } catch (error) {
    serverLogger.error('Failed to start server', error);
    process.exit(1);
  }
}

// Graceful shutdown handler
let isShuttingDown = false;
async function shutdown() {
  if (isShuttingDown) return;
  isShuttingDown = true;

  try {
    serverLogger.info('Shutting down...');
    await mcpUnity.stop();
    await server.close();
  } catch (error) {
    // Ignore errors during shutdown
  }
  process.exit(0);
}

// Start the server
startServer();

// Handle shutdown signals
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
process.on('SIGHUP', shutdown);

// Handle stdin close (when MCP client disconnects)
process.stdin.on('close', shutdown);
process.stdin.on('end', shutdown);
process.stdin.on('error', shutdown);

// Handle uncaught exceptions - exit cleanly if it's just a closed pipe
process.on('uncaughtException', (error: NodeJS.ErrnoException) => {
  // EPIPE/EOF errors are expected when the MCP client disconnects
  if (error.code === 'EPIPE' || error.code === 'EOF' || error.code === 'ERR_USE_AFTER_CLOSE') {
    shutdown();
    return;
  }
  serverLogger.error('Uncaught exception', error);
  process.exit(1);
});

// Handle unhandled promise rejections
process.on('unhandledRejection', (reason) => {
  serverLogger.error('Unhandled rejection', reason);
  process.exit(1);
});
