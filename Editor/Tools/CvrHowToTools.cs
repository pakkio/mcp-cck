using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Recipe definition metadata
    /// </summary>
    public class CckRecipeInfo
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string[] Aliases { get; set; }
        public bool SupportsScaffolding { get; set; }
        public Func<bool, string, JObject> Handler { get; set; }
    }

    /// <summary>
    /// Tool providing ChilloutVR CCK best practices, production recipes, and automatic scaffolding for common VR world/avatar mechanics.
    /// Supports wildcard and prefix searches (e.g. "*", "veh*", "door*", "list").
    /// </summary>
    public class HowtoCckTool : McpToolBase
    {
        private readonly List<CckRecipeInfo> _recipes = new List<CckRecipeInfo>();
        private JObject _activeParameters;

        public HowtoCckTool()
        {
            Name = "howto_cck";
            Description = "Provides expert ChilloutVR CCK recipes, setup instructions (docs.chilloutvr.net/cck/setup/), best practices, and optional GameObject scaffolding for setup, vehicles, doors, elevators, mirrors, pickups, AAS, and optimization. Supports wildcard searches ('*', 'veh*', 'list').";
            IsAsync = false;

            RegisterRecipes();
        }

        private void RegisterRecipes()
        {
            _recipes.Add(new CckRecipeInfo
            {
                Key = "setup",
                Title = "Unity & CCK 4 Setup, Upgrade & Publishing",
                Description = "Official ChilloutVR CCK setup (docs.chilloutvr.net/cck/setup/): recommended Unity version, Built-In RP, clean-import upgrade folders, Master Key sign-in, publishing, and bundle limits.",
                Aliases = new[] { "install", "installation", "cck4", "cck3", "unity_version", "upgrade", "login", "master_key", "publish", "getting_started" },
                SupportsScaffolding = false,
                Handler = HandleSetupTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "vehicles",
                Title = "Drivable Vehicles & Physics Rigging",
                Description = "WheelCollider suspension, spring/damper tuning, low center-of-mass, driver CVRSeat, steering wheel, and CVRVariableBuffer sync.",
                Aliases = new[] { "vehicle", "car", "cars", "drivable", "driving", "automobile" },
                SupportsScaffolding = true,
                Handler = HandleVehicleTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "door",
                Title = "Interactive Doors & Gates",
                Description = "Animator-driven sliding/swinging doors with CVRInteractable triggers and late-joiner multiplayer state sync via CVRVariableBuffer.",
                Aliases = new[] { "doors", "sliding_door", "swing_door", "gate", "gates" },
                SupportsScaffolding = true,
                Handler = HandleDoorTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "mirror",
                Title = "Optimized World & Avatar Mirrors",
                Description = "Performance-tuned CVRMirror setups (Optimized, AvatarOnly, Transparent, Cutout) with layer culling masks.",
                Aliases = new[] { "mirrors", "reflection", "reflections" },
                SupportsScaffolding = true,
                Handler = HandleMirrorTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "elevator",
                Title = "Moving Elevators & Platforms",
                Description = "Multi-floor elevators with Animator states, call buttons, non-trigger floor physics, and avatar parenting volumes.",
                Aliases = new[] { "elevators", "moving_platform", "lift", "lifts", "platform" },
                SupportsScaffolding = false,
                Handler = HandleElevatorTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "pickup",
                Title = "Grabbable Physics Pickups & Weapons",
                Description = "CVRPickupObject with custom grip offsets, continuous physics collision, auto-hold, and throw velocity multipliers.",
                Aliases = new[] { "pickups", "weapon", "weapons", "flashlight", "prop", "props", "grabbable" },
                SupportsScaffolding = true,
                Handler = HandlePickupTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "aas",
                Title = "Advanced Avatar Settings (AAS)",
                Description = "Custom in-game avatar radial menus, toggles, float sliders, sub-menus, color pickers, and Animator parameter linking.",
                Aliases = new[] { "advanced_avatar_settings", "avatar_toggles", "avatar_menu", "puppet", "toggles" },
                SupportsScaffolding = false,
                Handler = HandleAasTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "video_player",
                Title = "Synchronized Video Players",
                Description = "16:9 screen renderers with CVRVideoPlayer, 3D spatial audio attenuation curves, and player controls.",
                Aliases = new[] { "videoplayer", "video", "screen", "tv", "stream" },
                SupportsScaffolding = false,
                Handler = HandleVideoPlayerTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "portals",
                Title = "Portals & Sub-Worlds",
                Description = "World gateways, instance hopping, and sub-world scene loading triggers using CVRPortal.",
                Aliases = new[] { "portal", "subworld", "subworlds", "gateway", "teleporter" },
                SupportsScaffolding = true,
                Handler = HandlePortalTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "optimization",
                Title = "Upload Optimization & Performance Budgets",
                Description = "Polygon limits, draw calls, lightmap baking, light probe grids, and CCK pre-flight checklists.",
                Aliases = new[] { "performance", "audit", "preflight", "checklist", "budget", "stats" },
                SupportsScaffolding = false,
                Handler = HandleOptimizationTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "testing",
                Title = "Local Testing, Test Mode & Benign CCK Build Errors",
                Description = "Distinction between in-Editor Test Mode vs 'Test Your Content Locally' (chilloutvr://), benign Unity PlayerLoop/Build Finished Failure logs, and verifying bundles in Temp/.",
                Aliases = new[] { "test", "test_mode", "local_test", "build_errors", "playerloop", "chilloutvr_protocol", "bundle", "troubleshooting" },
                SupportsScaffolding = false,
                Handler = HandleTestingTopic
            });

            _recipes.Add(new CckRecipeInfo
            {
                Key = "sketchfab",
                Title = "Sketchfab & Blender -> Unity FBX Asset Workflow",
                Description = "Avoid multi-object foliage decimation hangs, verify visual previews before import, normalize scale/base-origin pivot, embed textures, and set Cutout alpha for leaves.",
                Aliases = new[] { "blender", "fbx", "assets", "scenery", "trees", "foliage", "textures", "import" },
                SupportsScaffolding = false,
                Handler = HandleSketchfabTopic
            });
        }

        public override JObject Execute(JObject parameters)
        {
            string query = parameters["topic"]?.ToObject<string>()?.Trim() ?? "*";
            bool scaffold = parameters["scaffold"]?.ToObject<bool?>() ?? false;
            string objectName = parameters["objectName"]?.ToObject<string>();

            // Check if user is asking to list all keys/topics
            if (query == "*" || query == "list" || query == "help" || query == "all" || string.IsNullOrEmpty(query))
            {
                return ReturnTopicList(_recipes, "All available ChilloutVR CCK recipes:");
            }

            // Find matching recipes using wildcard / regex / alias matching
            List<CckRecipeInfo> matches = FindMatchingRecipes(query);

            if (matches.Count == 0)
            {
                return new JObject
                {
                    ["success"] = false,
                    ["type"] = "error",
                    ["message"] = $"No ChilloutVR CCK recipes found matching '{query}'. Use topic='*' or topic='list' to see all available recipes.",
                    ["availableTopics"] = GetTopicSummaryArray(_recipes)
                };
            }

            // If query contains wildcard '*' or multiple matches found and not an exact key match, return list of matches
            if (query.Contains("*") || query.Contains("?") || (matches.Count > 1 && !IsExactKeyMatch(query)))
            {
                return ReturnTopicList(matches, $"Found {matches.Count} recipe(s) matching '{query}':");
            }

            // Exactly one matched recipe -> execute its handler!
            CckRecipeInfo matchedRecipe = matches[0];
            _activeParameters = parameters;
            try
            {
                return matchedRecipe.Handler(scaffold, objectName);
            }
            finally
            {
                _activeParameters = null;
            }
        }

        private List<CckRecipeInfo> FindMatchingRecipes(string query)
        {
            string lowerQuery = query.ToLowerInvariant();
            List<CckRecipeInfo> results = new List<CckRecipeInfo>();

            // Prepare wildcard regex
            string pattern = "^" + Regex.Escape(lowerQuery).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

            foreach (var recipe in _recipes)
            {
                // Check key
                if (regex.IsMatch(recipe.Key) || recipe.Key.StartsWith(lowerQuery.TrimEnd('*')))
                {
                    results.Add(recipe);
                    continue;
                }

                // Check aliases
                bool aliasMatched = false;
                if (recipe.Aliases != null)
                {
                    foreach (var alias in recipe.Aliases)
                    {
                        if (regex.IsMatch(alias) || alias.StartsWith(lowerQuery.TrimEnd('*')))
                        {
                            results.Add(recipe);
                            aliasMatched = true;
                            break;
                        }
                    }
                }

                if (aliasMatched) continue;

                // Check title or description substring if query length >= 3
                if (lowerQuery.Length >= 3 && (recipe.Title.ToLowerInvariant().Contains(lowerQuery) || recipe.Description.ToLowerInvariant().Contains(lowerQuery)))
                {
                    results.Add(recipe);
                }
            }

            return results;
        }

        private bool IsExactKeyMatch(string query)
        {
            string lower = query.ToLowerInvariant();
            foreach (var r in _recipes)
            {
                if (r.Key == lower) return true;
            }
            return false;
        }

        private JObject ReturnTopicList(List<CckRecipeInfo> recipes, string headerMessage)
        {
            JArray topicsArray = new JArray();
            foreach (var r in recipes)
            {
                topicsArray.Add(new JObject
                {
                    ["key"] = r.Key,
                    ["title"] = r.Title,
                    ["description"] = r.Description,
                    ["aliases"] = new JArray(r.Aliases ?? new string[0]),
                    ["supportsScaffolding"] = r.SupportsScaffolding
                });
            }

            string formattedList = $"{headerMessage}\n\n";
            foreach (var r in recipes)
            {
                formattedList += $"- **`{r.Key}`** — {r.Title}\n  {r.Description} *(Scaffold: {(r.SupportsScaffolding ? "Supported" : "Guide only")})*\n";
            }

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = formattedList,
                ["count"] = recipes.Count,
                ["topics"] = topicsArray
            };
        }

        private JArray GetTopicSummaryArray(List<CckRecipeInfo> recipes)
        {
            JArray arr = new JArray();
            foreach (var r in recipes) arr.Add(r.Key);
            return arr;
        }

        // ====================================================================
        // Topic Handlers
        // ====================================================================

        private JObject HandleSetupTopic(bool scaffold, string objectName)
        {
            string cckVersion = _activeParameters?["cckVersion"]?.ToObject<string>() ?? "4.0.1";
            string unityVersion = _activeParameters?["unityVersion"]?.ToObject<string>() ?? "2022.3.58f1";
            string cckDownloadUrl = _activeParameters?["cckDownloadUrl"]?.ToObject<string>() ?? "https://files.chilloutvr.net/cck/CCK_4.0.1_Release.unitypackage";
            string unityHubUrl = _activeParameters?["unityHubInstallUrl"]?.ToObject<string>() ?? "unityhub://2022.3.58f1/ed7f6eacb62e";
            string unityDownloadUrl = _activeParameters?["unityDownloadUrl"]?.ToObject<string>() ?? "https://unity.com/releases/editor/whats-new/2022.3.58f1#installs";
            string infoSource = _activeParameters?["cckInfoSource"]?.ToObject<string>() ?? "default";
            int maxAvatarMb = _activeParameters?["maxAvatarBundleMb"]?.ToObject<int?>() ?? 200;
            int maxSpawnableMb = _activeParameters?["maxSpawnableBundleMb"]?.ToObject<int?>() ?? 200;
            int maxWorldMb = _activeParameters?["maxWorldBundleMb"]?.ToObject<int?>() ?? 1024;

            string sourceNote = infoSource == "live"
                ? "live from https://api.chilloutvr.net/1/public/cck/info"
                : "cached defaults (offline fallback)";

            string guide = $@"### ChilloutVR Unity & CCK Setup Guide (Official docs.chilloutvr.net/cck/setup/)
*(Version data: {sourceNote} | Current Editor: `{Application.unityVersion}`)*

1. **Which CCK Version Should I Use?**
   - **CCK 4 (Release — Recommended):** `v{cckVersion}` using **Unity `{unityVersion}`** (**3D Built-In Render Pipeline** — URP/HDRP are not supported). Modern build panel, content browser, account switcher, reworked build pipeline (no Play Mode required to upload).
   - **CCK 3 (Legacy):** `v3.16.4` using **Unity `2021.3.45f2`**. Development discontinued; use CCK 4 for active projects.
   - **Never mix CCK 3 and CCK 4** in the same project — they conflict. CCK 4 includes all CCK 3 functionality.
   - **Client vs. CCK Version Note:** The ChilloutVR game client (`2026.3.0+`) runs on **Unity `6.3.26f1`** (still Built-In RP), while CCK content creation uses **Unity `{unityVersion}`** until an updated CCK is published via `https://api.chilloutvr.net/1/public/cck/info`.

2. **Downloads & Unity Hub Security Notice**
   - **CCK 4 Package:** {cckDownloadUrl}
   - **Unity Hub Direct Install:** `{unityHubUrl}`
   - **Unity Editor Download:** {unityDownloadUrl}
   - **Unity Security Advisory Note:** Unity Hub may flag `{unityVersion}` under a Unity Security Update Advisory. Per ChilloutVR docs, this only affects player builds and **not** the Unity Editor, so it is safe for creating ChilloutVR content.

3. **Clean Import Required on Every CCK 4 Update**
   - **Back up your project** before updating.
   - Delete all existing CCK folders from `Assets/` before importing the new package:
     - `Assets/ABI.CCK`
     - `Assets/ABI.MODS`
     - `Assets/ABI.QA`
     - `Assets/CVR.CCK`
   - Import the downloaded CCK 4 `.unitypackage` (`Assets > Import Package > Custom Package...`).

4. **Signing In to the CCK**
   - Sign in with your ChilloutVR **username** and **Content Creation Kit Master Key** (*not* your account password).
   - Retrieve your Master Key on the Community Hub (https://hub.chilloutvr.net) under **Profile Settings > Access Key Management**.

5. **Publishing Content & Official Bundle Limits**
   - After uploading, publish from the Community Hub content details page (**Submit for Publication**), reachable via:
     - Community Hub > **My Content** > **Manage**
     - CCK Builder Tab > top-right `▼` utility menu > **Open in Browser**
     - CCK Browser Tab (CCK 4.x+) > **View on Hub**
     - In-game content details page > **Open in Hub**
   - **Official Bundle Size Limits:** Avatars <= **{maxAvatarMb} MB**, Spawnables <= **{maxSpawnableMb} MB**, Worlds <= **{maxWorldMb} MB**.
   - Official Setup Docs: https://docs.chilloutvr.net/cck/setup/ | Discord: https://discord.gg/ChilloutVR";

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "setup",
                ["guide"] = guide,
                ["message"] = guide,
                ["cckInfo"] = new JObject
                {
                    ["cckVersion"] = cckVersion,
                    ["unityVersion"] = unityVersion,
                    ["currentUnityVersion"] = Application.unityVersion,
                    ["cckDownloadUrl"] = cckDownloadUrl,
                    ["unityHubInstallUrl"] = unityHubUrl,
                    ["source"] = infoSource,
                    ["maxAvatarBundleMb"] = maxAvatarMb,
                    ["maxSpawnableBundleMb"] = maxSpawnableMb,
                    ["maxWorldBundleMb"] = maxWorldMb
                }
            };
        }

        private JObject HandleVehicleTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR CCK Vehicle Best Practices & Recipe

