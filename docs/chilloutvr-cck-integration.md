# ChilloutVR CCK Integration Guide

This guide explains how to use **MCP Unity** to build, configure, script, scaffold, and optimize content for **ChilloutVR** using the **ABI Content Creation Kit (CCK)**.

---

## 1. Overview & Architecture

ChilloutVR (by Alpha Blend Interactive) uses Unity to create worlds, avatars, and spawnable props. For security and cross-platform safety, ChilloutVR does not permit arbitrary custom C# scripts in uploaded AssetBundles. Instead, creators rely on the CCK component ecosystem:

- **World Roots**: `CVRWorld`, `CVRSpawnPoint`, `CVRMirror`, `CVRSeat`, `CVRPortal`.
- **Interactivity & State**: `CVRInteractable`, `CVRPickupObject`, `CVRVariableBuffer`.
- **Avatars**: `CVRAvatar`, `CVRAdvancedAvatarSettings` (AAS), `CVRFaceTracking`.
- **Physics & Locomotion**: Standard Unity `Rigidbody`, `WheelCollider`, and CCK seats.

### Unity Version & Official CCK Setup (`docs.chilloutvr.net/cck/setup/`)
> [!IMPORTANT]
> **Official ChilloutVR CCK 4 Version**: **`CCK 4.0.1`** → **`Unity 2022.3.58f1`** (**3D Built-In Render Pipeline** — URP/HDRP are not supported)
>
> **Client vs. CCK Version Note**: While the ChilloutVR game client (`2026.3.0+`) runs on **Unity `6.3.26f1`** (still Built-In RP), content creation and uploading via CCK 4 still targets **`Unity 2022.3.58f1`** until an updated CCK is published. MCP Unity queries `https://api.chilloutvr.net/1/public/cck/info` dynamically (with offline fallback) in `howto_cck(topic="setup")` and `inspect_cvr_cck(action="check_setup")` so version checks stay current automatically.

Key rules from `https://docs.chilloutvr.net/cck/setup/`:
- **CCK 4 (Release) vs. CCK 3 (Legacy)**: Use **CCK 4** (`Unity 2022.3.58f1`) for all active projects. **Never import both CCK 3 and CCK 4** into the same project — they conflict.
- **Clean Import Required on Every CCK 4 Update**: Back up your project, delete `Assets/ABI.CCK`, `Assets/ABI.MODS`, `Assets/ABI.QA`, and `Assets/CVR.CCK`, then import the new `.unitypackage`.
- **Unity Hub Security Advisory**: Unity Hub may flag `2022.3.58f1` under a Unity Security Update Advisory. Per ChilloutVR docs, this only affects player builds and **not** the Unity Editor, so it is safe for creating ChilloutVR content.
- **Signing In**: Use your ChilloutVR **username** and **CCK Master Key** (*not* your account password) from Community Hub (`https://hub.chilloutvr.net`) → **Profile Settings > Access Key Management**.
- **Official Bundle Size Limits**: Avatars <= **200 MB**, Spawnables <= **200 MB**, Worlds <= **1024 MB (1 GB)**.

MCP Unity provides soft-linked tools that interact natively with CCK components when installed, and provide clean fallbacks and template scaffolding if CCK is not yet imported.

---

## 2. Dedicated CCK Tools

### 1. `manage_cvr_world`
Configures ChilloutVR world settings and environmental fixtures.

- **`setup_world`**: Creates the `CVRWorld` root GameObject with:
  - `respawnHeight` (e.g. `-50.0` meters)
  - `runSpeed`, `sprintMultiplier`, `jumpHeight`
  - `allowFlight`, `allowTeleport`
  - `gravity` vector
- **`add_spawn_point`**: Spawns and aligns `CVRSpawnPoint` objects and links them to the active world.
- **`create_mirror`**: Spawns an optimized `CVRMirror` with configurable culling masks (`Optimized`, `AvatarOnly`, `Transparent`, `Cutout`, `Full`).
- **`create_seat`**: Creates or converts any GameObject into an interactive sitting `CVRSeat`.
- **`create_portal`**: Creates a `CVRPortal` gateway linking to a target `worldId` and `instanceId`.

**Example:**
```json
{
  "method": "manage_cvr_world",
  "params": {
    "action": "setup_world",
    "respawnHeight": -50,
    "runSpeed": 4.5,
    "allowFlight": true
  }
}
```

---

### 2. `configure_cvr_interactivity`
Wires up interactive triggers, physics pickups, and multiplayer networked variable synchronization.

