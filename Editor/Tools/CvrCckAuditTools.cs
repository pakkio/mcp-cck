using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for auditing, validating, and profiling ChilloutVR CCK Worlds, Avatars, and project setup before upload.
    /// </summary>
    public class InspectCvrCckTool : McpToolBase
    {
        public InspectCvrCckTool()
        {
            Name = "inspect_cvr_cck";
            Description = "Audits and validates ChilloutVR CCK content (Worlds, Avatars, Props) and project setup (Unity version, Built-In Render Pipeline, CCK installation, folder hygiene) for upload readiness and performance budgets.";
            IsAsync = false;
        }

        public override JObject Execute(JObject parameters)
        {
            string action = parameters["action"]?.ToObject<string>()?.ToLowerInvariant() ?? "validate_content";
            string contentType = parameters["contentType"]?.ToObject<string>()?.ToLowerInvariant() ?? "world";
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();

            switch (action)
            {
                case "validate_content":
                    return HandleValidateContent(contentType, instanceId, objectPath);

                case "get_stats":
                    return HandleGetStats(contentType, instanceId, objectPath);

                case "check_setup":
                    return HandleCheckSetup(parameters);

                default:
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Unknown action '{action}'. Supported actions: 'validate_content', 'get_stats', 'check_setup'",
                        "invalid_action"
                    );
            }
        }

        private JObject HandleValidateContent(string contentType, int? instanceId, string objectPath)
        {
            GameObject targetGo = null;
            if (instanceId.HasValue || !string.IsNullOrEmpty(objectPath))
            {
                JObject findErr = GameObjectToolUtils.FindGameObject(instanceId, objectPath, out targetGo, out string info);
                if (findErr != null) return findErr;
            }

            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            List<string> suggestions = new List<string>();

            int totalPolygons = 0;
            int materialCount = 0;
            int skinnedMeshCount = 0;
            int audioSourceCount = 0;
            int lightCount = 0;
            int particleCount = 0;

            if (contentType == "avatar")
            {
                if (targetGo == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse("Target avatar GameObject ('instanceId' or 'objectPath') must be provided for avatar validation.", "validation_error");
                }

                // Check CVRAvatar or Animator
                Type cvrAvatarType = FindCckType("CVRAvatar");
                if (cvrAvatarType != null && targetGo.GetComponent(cvrAvatarType) == null)
                {
                    errors.Add("Missing 'CVRAvatar' component on avatar root.");
                }

                Animator anim = targetGo.GetComponent<Animator>();
                if (anim == null || !anim.isHuman)
                {
                    warnings.Add("Avatar is not configured with a Humanoid Animator.");
                }

                // Count Renderers on Avatar
                SkinnedMeshRenderer[] smrs = targetGo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                skinnedMeshCount = smrs.Length;
                if (skinnedMeshCount > 8)
                {
                    warnings.Add($"High SkinnedMeshRenderer count ({skinnedMeshCount}). Consider combining meshes to reduce draw calls.");
                }

                foreach (var smr in smrs)
                {
                    if (smr.sharedMesh != null) totalPolygons += smr.sharedMesh.triangles.Length / 3;
                    if (smr.sharedMaterials != null) materialCount += smr.sharedMaterials.Length;
                }

                MeshFilter[] mfs = targetGo.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfs)
                {
                    if (mf.sharedMesh != null) totalPolygons += mf.sharedMesh.triangles.Length / 3;
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterials != null) materialCount += mr.sharedMaterials.Length;
                }

                if (totalPolygons > 100000)
                {
                    warnings.Add($"High polygon count ({totalPolygons:N0} triangles). Target under 70,000 for Good rating.");
                }

                if (materialCount > 16)
                {
                    warnings.Add($"High material slot count ({materialCount}). Consider atlasing textures to reduce material slots.");
                }

                AudioSource[] audios = targetGo.GetComponentsInChildren<AudioSource>(true);
                audioSourceCount = audios.Length;
                foreach (var a in audios)
                {
                    if (a.spatialize == false || a.spatialBlend < 0.8f)
                    {
                        warnings.Add($"AudioSource on '{a.gameObject.name}' is not fully spatialized (spatialBlend={a.spatialBlend}).");
                    }
                }
            }
            else // World validation
            {
                Type cvrWorldType = FindCckType("CVRWorld");
                UnityEngine.Object[] worlds = cvrWorldType != null ? UnityEngine.Object.FindObjectsByType(cvrWorldType, FindObjectsSortMode.None) : new UnityEngine.Object[0];

                if (worlds.Length == 0)
                {
                    warnings.Add("No 'CVRWorld' component found in the active scene.");
                }
                else if (worlds.Length > 1)
                {
                    errors.Add($"Multiple ({worlds.Length}) 'CVRWorld' components found in scene. Only one is allowed.");
                }

                // Check Spawn points
                Type spawnType = FindCckType("CVRSpawnPoint");
                UnityEngine.Object[] spawns = spawnType != null ? UnityEngine.Object.FindObjectsByType(spawnType, FindObjectsSortMode.None) : new UnityEngine.Object[0];
                if (spawns.Length == 0 && GameObject.Find("SpawnPoint") == null)
                {
                    warnings.Add("No spawn point found in the scene.");
                }

                // Check Lights
                Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                lightCount = lights.Length;
                int realtimeLights = 0;
                foreach (var l in lights)
                {
                    if (l.lightmapBakeType == LightmapBakeType.Realtime) realtimeLights++;
                }

                if (realtimeLights > 4)
                {
                    warnings.Add($"High realtime light count ({realtimeLights}). Bake lighting to improve performance.");
                }

                // Count scene meshes
                MeshFilter[] mfs = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
                foreach (var mf in mfs)
                {
                    if (mf.sharedMesh != null) totalPolygons += mf.sharedMesh.triangles.Length / 3;
                    MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                    if (mr != null && mr.sharedMaterials != null) materialCount += mr.sharedMaterials.Length;
                }

                ParticleSystem[] particles = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                particleCount = particles.Length;

                AudioSource[] audios = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                audioSourceCount = audios.Length;
            }

            string status = errors.Count > 0 ? "error" : (warnings.Count > 0 ? "warning" : "pass");

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Validation result: {status.ToUpper()} ({errors.Count} errors, {warnings.Count} warnings)",
                ["status"] = status,
                ["contentType"] = contentType,
                ["errors"] = new JArray(errors),
                ["warnings"] = new JArray(warnings),
                ["suggestions"] = new JArray(suggestions),
                ["metrics"] = new JObject
                {
                    ["totalTriangles"] = totalPolygons,
                    ["materialSlots"] = materialCount,
                    ["skinnedMeshRenderers"] = skinnedMeshCount,
                    ["audioSources"] = audioSourceCount,
                    ["lights"] = lightCount,
                    ["particleSystems"] = particleCount
                }
            };
        }

        private JObject HandleGetStats(string contentType, int? instanceId, string objectPath)
        {
            return HandleValidateContent(contentType, instanceId, objectPath);
        }

        private JObject HandleCheckSetup(JObject parameters)
        {
            string expectedUnityVersion = parameters["expectedUnityVersion"]?.ToObject<string>() ?? "2022.3.58f1";
            string expectedCckVersion = parameters["expectedCckVersion"]?.ToObject<string>() ?? "4.0.1";
            string cckInfoSource = parameters["cckInfoSource"]?.ToObject<string>() ?? "default";
            int maxAvatarMb = parameters["maxAvatarBundleMb"]?.ToObject<int?>() ?? 200;
            int maxSpawnableMb = parameters["maxSpawnableBundleMb"]?.ToObject<int?>() ?? 200;
            int maxWorldMb = parameters["maxWorldBundleMb"]?.ToObject<int?>() ?? 1024;

            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            List<string> suggestions = new List<string>();

            // 1. Unity version check
            string currentUnityVersion = Application.unityVersion;
            bool exactUnityMatch = string.Equals(currentUnityVersion, expectedUnityVersion, StringComparison.OrdinalIgnoreCase);
            string currentMajorMinor = GetMajorMinor(currentUnityVersion);
            string expectedMajorMinor = GetMajorMinor(expectedUnityVersion);

            if (!exactUnityMatch)
            {
                if (string.Equals(currentMajorMinor, expectedMajorMinor, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Unity Editor is '{currentUnityVersion}', while official CCK {expectedCckVersion} recommends '{expectedUnityVersion}'.");
                }
                else if (currentUnityVersion.StartsWith("2021.3", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Unity Editor is '{currentUnityVersion}' (CCK 3 Legacy track). Official CCK 4 ({expectedCckVersion}) requires Unity '{expectedUnityVersion}'.");
                }
                else
                {
                    errors.Add($"Unsupported Unity Editor version '{currentUnityVersion}' for CCK {expectedCckVersion}. Official CCK 4 requires Unity '{expectedUnityVersion}' (note: while the ChilloutVR client runs Unity 6.3, CCK uploads still require the official CCK Unity version from https://docs.chilloutvr.net/cck/setup/).");
                }
            }

            // 2. Render pipeline check (Built-In RP required)
            RenderPipelineAsset currentRp = GraphicsSettings.currentRenderPipeline;
            bool isBuiltInRp = currentRp == null;
            string renderPipelineName = isBuiltInRp ? "Built-In Render Pipeline" : currentRp.GetType().Name;
            if (!isBuiltInRp)
            {
                errors.Add($"Project uses '{renderPipelineName}'. ChilloutVR CCK requires the 3D Built-In Render Pipeline (URP and HDRP are not supported).");
            }

            // 3. Color Space check
            ColorSpace colorSpace = PlayerSettings.colorSpace;
            if (colorSpace != ColorSpace.Linear)
            {
                warnings.Add($"Project Color Space is '{colorSpace}'. Linear color space is recommended for ChilloutVR content parity.");
            }

            // 4. CCK installation & Assets/ folder hygiene check
            string assetsDir = Application.dataPath;
            string[] knownCckFolderNames = new[] { "ABI.CCK", "ABI.MODS", "ABI.QA", "CVR.CCK" };
            List<string> presentFolders = new List<string>();
            foreach (string folder in knownCckFolderNames)
            {
                if (Directory.Exists(Path.Combine(assetsDir, folder)))
                {
                    presentFolders.Add($"Assets/{folder}");
                }
            }

            Type cck4TestModeType = FindCckType("CVR.CCKEditor.TestMode.CCKTestModeManager") ?? FindCckType("CCKTestModeManager");
            Type cvrWorldType = FindCckType("CVRWorld");
            Type cvrAvatarType = FindCckType("CVRAvatar");
            Type cvrAssetInfoType = FindCckType("CVRAssetInfo");

            bool cckInstalled = cck4TestModeType != null || cvrWorldType != null || cvrAvatarType != null || cvrAssetInfoType != null;
            string cckTrack = !cckInstalled
                ? "not_installed"
                : (cck4TestModeType != null || presentFolders.Contains("Assets/CVR.CCK") ? "cck4" : "cck3_legacy");

            if (!cckInstalled)
            {
                errors.Add($"ChilloutVR CCK is not installed in this project. Download CCK {expectedCckVersion} from https://docs.chilloutvr.net/cck/setup/ and import it cleanly.");
            }
            else if (cckTrack == "cck3_legacy")
            {
                warnings.Add($"Detected legacy CCK 3 installation. CCK 3 development is discontinued; migrate to CCK {expectedCckVersion} (Unity {expectedUnityVersion}) after backing up and deleting old CCK folders.");
            }

            if (presentFolders.Contains("Assets/ABI.QA"))
            {
                warnings.Add("Found 'Assets/ABI.QA' folder. If upgrading CCK 4, remember a clean import requires deleting Assets/ABI.CCK, Assets/ABI.MODS, Assets/ABI.QA, and Assets/CVR.CCK before importing.");
            }

            suggestions.Add($"Sign in to the CCK using your ChilloutVR username and CCK Master Key (Community Hub > Profile Settings > Access Key Management).");
            suggestions.Add($"Official bundle size limits: Avatars <= {maxAvatarMb} MB, Spawnables <= {maxSpawnableMb} MB, Worlds <= {maxWorldMb} MB.");

            string status = errors.Count > 0 ? "error" : (warnings.Count > 0 ? "warning" : "pass");

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"CCK Setup Audit: {status.ToUpper()} ({errors.Count} errors, {warnings.Count} warnings) — Unity {currentUnityVersion} (expected {expectedUnityVersion}), {renderPipelineName}, CCK: {cckTrack}",
                ["status"] = status,
                ["contentType"] = "setup",
                ["errors"] = new JArray(errors),
                ["warnings"] = new JArray(warnings),
                ["suggestions"] = new JArray(suggestions),
                ["environment"] = new JObject
                {
                    ["unityVersion"] = currentUnityVersion,
                    ["expectedUnityVersion"] = expectedUnityVersion,
                    ["expectedCckVersion"] = expectedCckVersion,
                    ["cckInfoSource"] = cckInfoSource,
                    ["exactUnityVersionMatch"] = exactUnityMatch,
                    ["renderPipeline"] = renderPipelineName,
                    ["isBuiltInRenderPipeline"] = isBuiltInRp,
                    ["colorSpace"] = colorSpace.ToString(),
                    ["cckInstalled"] = cckInstalled,
                    ["cckTrack"] = cckTrack,
                    ["cckFolders"] = new JArray(presentFolders),
                    ["contentLimitsMb"] = new JObject
                    {
                        ["avatar"] = maxAvatarMb,
                        ["spawnable"] = maxSpawnableMb,
                        ["world"] = maxWorldMb
                    }
                }
            };
        }

        private static string GetMajorMinor(string unityVersion)
        {
            if (string.IsNullOrEmpty(unityVersion)) return string.Empty;
            string[] parts = unityVersion.Split('.');
            return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : unityVersion;
        }

        private static Type FindCckType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = assembly.GetType($"ABI.CCK.Components.{typeName}")
                          ?? assembly.GetType($"ABI.CCK.Scripts.{typeName}")
                          ?? assembly.GetType($"CVR.CCK.Components.{typeName}")
                          ?? assembly.GetType(typeName);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
