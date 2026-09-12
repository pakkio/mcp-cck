using System;
using System.Linq;
using System.Reflection;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for driving ChilloutVR CCK's built-in Test Mode (the "Test in PlayMode" action
    /// normally reached via a native GenericMenu in the CCK Control Panel, which cannot be
    /// clicked through UI Toolkit automation). Calls CCKTestModeManager's public static API
    /// directly via reflection instead, so no CCK assembly reference is required.
    /// </summary>
    public class CvrTestModeTool : McpToolBase
    {
        public CvrTestModeTool()
        {
            Name = "manage_cck_test_mode";
            Description = "Enters/exits ChilloutVR CCK's built-in Test Mode (spawns a walkable player and builds a temporary version of the selected World/Avatar/Prop), or reports whether it's currently active. This is the same action as the Control Panel's 'Test in PlayMode' menu item, invoked directly since that item lives in a native context menu no UI automation can click.";
        }

        public override JObject Execute(JObject parameters)
        {
            string action = parameters["action"]?.ToObject<string>() ?? "enter";
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();

            Type managerType = FindTypeByFullNameOrSimpleName(
                "CVR.CCKEditor.TestMode.CCKTestModeManager", "CCKTestModeManager");

            if (managerType == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Could not find CCKTestModeManager - is the ChilloutVR CCK package installed in this project?",
                    "not_found_error");
            }

            switch (action)
            {
                case "status":
                    return BuildStatusResponse(managerType);

                case "exit":
                    InvokeStatic(managerType, "ExitTestMode");
                    return new JObject
                    {
                        ["success"] = true,
                        ["type"] = "text",
                        ["message"] = "Exited CCK Test Mode."
                    };

                case "enter":
                    return EnterTestMode(managerType, objectPath, instanceId);

                default:
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Unknown action '{action}'. Use 'enter', 'exit', or 'status'.", "validation_error");
            }
        }

        private JObject EnterTestMode(Type managerType, string objectPath, int? instanceId)
        {
            Type assetInfoType = FindTypeByFullNameOrSimpleName(
                "ABI.CCK.Components.CVRAssetInfo", "CVRAssetInfo");

            if (assetInfoType == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Could not find CVRAssetInfo type in this project.", "not_found_error");
            }

            UnityEngine.Object targetAssetInfo = null;

            if (!string.IsNullOrEmpty(objectPath) || instanceId.HasValue)
            {
                GameObject go = instanceId.HasValue
                    ? UnityEditor.EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject
                    : GameObject.Find(objectPath);

                if (go == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "Could not resolve the given objectPath/instanceId to a GameObject.", "not_found_error");
                }

                targetAssetInfo = go.GetComponent(assetInfoType);
                if (targetAssetInfo == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"GameObject '{go.name}' has no CVRAssetInfo component.", "not_found_error");
                }
            }
            else
            {
                UnityEngine.Object[] candidates = UnityEngine.Object.FindObjectsByType(
                    assetInfoType, FindObjectsSortMode.None);

                if (candidates.Length == 0)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        "No CVRAssetInfo found in the open scene - is there a CVRWorld/CVRAvatar/CVRSpawnable root set up?",
                        "not_found_error");
                }

                targetAssetInfo = candidates.FirstOrDefault(c => GetAssetTypeName(c, assetInfoType) == "World")
                    ?? (candidates.Length == 1 ? candidates[0] : null);

                if (targetAssetInfo == null)
                {
                    string names = string.Join(", ", candidates.Select(c => ((Component)c).gameObject.name));
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Multiple CVRAssetInfo found ({names}) and none is type 'World' - pass objectPath or instanceId to pick one.",
                        "ambiguous_reference_error");
                }
            }

            Array assetInfoArray = Array.CreateInstance(assetInfoType, 1);
            assetInfoArray.SetValue(targetAssetInfo, 0);

            MethodInfo testContent = managerType.GetMethod("TestContent", BindingFlags.Public | BindingFlags.Static);
            if (testContent == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "CCKTestModeManager.TestContent method not found (CCK API may have changed).", "not_found_error");
            }

            string targetName = ((Component)targetAssetInfo).gameObject.name;
            McpLogger.LogInfo($"[MCP Unity] Entering CCK Test Mode for '{targetName}'");

            try
            {
                testContent.Invoke(null, new object[] { assetInfoArray });
            }
            catch (TargetInvocationException ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"CCKTestModeManager.TestContent threw: {ex.InnerException?.Message ?? ex.Message}",
                    "invocation_error");
            }

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Entered CCK Test Mode for '{targetName}'. Unity will build a temporary version and enter Play mode with a walkable player."
            };
        }

        private JObject BuildStatusResponse(Type managerType)
        {
            PropertyInfo isInTestMode = managerType.GetProperty("IsInTestMode", BindingFlags.Public | BindingFlags.Static);
            bool inTestMode = isInTestMode != null && (bool)isInTestMode.GetValue(null);

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["isInTestMode"] = inTestMode,
                ["message"] = inTestMode ? "CCK Test Mode is currently active." : "CCK Test Mode is not active."
            };
        }

        private static string GetAssetTypeName(UnityEngine.Object assetInfo, Type assetInfoType)
        {
            object value = assetInfoType.GetField("type", BindingFlags.Public | BindingFlags.Instance)?.GetValue(assetInfo)
                ?? assetInfoType.GetProperty("type", BindingFlags.Public | BindingFlags.Instance)?.GetValue(assetInfo);
            return value?.ToString();
        }

        private static void InvokeStatic(Type type, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, null);
        }

        private static Type FindTypeByFullNameOrSimpleName(string fullName, string simpleName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type direct = null;
                try { direct = assembly.GetType(fullName); } catch { }
                if (direct != null) return direct;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type match = assembly.GetTypes().FirstOrDefault(t => t.Name == simpleName);
                    if (match != null) return match;
                }
                catch { }
            }

            return null;
        }
    }
}