- **`add_interactable`**: Adds `CVRInteractable` with trigger mechanisms (`interact`, `grab`, `touch`, `look_at`, `area_trigger`) and action handlers (`toggle_gameobject`, `teleport_player`, `play_audio`, `set_animator_param`, `spawn_prefab`).
- **`configure_pickup`**: Attaches `CVRPickupObject` with custom grip points, auto-hold toggles, and throw velocity multipliers.
- **`setup_variable_buffer`**: Creates `CVRVariableBuffer` network synchronized variables (`bool`, `float`, `int`, `string`) for multiplayer logic.

**Example:**
```json
{
  "method": "configure_cvr_interactivity",
  "params": {
    "action": "add_interactable",
    "objectPath": "LightSwitch",
    "interactionType": "interact",
    "actionType": "toggle_gameobject",
    "targetObjectPath": "CeilingLight"
  }
}
```

---

### 3. `manage_cvr_avatar`
Automates avatar setup, viewpoint calculations, lip-sync, and AAS menus.

- **`setup_avatar`**:
  - Automatically calculates exact eye viewpoint (`viewPosition`) and mouth voice position from Humanoid `Animator` bones or mesh bounds.
  - Detects face meshes and connects 15 Oculus visemes and eye blink blendshapes.
- **`configure_aas`**: Configures Advanced Avatar Settings (AAS) radial wheels, toggles, float sliders, sub-menus, and color pickers.
- **`setup_face_tracking`**: Binds unified face tracking blendshapes to `CVRFaceTracking`.

**Example:**
```json
{
  "method": "manage_cvr_avatar",
  "params": {
    "action": "setup_avatar",
    "objectPath": "RobotAvatar"
  }
}
```

---

### 4. `configure_cvr_vehicle`
Creates and tunes drivable 4-wheel vehicles and passenger seating.

- **`create_car_rig`**:
  - Creates a `Rigidbody` (mass 1200kg, interpolate, dynamic continuous collision).
  - Sets an offset low center of mass (`CenterOfMass` at Y = -0.35m) to prevent rollovers.
  - Adds 4 `WheelCollider`s with tuned spring/damper suspension.
  - Sets up driver `CVRSeat`, steering wheel with `CVRInteractable`, headlights, engine `AudioSource`, and `CVRVariableBuffer`.
- **`configure_suspension`**: Fine-tunes spring forces, damper rates, suspension travel distance, and tire friction curves.
- **`add_passenger_seats`**: Spawns and aligns passenger `CVRSeat` components (up to 8 seats).

**Example:**
```json
{
  "method": "configure_cvr_vehicle",
  "params": {
    "action": "create_car_rig",
    "vehicleName": "DuneBuggy",
    "mass": 1400,
    "spring": 35000,
    "damper": 5000
  }
}
```

---

### 5. `inspect_cvr_cck` (Pre-Flight Validation, Setup Audit & Stats)
Performs automated project setup checks (`check_setup`) and pre-upload content validation (`validate_content` / `get_stats`) against ChilloutVR limits:

- **`check_setup`**:
  - Compares the active Unity Editor version against the live official CCK version (`https://api.chilloutvr.net/1/public/cck/info`).
  - Verifies **Built-In Render Pipeline** (flags URP/HDRP as errors) and checks Color Space (`Linear` recommended).
  - Detects installed CCK track (`cck4`, `cck3_legacy`, or `not_installed`) and inspects `Assets/` CCK folders (`ABI.CCK`, `ABI.MODS`, `ABI.QA`, `CVR.CCK`).
- **`validate_content` / `get_stats`**:
  - Verifies required root components (`CVRWorld` or `CVRAvatar`).
  - Checks polygon/triangle budgets:
    - Worlds: Recommended under 500,000–1,500,000 triangles.
    - Avatars: Recommended under 70,000 triangles for Good rating.
  - Audits SkinnedMeshRenderer counts and material slot counts.
  - Verifies AudioSource 3D spatialization (prevents 2D audio world flooding).
  - Returns a structured `PASS`, `WARNING`, or `ERROR` report with actionable optimization advice.

**Example:**
```json
{
  "method": "inspect_cvr_cck",
  "params": {
    "action": "check_setup"
  }
}
```

---

### 6. `howto_cck` (Setup Guide, Recipes & Scaffolding)
Provides official setup instructions, production recipes, and instant GameObject hierarchy scaffolding.

- Supports wildcard and topic searches (`*`, `list`, `setup`, `veh*`, `door*`, `mirror*`, `opt*`).
- `topic: "setup"` pulls live CCK version/download/limit info from `https://api.chilloutvr.net/1/public/cck/info` (and even works if Unity is not yet connected).
- Generates step-by-step best practices.
- When `scaffold: true` is set, creates and wires up the ready-to-use GameObject structure directly in your Unity scene.