1. **Architecture & Constraints**:
   - ChilloutVR does not permit custom C# scripts in uploaded bundles.
   - Drivable vehicles use CCK 4 native vehicle components (`CVRWheelHubController`, `CVRWheelController`) alongside Unity physics (`Rigidbody`, `WheelCollider`), `CVRSeat`, `CVRInteractable`, and `CVRVariableBuffer`.

2. **Chassis & Rigidbody**:
   - Vehicle Root: `Rigidbody` with mass ~1200kg, Drag 0.05, Angular Drag 0.5, Interpolate enabled.
   - Low Center of Mass: Create an empty child `CenterOfMass` at Y = -0.3m to prevent rollovers.

3. **WheelColliders (4 Wheels)**:
   - Front Left / Front Right (Steerable), Rear Left / Rear Right (Motor Driven).
   - Suspension: Spring ~30,000, Damper ~4,500, TargetPosition 0.5, SuspensionDistance 0.2m.
   - Forward & Sideways Friction: Extremum Slip 0.4, Extremum Value 1.0, Asymptote Slip 0.8, Asymptote Value 0.6.

4. **Seating & Controls**:
   - Driver Seat: `CVRSeat` positioned at driver chair.
   - Steering & Throttle: `CVRInteractable` buttons or grabbable `CVRPickupObject` mapped to Animator variables.
   - Network Sync: `CVRVariableBuffer` syncing 'EngineOn', 'Speed', and 'Headlights'.";

            JObject response = new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "vehicles",
                ["guide"] = guide,
                ["message"] = guide
            };

            if (scaffold)
            {
                string name = objectName ?? "CVR_Vehicle_Rig";
                GameObject root = new GameObject(name);
                Rigidbody rb = root.AddComponent<Rigidbody>();
                rb.mass = 1200f;
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.5f;
#else
                rb.drag = 0.05f;
                rb.angularDrag = 0.5f;
#endif
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                // Body visual
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "BodyVisual";
                body.transform.SetParent(root.transform);
                body.transform.localScale = new Vector3(1.8f, 0.8f, 3.5f);
                body.transform.localPosition = new Vector3(0, 0.6f, 0);

                // Driver seat
                GameObject driverSeat = new GameObject("DriverSeat");
                driverSeat.transform.SetParent(root.transform);
                driverSeat.transform.localPosition = new Vector3(-0.45f, 0.5f, 0.1f);
                Type cvrSeatType = FindCckType("CVRSeat");
                if (cvrSeatType != null) Undo.AddComponent(driverSeat, cvrSeatType);

                // 4 Wheels
                Vector3[] wheelOffsets = new[]
                {
                    new Vector3(-0.85f, 0.35f, 1.2f),   // FL
                    new Vector3(0.85f, 0.35f, 1.2f),    // FR
                    new Vector3(-0.85f, 0.35f, -1.2f),  // RL
                    new Vector3(0.85f, 0.35f, -1.2f)   // RR
                };
                string[] wheelNames = new[] { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };

                for (int i = 0; i < 4; i++)
                {
                    GameObject wheelGo = new GameObject(wheelNames[i]);
                    wheelGo.transform.SetParent(root.transform);
                    wheelGo.transform.localPosition = wheelOffsets[i];
                    WheelCollider wc = wheelGo.AddComponent<WheelCollider>();
                    wc.radius = 0.35f;
                    wc.suspensionDistance = 0.2f;
                    JointSpring js = wc.suspensionSpring;
                    js.spring = 30000f;
                    js.damper = 4500f;
                    js.targetPosition = 0.5f;
                    wc.suspensionSpring = js;
                }

                Undo.RegisterCreatedObjectUndo(root, "Scaffold CVR Vehicle Rig");
                response["scaffoldedObject"] = root.name;
                response["instanceId"] = UnityObjectId.GetObjectId(root);
            }

            return response;
        }

        private JObject HandleDoorTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR CCK Interactive Door Best Practices & Recipe

