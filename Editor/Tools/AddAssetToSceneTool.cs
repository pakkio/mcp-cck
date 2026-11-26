using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for adding assets from the AssetDatabase to the Unity scene
    /// </summary>
    public class AddAssetToSceneTool : McpToolBase
    {
        public AddAssetToSceneTool()
        {
            Name = "add_asset_to_scene";
            Description = "Adds an asset from the AssetDatabase to the Unity scene";
        }
        
        /// <summary>
        /// Execute the AddAssetToScene tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            // Extract parameters
            string assetPath = parameters["assetPath"]?.ToObject<string>();
            string guid = parameters["guid"]?.ToObject<string>();
            Vector3 position = ParseVector3(parameters["position"], Vector3.zero);
            
            // Optional parent game object
            string parentPath = parameters["parentPath"]?.ToObject<string>();
            int? parentId = parameters["parentId"]?.ToObject<int?>();
            
            // Validate parameters - require either assetPath or guid
            if (string.IsNullOrEmpty(assetPath) && string.IsNullOrEmpty(guid))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'assetPath' or 'guid' not provided", 
                    "validation_error"
                );
            }
            
            // If we have a GUID but no path, convert GUID to path
            if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(guid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Asset with GUID '{guid}' not found", 
                        "not_found_error"
                    );
                }
            }
            
            // Load the asset
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Failed to load asset at path '{assetPath}'", 
                    "not_found_error"
                );
            }
            
            // Check if the asset is a prefab or another type that can be instantiated
            bool isPrefab = PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.NotAPrefab;
            bool canInstantiate = asset is GameObject || isPrefab;
            
            if (!canInstantiate)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset of type '{asset.GetType().Name}' cannot be instantiated in the scene", 
                    "invalid_asset_type"
                );
            }
            
            // Instantiate the asset
            GameObject instance = null;
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);

                // Set parent first (if specified), then apply requested world position
                // so parent transforms never overwrite the requested placement.
                if (!string.IsNullOrEmpty(parentPath) || parentId.HasValue)
                {
                    GameObject parent = null;
                    
                    // Try to find parent by ID first
                    if (parentId.HasValue)
                    {
                        parent = UnityObjectId.ObjectFromId(parentId.Value) as GameObject;
                    }
                    // Otherwise try to find by path
                    else if (!string.IsNullOrEmpty(parentPath))
                    {
                        parent = GameObject.Find(parentPath);
                    }
                    
                    if (parent != null)
                    {
                        instance.transform.SetParent(parent.transform, true);
                    }
                    else
                    {
                        McpLogger.LogWarning($"Parent object not found, asset will be created at the root of the scene");
                    }
                }

                // Set world position after parenting
                instance.transform.position = position;
                
                // Select the newly created object
                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error instantiating asset: {ex.Message}", 
                    "instantiation_error"
                );
            }
            
            // Log the action
            Vector3 finalPos = instance.transform.position;
            McpLogger.LogInfo($"Added asset '{asset.name}' to scene from path '{assetPath}' at ({finalPos.x:F2}, {finalPos.y:F2}, {finalPos.z:F2})");
            
            // Create the response
            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Successfully added asset '{asset.name}' with instance ID {UnityObjectId.GetObjectId(instance)} to the scene at ({finalPos.x:F2}, {finalPos.y:F2}, {finalPos.z:F2})",
                ["instanceId"] = UnityObjectId.GetObjectId(instance),
                ["position"] = new JObject
                {
                    ["x"] = finalPos.x,
                    ["y"] = finalPos.y,
                    ["z"] = finalPos.z
                }
            };
        }

        private static Vector3 ParseVector3(JToken token, Vector3 fallback)
        {
            if (token == null) return fallback;
            if (token is JObject obj)
            {
                return new Vector3(
                    obj["x"]?.ToObject<float>() ?? fallback.x,
                    obj["y"]?.ToObject<float>() ?? fallback.y,
                    obj["z"]?.ToObject<float>() ?? fallback.z
                );
            }
            if (token is JArray arr && arr.Count >= 3)
            {
                return new Vector3(
                    arr[0]?.ToObject<float>() ?? fallback.x,
                    arr[1]?.ToObject<float>() ?? fallback.y,
                    arr[2]?.ToObject<float>() ?? fallback.z
                );
            }
            return fallback;
        }
    }
}