**Supported Topics**:
- `setup`: Official Unity & CCK 4 setup (`docs.chilloutvr.net/cck/setup/`), clean-import upgrade folders, Master Key auth, publishing, and bundle size limits.
- `vehicles`: Drivable cars, suspension math, low center of mass, CVRSeat, steering wheel.
- `door`: Interactive sliding/swinging doors with `Animator`, `CVRInteractable`, and multiplayer sync.
- `mirror`: Performance-tuned `CVRMirror` modes and layer culling masks.
- `elevator`: Moving platforms, floor state animators, and avatar parenting.
- `pickup`: Physics props, weapons, custom grips, and throw velocity.
- `aas`: Advanced Avatar Settings radial menus, sliders, and color pickers.
- `video_player`: 16:9 screen quads with 3D audio attenuation.
- `portals`: Gateway hubs and sub-world loading.
- `optimization`: Pre-upload checklists for draw calls, lightmap baking, and probe grids.
- `testing`: Distinction between `manage_cck_test_mode` vs "Test Your Content Locally" (`chilloutvr://`), known benign `PlayerLoop` / `Build Finished, Result: Failure` build messages, and verifying `.cvrworld` bundles in `Temp/`.
- `sketchfab`: Sketchfab & Blender → Unity FBX workflow (preview checks, avoiding multi-object foliage decimation hangs, scale/pivot normalization, embedded textures, Cutout alpha leaves, CC-BY attribution).

---

## 3. Synergy with Core MCP Unity Tools

When building for ChilloutVR, combine CCK tools with:
- **`probuilder_create_shape`**: Greybox world geometry, rooms, ramps, and obstacles.
- **`manage_terrain`**: Sculpt world terrain with Perlin noise, textures, and foliage.
- **`manage_lighting` & `configure_light_probe_group`**: Bake static illumination and generate automated 3D light probe grids for dynamic avatar lighting.
- **`configure_colliders`**: Generate optimized convex mesh colliders for world floors and physics props.
- **`configure_texture_settings`**: Optimize texture memory, max sizes, and crunch compression before upload.

---

## 4. Field-Tested Troubleshooting & Workflow Gotchas

### Local Testing vs. In-Editor Test Mode & Benign CCK Build Errors
- **Two test modes**:
  - `manage_cck_test_mode` runs CCK's **in-Editor PlayMode** test (spawns a walkable player inside Unity; does **not** require ChilloutVR installed).
  - CCK **"Test Your Content Locally"** builds a bundle in `Temp/` and launches the external ChilloutVR client via the `chilloutvr://` protocol — this requires **ChilloutVR installed via Steam and launched at least once** so Windows registers `chilloutvr://`.
- **Known benign Unity/CCK build errors**:
  - `"PlayerLoop internal function has been called recursively"` and `"Build Finished, Result: Failure"` occur because CCK invokes `BuildAssetBundles` from async code inside Unity's PlayerLoop. **They are NOT scene errors** — the bundle (`Temp/cvrworld_*.cvrworld`, `.cvravatar`, `.cvrprop`) is still produced cleanly. Always verify the generated file in `Temp/` instead of treating those two lines as a scene failure.

### Sketchfab & Blender → Unity Asset Imports
- **Check preview thumbnails & object counts first**: Don't pick models blindly by name (many `"Tree"` assets are leafless/dead trees) and **never run per-object decimation on multi-object foliage** (a tree made of thousands of 8-vertex leaf pieces hangs Blender for 10+ minutes and ruins the leaves). Prefer single-mesh assets (`~2k–10k` tris) without decimation, or join meshes before decimating.
- **Normalize scale, pivot & textures before FBX export**: Scale to realistic meters, move base pivot to origin (`Z = 0`), export with `path_mode='COPY', embed_textures=True` (or copy textures into `Assets/Materials/`), and use Unity Built-In `Standard` **Cutout** mode for alpha foliage cards.
- **CC Attribution**: Keep creator credit notes when using CC-BY Sketchfab models.

### Package Installs & Asset Placement
- **`add_package` domain reload**: Installing UPM packages (e.g. ProBuilder) triggers a Unity script recompile and a brief WebSocket drop/reconnect; this is normal and preserves unsaved scene edits.
- **`add_asset_to_scene` world position**: Parenting now uses `worldPositionStays = true` and applies world `position` after parenting (supporting both `{x, y, z}` and `[x, y, z]`), returning the final placed coordinates.