1. **Mechanic Design**:
   - Doors in ChilloutVR should be driven by an `Animator` controller (states: 'Closed', 'Opening', 'Open', 'Closing') with a boolean parameter 'IsOpen'.
   - Triggering is handled by `CVRInteractable` attached to the door handle or a proximity trigger collider.

2. **Components Setup**:
   - Door Frame / Anchor: Root GameObject with `Animator`.
   - Door Leaf: Rotating or sliding child object.
   - Handle / Button: Has a `BoxCollider` and `CVRInteractable` configured with interactionType 'Interact' and action 'SetAnimatorParameter' (parameter: 'IsOpen').

3. **Multiplayer State Sync**:
   - Add `CVRVariableBuffer` to the root with boolean variable 'IsOpen' to ensure players who join late see the door in the correct synchronized state.";

            JObject response = new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "door",
                ["guide"] = guide,
                ["message"] = guide
            };

            if (scaffold)
            {
                string name = objectName ?? "CVR_Interactive_Door";
                GameObject root = new GameObject(name);

                // Frame
                GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frame.name = "DoorFrame";
                frame.transform.SetParent(root.transform);
                frame.transform.localScale = new Vector3(1.2f, 2.2f, 0.1f);
                frame.transform.localPosition = new Vector3(0, 1.1f, 0);

                // Hinge Pivot
                GameObject hinge = new GameObject("HingePivot");
                hinge.transform.SetParent(root.transform);
                hinge.transform.localPosition = new Vector3(-0.55f, 0, 0);

                // Door Panel
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "DoorPanel";
                panel.transform.SetParent(hinge.transform);
                panel.transform.localScale = new Vector3(1.0f, 2.1f, 0.06f);
                panel.transform.localPosition = new Vector3(0.5f, 1.05f, 0);

                // Handle with CVRInteractable
                GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handle.name = "DoorHandle";
                handle.transform.SetParent(panel.transform);
                handle.transform.localScale = new Vector3(0.08f, 0.15f, 0.15f);
                handle.transform.localPosition = new Vector3(0.4f, 0f, 0);

                Type cvrInteractableType = FindCckType("CVRInteractable");
                if (cvrInteractableType != null) Undo.AddComponent(handle, cvrInteractableType);

                Undo.RegisterCreatedObjectUndo(root, "Scaffold CVR Interactive Door");
                response["scaffoldedObject"] = root.name;
                response["instanceId"] = UnityObjectId.GetObjectId(root);
            }

            return response;
        }

        private JObject HandleMirrorTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR CCK Mirror Best Practices & Optimization

