import * as z from 'zod';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { Logger } from '../utils/logger.js';
import { BlenderBridge } from '../unity/blenderBridge.js';
import * as fs from 'fs';
import * as path from 'path';

export function registerBlenderExportVehicleFbxTool(server: McpServer, blender: BlenderBridge, logger: Logger) {
    const toolName = 'blender_export_vehicle_fbx';
    server.tool(toolName, 'Export a Blender vehicle object as Unity-ready FBX (axis-corrected). Forwards to mcp-blender extension over WebSocket.', {
        object_name: z.string().describe('Name of the Blender object to export'),
        export_path: z.string().describe('File path for the exported FBX')
    }, async (params: any) => {
        logger.info(`Executing ${toolName}: ${params.object_name} → ${params.export_path}`);
        try {
            const result = await blender.sendRequest('blender_render_pipeline', { action: 'export_unity_fbx', params: { object_name: params.object_name, export_path: params.export_path } }, 120000);
            return { content: [{ type: 'text' as const, text: JSON.stringify(result, null, 2) }] };
        } catch (err: any) {
            return { content: [{ type: 'text' as const, text: `Error: ${err.message}` }], isError: true };
        }
    });
}

export function registerBlenderGetVehicleInfoTool(server: McpServer, blender: BlenderBridge, logger: Logger) {
    const toolName = 'blender_get_vehicle_info';
    server.tool(toolName, 'Inspect a vehicle mesh in Blender: vertex count, triangle count, bounds, origin position.', {
        object_name: z.string().describe('Name of the Blender object to inspect')
    }, async (params: any) => {
        logger.info(`Executing ${toolName}: ${params.object_name}`);
        try {
            const result = await blender.sendRequest('get_object_info', { object_name: params.object_name }, 10000);
            return { content: [{ type: 'text' as const, text: JSON.stringify(result, null, 2) }] };
        } catch (err: any) {
            return { content: [{ type: 'text' as const, text: `Error: ${err.message}` }], isError: true };
        }
    });
}

export function registerBlenderImportVehicleToUnityTool(server: McpServer, logger: Logger) {
    const toolName = 'blender_import_vehicle_to_unity';
    server.tool(toolName, 'Copy an exported FBX from Blender export path into the Unity project Assets folder.', {
        fbx_path: z.string().describe('Source FBX file path'),
        unity_asset_path: z.string().describe('Destination path inside Unity Assets/')
    }, async (params: any) => {
        logger.info(`Executing ${toolName}: ${params.fbx_path} → ${params.unity_asset_path}`);
        try {
            if (!fs.existsSync(params.fbx_path)) return { content: [{ type: 'text' as const, text: `Error: FBX not found: ${params.fbx_path}` }], isError: true };
            const dest = path.resolve(params.unity_asset_path);
            fs.mkdirSync(path.dirname(dest), { recursive: true });
            fs.copyFileSync(params.fbx_path, dest);
            const rel = path.relative(path.resolve('Assets'), dest).split(path.sep).join('/');
            return { content: [{ type: 'text' as const, text: JSON.stringify({ asset_path: rel, absolute_path: dest, note: 'Unity imports on next AssetDatabase refresh; call unity_add_asset_to_scene to place it' }, null, 2) }] };
        } catch (err: any) {
            return { content: [{ type: 'text' as const, text: `Error: ${err.message}` }], isError: true };
        }
    });
}

