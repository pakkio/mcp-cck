## MCP Unity — AI Agent Guide (MCP Package)

### Purpose (what this repo is)
**MCP Unity** exposes Unity Editor capabilities to MCP-enabled clients by running:
- **Unity-side “client” (C# Editor scripts)**: a WebSocket server inside the Unity Editor that executes tools/resources.
- **Node-side “server” (TypeScript)**: an MCP stdio server that registers MCP tools/resources and forwards requests to Unity over WebSocket.

### How it works (high-level data flow)
- **MCP client** ⇄ (stdio / MCP SDK) ⇄ **Node server** (`Server~/src/index.ts`)
- **Node server** ⇄ (WebSocket JSON-RPC-ish) ⇄ **Unity Editor** (`Editor/UnityBridge/McpUnityServer.cs` + `McpUnitySocketHandler.cs`)
- **Tool/Resource names must match exactly** across Node and Unity (typically `lower_snake_case`).

### Key defaults & invariants
- **Unity WebSocket endpoint**: `ws://localhost:8090/McpUnity` by default.
- **Config file**: `ProjectSettings/McpUnitySettings.json` (written/read by Unity; read opportunistically by Node).
- **Execution thread**: Tool/resource execution is dispatched via `EditorCoroutineUtility` and runs on the **Unity main thread**. Keep synchronous work short; use async patterns for long work.

### Repo layout (where to change what)
```
/
├── Editor/                       # Unity Editor package code (C#)
│   ├── Tools/                    # Tools (inherit McpToolBase)
│   ├── Resources/                # Resources (inherit McpResourceBase)
│   ├── UnityBridge/              # WebSocket server + message routing
│   ├── Services/                 # Test/log services used by tools/resources
│   └── Utils/                    # Shared helpers (config, logging, workspace integration)
├── Server~/                      # Node MCP server (TypeScript, ESM)
│   ├── src/index.ts              # Registers tools/resources/prompts with MCP SDK
│   ├── src/tools/                # MCP tool definitions (zod schema + handler)
│   ├── src/resources/            # MCP resource definitions
│   └── src/unity/mcpUnity.ts      # WebSocket client that talks to Unity
```

### Quickstart (local dev)
- **Unity side**
  - Open the Unity project that has this package installed.
  - Ensure the server is running (auto-start is controlled by `McpUnitySettings.AutoStartServer`).
  - Settings persist in `ProjectSettings/McpUnitySettings.json`.

- **Node side (build)**
  - `cd Server~ && npm run build`
  - The MCP entrypoint is `Server~/build/index.js` (published as an MCP stdio server).

- **Node side (debug/inspect)**
  - `cd Server~ && npm run inspector` to use the MCP Inspector.

### Configuration (Unity ↔ Node bridge)
The Unity settings file is the shared contract:
- **Path**: `ProjectSettings/McpUnitySettings.json`
- **Fields**
  - **Port** (default **8090**): Unity WebSocket server port.
  - **RequestTimeoutSeconds** (default **10**): Node request timeout.
  - **AllowBatchModeServer** (default **false**): permits a persistent MCP host in Unity `-batchmode`; package installation remains disabled in batch mode.
  - **AllowRemoteConnections** (default **false**): Unity binds to `0.0.0.0` when enabled; otherwise `localhost`.
  - **EnableInfoLogs**: Unity console logging verbosity.
  - **NpmExecutablePath**: optional npm path for Unity-driven install/build.

Node resolves bridge configuration in this order:
- explicit environment values: `UNITY_PORT`, `UNITY_HOST`, `UNITY_REQUEST_TIMEOUT`;
- `MCP_UNITY_SETTINGS_PATH` when set;
- `ProjectSettings/McpUnitySettings.json` discovered above the installed Node module, then above the current working directory.

If no valid setting is found, Node falls back to:
- **host**: `localhost`
- **port**: `8090`
- **timeout**: `10s`

**Remote connection note**:
- If Unity is on another machine, set `AllowRemoteConnections=true` in Unity and set `UNITY_HOST=<unity_machine_ip_or_hostname>` for the Node process.

**Persistent headless host note**:
- Batch mode is disabled by default. Enable `AllowBatchModeServer` or launch Unity with `MCP_UNITY_ALLOW_BATCH_MODE=true` for a long-lived headless MCP host; npm installation/build remains skipped in batch mode.

### Adding a new capability

### Add a tool
1. **Unity (C#)**
   - Add `Editor/Tools/<YourTool>Tool.cs` inheriting `McpToolBase`.
   - Set `Name` to the MCP tool name (recommended: `lower_snake_case`).
   - Implement:
     - `Execute(JObject parameters)` for synchronous work, or
     - set `IsAsync = true` and implement `ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)` for long-running operations.
   - Register it in `Editor/UnityBridge/McpUnityServer.cs` (`RegisterTools()`).

2. **Node (TypeScript)**
   - Add `Server~/src/tools/<yourTool>Tool.ts`.
   - Register the tool in `Server~/src/index.ts`.
   - Use a zod schema for params; forward to Unity using the same `method` string:
     - `mcpUnity.sendRequest({ method: toolName, params: {...} })`

3. **Build**
   - `cd Server~ && npm run build`

### Add a resource
1. **Unity (C#)**
   - Add `Editor/Resources/<YourResource>Resource.cs` inheriting `McpResourceBase`.
   - Set `Name` (method string) and `Uri` (e.g. `unity://...`).
   - Implement `Fetch(...)` or `FetchAsync(...)`.
   - Register in `Editor/UnityBridge/McpUnityServer.cs` (`RegisterResources()`).

2. **Node (TypeScript)**
   - Add `Server~/src/resources/<yourResource>.ts`, register in `Server~/src/index.ts`.
   - Forward to Unity via `mcpUnity.sendRequest({ method: resourceName, params: {} })`.

### Logging & debugging
- **Unity**
  - Uses `McpUnity.Utils.McpLogger` (info logs gated by `EnableInfoLogs`).
  - Connection lifecycle is managed in `Editor/UnityBridge/McpUnityServer.cs` (domain reload & playmode transitions stop/restart the server).

- **Node**
  - Logging is controlled by env vars:
    - `LOGGING=true` enables console logging.
    - `LOGGING_FILE=true` writes `log.txt` in the Node process working directory.

### Common pitfalls
- **Port mismatch**: Unity default is **8090**; update docs/config if you change it.
- **Name mismatch**: Node `toolName`/`resourceName` must equal Unity `Name` exactly, or Unity responds `unknown_method`.
- **Long main-thread work**: synchronous `Execute()` blocks the Unity editor; use async patterns for heavy operations.
- **Remote connections**: Unity must bind `0.0.0.0` (`AllowRemoteConnections=true`) and Node must target the correct host (`UNITY_HOST`).
- **Unity domain reload**: the server stops during script reloads (including after `add_package` installs like ProBuilder) and reconnects automatically; avoid relying on persistent in-memory state across reloads.
- **Multiplayer Play Mode**: Clone instances automatically skip server startup; only the main editor hosts the MCP server.
- **Schema compatibility across clients**: avoid reusing the same nested Zod object instance for multiple sibling fields (for example `position`, `rotation`, `scale`). Some MCP clients fail on local refs like `#/properties/position`; prefer creating a fresh nested schema per field.
- **CCK local build benign errors & `chilloutvr://`**: During CCK builds, `"PlayerLoop internal function has been called recursively"` followed by `"Build Finished, Result: Failure"` are known benign Unity/CCK logs — verify the generated `.cvrworld`/`.cvravatar`/`.cvrprop` bundle in `Temp/` instead of treating them as scene failures. Use `manage_cck_test_mode` for in-Editor PlayMode testing; CCK "Test Your Content Locally" requires Steam ChilloutVR installed and launched once to register `chilloutvr://`.
- **Sketchfab / Blender FBX workflow**: Always inspect preview thumbnails and object/triangle counts before importing; never run per-object decimation on multi-object foliage (hangs Blender); normalize scale and base-origin pivot (`Z = 0`) and export FBX with embedded/copied textures and Cutout alpha materials for leaves.

### Release/version bump checklist
- Update versions consistently:
  - Unity package `package.json` (`version`)
  - Node server `Server~/package.json` (`version`)
- Rebuild Node output: `cd Server~ && npm run build`

### Available tools (current)
- `execute_menu_item` — Execute Unity menu items
- `select_gameobject` — Select GameObjects in hierarchy
- `update_gameobject` — Update or create GameObject properties
- `update_component` — Update or add components on GameObjects
- `remove_component` — Remove a component from a GameObject (undoable in Editor via Undo system)
- `add_package` — Install packages via Package Manager
- `run_tests` — Run Unity Test Runner tests
- `send_console_log` — Send logs to Unity console
- `add_asset_to_scene` — Add assets to scene
- `create_prefab` — Create prefabs with optional scripts
- `create_scene` — Create and save new scenes
- `load_scene` — Load scenes (single or additive)
- `delete_scene` — Delete scenes and remove from Build Settings
- `save_scene` — Save current scene (with optional Save As or saveAllModified, auto-saves untitled scenes)
- `get_scene_info` — Get active scene info and loaded scenes list
- `get_editor_state` — Get editor status, modal dialogs, open windows, and unsaved changes
- `get_play_mode_status` — Get Unity play mode status (isPlaying, isPaused)
- `set_play_mode_status` — Control Unity play mode (play, pause, stop, step)
- `unload_scene` — Unload scene from hierarchy
- `get_gameobject` — Get detailed GameObject info
- `capture_screenshot` — Captures a screenshot of the Scene view or a named camera
- `get_console_logs` — Retrieve Unity console logs
- `recompile_scripts` — Recompile all project scripts
- `duplicate_gameobject` — Duplicate GameObjects with optional rename/reparent
- `delete_gameobject` — Delete GameObjects from scene
- `reparent_gameobject` — Change GameObject parent in hierarchy
- `create_material` — Create materials with specified shader
- `assign_material` — Assign materials to Renderer components
- `modify_material` — Modify material properties (colors, floats, textures)
- `get_material_info` — Get material details including all properties
- `get_bounds` — Get combined world-space bounds for a GameObject
- `place_next_to` — Place one GameObject next to another using bounds and distance
- `find_local_assets` — Find assets in the Unity project by name or extension
- `import_local_file` — Copy an external file into Assets/ and import it
- `measure_distance` — Measure distance between two GameObjects (center and edge)
- `get_floor_height` — Raycast downward to find ground height at a position
- `get_nearby_objects` — Find GameObjects with colliders within a radius
- `frame_camera_on` — Move the Scene view camera to frame a GameObject
- `delete_asset` — Deletes assets/folders from the project
- `rename_asset` — Renames assets/folders in the project
- `create_folder` — Creates folders in the project
- `build_project` — Builds the Unity project
- `get_compilation_errors` — Gets C# compilation errors and warnings
- `find_script_references` — Finds references to a C# class/script in scene GameObjects
- `raycast_query` — Executes a physics raycast in the scene
- `copy_component` — Copies a component from one GameObject to another
- `draw_scene_gizmo` — Draws a temporary debug shape in the scene view
- `apply_prefab_overrides` — Applies modifications/overrides from a scene prefab instance back to its source Prefab Asset
- `revert_prefab_overrides` — Reverts property and component overrides on a scene prefab instance back to prefab asset defaults
- `unpack_prefab` — Unpacks a prefab instance in the scene into regular GameObjects
- `manage_tags_and_layers` — Gets or adds Unity Tags, Physics Layers (indices 8-31), and Sorting Layers
- `create_scriptable_object` — Creates a new ScriptableObject asset file (.asset) with optional serialized field values
- `set_rect_transform` — Sets RectTransform UI layout properties (anchor presets, anchoredPosition, sizeDelta, pivot, offsets)
- `manage_terrain` — Creates, sculpts, textures, and manages Unity Terrains and trees
- `probuilder_create_shape` — Generates 3D ProBuilder greyboxing shapes (cube, stair, cylinder, arch, prism, pipe, etc.)
- `probuilder_mesh_op` — Performs ProBuilder mesh operations (subdivide, export to asset, strip ProBuilder scripts)
- `manage_lighting` — Manages environment/ambient illumination, skybox, and lightmap baking (bake, cancel, clear, status)
- `configure_light_probe_group` — Creates or populates a LightProbeGroup with an automated 3D grid layout
- `create_reflection_probe` — Creates and configures Reflection Probes for localized environment reflections
- `manage_occlusion_culling` — Manages Occlusion Culling (baking, clearing, status, Occluder/Occludee static flags, Occlusion Areas/Portals)
- `configure_lod_group` — Configures LODGroup (Level of Detail) with distance/screen height thresholds and renderer slots
- `configure_camera_culling` — Configures Camera culling properties: layer-based draw distances (layerCullDistances) and far clip planes
- `configure_colliders` — Configures MeshColliders (convex hull, trigger, custom meshes), hierarchy colliders, and PhysicMaterials
- `configure_texture_settings` — Configures texture asset import settings (Normal Map, Sprite, Default, Max Size, sRGB, Read/Write)
- `manage_navmesh` — Manages AI Navigation (bakes/clears NavMesh, configures NavMeshAgents and NavMeshObstacles, queries pathfinding routes)
- `configure_post_processing` — Creates/configures Post-Processing Volumes and Volume Profiles (Bloom, Tonemapping, Vignette, Color Adjustments, DOF)
- `create_virtual_camera` — Creates/configures Cinemachine Virtual Camera rigs (Follow, LookAt, 3rd Person, First Person POV, Orbit)
- `manage_cvr_world` — Manages ChilloutVR CCK Worlds (CVRWorld settings, spawn points, optimized mirrors, seats/chairs, portals)
- `configure_cvr_interactivity` — Configures ChilloutVR CCK Interactivity (CVRInteractable triggers/actions, CVRPickupObject grips, CVRVariableBuffer sync)
- `manage_cvr_avatar` — Manages ChilloutVR CCK Avatars (CVRAvatar viewpoint/voice, visemes, eye blinks, Advanced Avatar Settings AAS)
- `inspect_cvr_cck` — Audits and validates ChilloutVR CCK content (Worlds, Avatars, Props) and project setup (`check_setup` verifies Unity version against live CCK API, Built-In RP, Color Space, and CCK installation)
- `howto_cck` — Provides official ChilloutVR CCK setup guide (`setup` with live `api.chilloutvr.net/1/public/cck/info` lookup), local testing/build error troubleshooting (`testing`), Sketchfab/Blender FBX workflow (`sketchfab`), expert recipes, best practices, and GameObject scaffolding for vehicles, doors, elevators, mirrors, pickups, AAS, portals, and optimization
- `get_mcp_docs` — Lists, reads (with pagination & HTML-to-text conversion), and searches all bundled MCP documentation files (`INSTALL.md`, `AGENTS.md`, `README.md`, `docs/*`) directly from the Node server without needing Unity connected
- `configure_cvr_vehicle` — Creates and configures ChilloutVR CCK drivable vehicles (4-wheel chassis rigs, WheelCollider suspension tuning, passenger seats)
- `execute_batch` — Executes multiple MCP Unity operations in a single synchronous Unity frame

### Available apps (current)
- `show_unity_dashboard` — Open the Unity dashboard MCP App in VS Code

### Available resources (current)
- `unity://menu-items` — List of available menu items
- `unity://scenes-hierarchy` — Current scene hierarchy
- `unity://gameobject/{id}` — GameObject details by ID or path
- `unity://logs` — Unity console logs
- `unity://packages` — Installed and available packages
- `unity://assets` — Asset database information
- `unity://tests/{testMode}` — Test Runner test information
- `ui://unity-dashboard` — Unity dashboard MCP App UI

### Available prompts (current)
- `unity_dashboard` — Opens Unity dashboard MCP app with guided information about features
- `gameobject_handling_strategy` — Provides structured workflow for GameObject operations

### Update policy (for agents)
- Update this file when:
  - tools/resources/prompts are added/removed/renamed,
  - config shape or default ports/paths change,
  - the bridge protocol changes (request/response contract).
- Keep it **high-signal**: where to edit code, how to run/build/debug, and the invariants that prevent subtle breakage.