1. **Mirror Types**:
   - `Optimized`: Renders avatars and local world without heavy post-processing. Best default for worlds.
   - `AvatarOnly`: Discards all static world geometry, drastically reducing draw calls. Ideal for dance floors and social hubs.
   - `Transparent` / `Cutout`: Renders alpha materials in reflection.
   - `Full`: Heavy performance cost. Use sparingly.

2. **Layer Masking**:
   - Exclude UI and Post-Processing layers from mirror culling mask.
   - Turn off mirror when player is far away using distance culling or `CVRDistanceLod`.

3. **Mirror Toggle Button**:
   - Pair with a `CVRInteractable` button near the mirror to let players toggle it on/off.";

            JObject response = new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "mirror",
                ["guide"] = guide,
                ["message"] = guide
            };

            if (scaffold)
            {
                string name = objectName ?? "CVR_Optimized_Mirror";
                GameObject mirrorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mirrorQuad.name = name;
                mirrorQuad.transform.localScale = new Vector3(3.0f, 2.0f, 1.0f);
                mirrorQuad.transform.position = new Vector3(0, 1.5f, 2.0f);
                mirrorQuad.transform.rotation = Quaternion.Euler(0, 180f, 0);

                Type cvrMirrorType = FindCckType("CVRMirror");
                if (cvrMirrorType != null) Undo.AddComponent(mirrorQuad, cvrMirrorType);

                Undo.RegisterCreatedObjectUndo(mirrorQuad, "Scaffold CVR Optimized Mirror");
                response["scaffoldedObject"] = mirrorQuad.name;
                response["instanceId"] = UnityObjectId.GetObjectId(mirrorQuad);
            }

            return response;
        }

        private JObject HandleElevatorTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR CCK Moving Elevator Best Practices