export function registerPipelineBuildVehicleTool(server: McpServer, blender: BlenderBridge, mcpUnity: any, logger: Logger) {
    const toolName = 'pipeline_build_vehicle';
    server.tool(toolName, 'End-to-end vehicle pipeline: export from Blender → copy to Unity Assets → import into scene → rig as CVR vehicle. Returns per-step status for resume on failure.', {
        blender_object_name: z.string().describe('Name of the Blender object'),
        blender_export_path: z.string().describe('Export path for the FBX'),
        unity_asset_path: z.string().describe('Destination in Unity Assets/'),
        vehicle_name: z.string().default('CVR_Drivable_Car').describe('Name for the vehicle GameObject'),
        position: z.array(z.number()).optional().describe('[x, y, z] position in scene'),
        seat_count: z.number().int().min(1).max(8).default(3).describe('Number of passenger seats')
    }, async (params: any) => {
        logger.info(`Executing ${toolName}: ${params.blender_object_name}`);
        const steps: any[] = [];
        const pos = params.position || [0, 0, 0];

        try {
            const r = await blender.sendRequest('blender_render_pipeline', { action: 'export_unity_fbx', params: { object_name: params.blender_object_name, export_path: params.blender_export_path } }, 120000);
            steps.push({ step: 'blender_export', status: 'ok', result: r });
        } catch (err: any) {
            steps.push({ step: 'blender_export', status: 'failed', error: err.message });
            return { content: [{ type: 'text' as const, text: JSON.stringify({ ok: false, steps, error: err.message }, null, 2) }] };
        }

        try {
            const dest = path.resolve(params.unity_asset_path);
            fs.mkdirSync(path.dirname(dest), { recursive: true });
            fs.copyFileSync(params.blender_export_path, dest);
            const rel = path.relative(path.resolve('Assets'), dest).split(path.sep).join('/');
            steps.push({ step: 'copy_to_unity', status: 'ok', asset_path: rel });
        } catch (err: any) {
            steps.push({ step: 'copy_to_unity', status: 'failed', error: err.message });
            return { content: [{ type: 'text' as const, text: JSON.stringify({ ok: false, steps, error: err.message }, null, 2) }] };
        }

        const assetPath = steps[1].asset_path;
        try {
            const r = await mcpUnity.sendRequest({ method: 'add_asset_to_scene', params: { assetPath: assetPath, position: { x: pos[0], y: pos[1], z: pos[2] } } });
            steps.push({ step: 'add_to_scene', status: 'ok', result: r });
        } catch (err: any) {
            steps.push({ step: 'add_to_scene', status: 'failed', error: err.message });
            return { content: [{ type: 'text' as const, text: JSON.stringify({ ok: false, steps, error: err.message, note: 'FBX is in Assets, ready for retry' }, null, 2) }] };
        }

        const objectPath = steps[2].result?.objectPath || params.vehicle_name;
        try {
            const r = await mcpUnity.sendRequest({ method: 'configure_cvr_vehicle', params: { action: 'create_car_rig', objectPath: objectPath, vehicleName: params.vehicle_name, seatCount: params.seat_count } });
            steps.push({ step: 'configure_cvr_vehicle', status: 'ok', result: r });
        } catch (err: any) {
            steps.push({ step: 'configure_cvr_vehicle', status: 'failed', error: err.message });
            return { content: [{ type: 'text' as const, text: JSON.stringify({ ok: false, steps, error: err.message, note: 'Vehicle in scene, resume with unity_configure_cvr_vehicle', partial_object_path: objectPath }, null, 2) }] };
        }

        return { content: [{ type: 'text' as const, text: JSON.stringify({ ok: true, vehicle_name: params.vehicle_name, object_path: objectPath, fbx_path: params.blender_export_path, unity_asset_path: params.unity_asset_path, steps }, null, 2) }] };
    });
}

export function registerPipelineBridgeStatusTool(server: McpServer, blender: BlenderBridge, mcpUnity: any, logger: Logger) {
    const toolName = 'pipeline_get_bridge_status';
    server.tool(toolName, 'Health check for both Blender and Unity WebSocket connections.', {}, async () => {
        logger.info(`Executing ${toolName}`);
        const blenderState = blender.connectionState;
        const blenderUrl = blender.url;
        let unityState = 'unknown';
        let unityOk = false;
        try {
            await mcpUnity.sendRequest({ method: 'get_scene_info', params: {} }, { timeout: 5000 });
            unityOk = true;
            unityState = 'connected';
        } catch { unityState = 'unreachable'; }
        return { content: [{ type: 'text' as const, text: JSON.stringify({ blender: { state: blenderState, url: blenderUrl }, unity: { state: unityState, url: 'ws://localhost:8090/McpUnity', reachable: unityOk } }, null, 2) }] };
    });
}