1. **Parenting & Movement**:
   - Use an `Animator` on the Elevator Root containing animation clips for each floor ('Floor1', 'Floor2').
   - Floor Call Buttons: Have `CVRInteractable` set Animator triggers ('GoToFloor1', 'GoToFloor2').
2. **Physics & Player Riding**:
   - Elevator platform must have a solid `BoxCollider` (non-trigger) on the floor.
   - Add a trigger volume slightly above the floor with a parenting script or trigger to keep avatars locked to the moving platform smoothly.";

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "elevator",
                ["guide"] = guide,
                ["message"] = guide
            };
        }

        private JObject HandlePickupTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR CCK Grabbable Pickups & Weapons Recipe

1. **Requirements**:
   - `Rigidbody`: mass ~0.5–2kg, collision detection Continuous, Interpolate enabled.
   - `Collider`: Box or Capsule for hand grab detection.
   - `CVRPickupObject`: Configured with gripType 'Origin' or 'CustomGrip'.
2. **Custom Grip**:
   - Create a child empty GameObject named 'Grip' rotated so the object points forward when held in VR hand.
3. **Throw Velocity**:
   - Set `throwVelocityMultiplier` to 1.0–1.2 for natural physics throwing.";

            JObject response = new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "pickup",
                ["guide"] = guide,
                ["message"] = guide
            };

            if (scaffold)
            {
                string name = objectName ?? "CVR_Physics_Pickup";
                GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                root.name = name;
                root.transform.localScale = new Vector3(0.06f, 0.25f, 0.06f);

                Rigidbody rb = root.AddComponent<Rigidbody>();
                rb.mass = 1.0f;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                Type cvrPickupType = FindCckType("CVRPickupObject");
                if (cvrPickupType != null) Undo.AddComponent(root, cvrPickupType);

                Undo.RegisterCreatedObjectUndo(root, "Scaffold CVR Pickup");
                response["scaffoldedObject"] = root.name;
                response["instanceId"] = UnityObjectId.GetObjectId(root);
            }

            return response;
        }

        private JObject HandleAasTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR Advanced Avatar Settings (AAS) Best Practices

1. **Structure**:
   - `CVRAvatar` on avatar root.
   - `CVRAdvancedAvatarSettings` holds list of custom menu entries.
2. **Control Types**:
   - `Toggle`: Boolean (0 or 1). Use for clothing/props on/off.
   - `Slider / Radial`: Float (0.0 to 1.0). Use for size, blendshapes, or emission brightness.
   - `ColorPicker`: Vector4 (RGBA). Drives shader color parameters.
   - `Submenu`: Organizes options into nested wheels.";

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "aas",
                ["guide"] = guide,
                ["message"] = guide
            };
        }

        private JObject HandleVideoPlayerTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR Video Player Best Practices

1. **Screen Quad**: Create a 16:9 Quad with `MeshRenderer`.
2. **Audio Routing**: Use an `AudioSource` with 3D spatial blend and distance attenuation (max distance ~20m) so it does not drown out the entire world.
3. **Component**: Add `CVRVideoPlayer` and assign the screen renderer and audio source.";

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "video_player",
                ["guide"] = guide,
                ["message"] = guide
            };
        }

        private JObject HandlePortalTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR Portal & Sub-World Best Practices

1. **Portal Structure**:
   - Door frame or arch with a `BoxCollider` trigger.
   - `CVRPortal` component assigned with `worldId` and optional `instanceId`.
2. **Instance Hopping**:
   - Link worlds together in a hub or gateway zone.";

            JObject response = new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "portals",
                ["guide"] = guide,
                ["message"] = guide
            };

            if (scaffold)
            {
                string name = objectName ?? "CVR_Portal_Frame";
                GameObject portalGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                portalGo.name = name;
                portalGo.transform.localScale = new Vector3(1.4f, 2.4f, 0.2f);

                Type cvrPortalType = FindCckType("CVRPortal");
                if (cvrPortalType != null) Undo.AddComponent(portalGo, cvrPortalType);

                Undo.RegisterCreatedObjectUndo(portalGo, "Scaffold CVR Portal");
                response["scaffoldedObject"] = portalGo.name;
                response["instanceId"] = UnityObjectId.GetObjectId(portalGo);
            }

            return response;
        }

        private JObject HandleOptimizationTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR Pre-Upload Optimization Checklist

1. **World Budgets**:
   - Triangles: Aim under 500,000 for standard worlds (under 1,500,000 for complex worlds).
   - Draw Calls: Aim under 150 (use static batching & texture atlasing).
   - Realtime Lights: Maximum 2-4 realtime lights; bake all static lighting!
   - Light Probes: Place 3D LightProbeGroup grids so dynamic avatars and pickups receive proper lighting.

2. **Avatar Budgets**:
   - Triangles: Under 70,000 for Good rating.
   - Skinned Meshes: Under 4.
   - Material Slots: Under 8-12.
   - Texture Memory: Under 150MB VRAM.";

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "optimization",
                ["guide"] = guide,
                ["message"] = guide
            };
        }

        private JObject HandleTestingTopic(bool scaffold, string objectName)
        {
            string guide = @"### ChilloutVR CCK Testing, Local Builds & Benign Build Errors

1. **Two Different Test Modes (Do Not Confuse Them)**:
   - **In-Editor Test Mode (`manage_cck_test_mode`)**: Same as CCK Control Panel's 'Test in PlayMode'. Spawns a walkable player inside the Unity Editor. Does **not** require ChilloutVR installed on the machine.
   - **Test Your Content Locally (CCK Panel)**: Builds a real `.cvrworld` / `.cvravatar` / `.cvrprop` bundle into the project's `Temp/` folder and launches the external ChilloutVR client via the `chilloutvr://` URL protocol.
   - **Prerequisite for 'Test Your Content Locally'**: ChilloutVR must be installed via Steam and **launched at least once** so Windows registers the `chilloutvr://` protocol handler. Otherwise the build completes, restores the scene, and stops when trying to invoke `chilloutvr://`.

2. **Known Benign Unity/CCK Build Errors (NOT Scene Errors)**:
   - `PlayerLoop internal function has been called recursively`
     - Happens because CCK invokes `BuildAssetBundles` from async code while Unity is inside its PlayerLoop.
   - `Build Finished, Result: Failure`
     - Logged by Unity solely because the PlayerLoop error was emitted during the build — **CCK continues and still creates the bundle cleanly**.
   - **Do NOT waste time debugging your scene** over these two messages alone.

3. **How to Verify a CCK Build Actually Succeeded**:
   - Check the Unity project's `Temp/` folder for the generated bundle (e.g., `cvrworld_<id>_<hash>.cvrworld`, `.cvravatar`, or `.cvrprop`) and confirm a non-zero file size.
   - Confirm in `Editor.log` that only allowed CCK/Unity components (e.g. `CVRWorld`, `CVRWheelHubController`, `CVRWheelController`, `CVRInteractable`) are bundled and no custom user C# scripts were stripped or failed.";

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "testing",
                ["guide"] = guide,
                ["message"] = guide
            };
        }

        private JObject HandleSketchfabTopic(bool scaffold, string objectName)
        {
            string guide = @"### Sketchfab & Blender -> Unity FBX Asset Workflow (Pitfalls to Avoid)

1. **Inspect Preview, Object Count & Triangle Count BEFORE Importing**:
   - **Always inspect the Sketchfab preview thumbnail** before downloading — asset names like 'Tree' are often leafless/dead trees.
   - **Prefer single-mesh models (~2k–10k triangles)** for scenery/props.
   - **NEVER run per-object decimation/simplification on multi-object foliage** (trees split into thousands of 8-vertex leaf objects will hang Blender for 10+ minutes and destroy leaves). Disable auto-decimation on already-low-poly (<10k tris) or multi-piece foliage; if decimation is genuinely needed, join meshes first.

2. **Normalize in Blender BEFORE Exporting FBX**:
   - Scale the asset to realistic world meters (raw Sketchfab assets frequently import 10x–30x too large).
   - Move the object pivot/base to the world origin (`Z = 0` in Blender) and apply scale/rotation transforms (`Ctrl+A`).

3. **Textures & Foliage Materials in Unity**:
   - Export FBX with `path_mode='COPY'` and `embed_textures=True` (otherwise FBX arrives in Unity without textures), OR explicitly copy texture maps into `Assets/Materials/` and assign them.
   - For leaves/hedges with alpha transparency, configure Unity Built-In `Standard` materials with **Cutout** rendering mode (`_Mode = 1`) so foliage cards render cleanly.
   - Verify placement after `add_asset_to_scene` (it returns the final world `position`).

4. **Licensing**:
   - Track attribution credits when keeping **CC Attribution (CC-BY)** Sketchfab models in published ChilloutVR worlds.";

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["topic"] = "sketchfab",
                ["guide"] = guide,
                ["message"] = guide
            };
        }

        private static Type FindCckType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = assembly.GetType($"ABI.CCK.Components.{typeName}")
                          ?? assembly.GetType($"ABI.CCK.Scripts.{typeName}")
                          ?? assembly.GetType(typeName);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
