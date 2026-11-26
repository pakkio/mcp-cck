using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Unity;
using McpUnity.Utils;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for creating and saving a new Unity scene
    /// </summary>
    public class CreateSceneTool : McpToolBase
    {
        public CreateSceneTool()
        {
            Name = "create_scene";
            Description = "Creates a new scene and saves it to the specified path";
        }

        /// <summary>
        /// Execute the CreateScene tool with the provided parameters
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        public override JObject Execute(JObject parameters)
        {
            // Parameters
            string sceneName = parameters["sceneName"]?.ToObject<string>();
            string folderPath = parameters["folderPath"]?.ToObject<string>();
            bool addToBuildSettings = parameters["addToBuildSettings"]?.ToObject<bool?>() ?? false;
            bool makeActive = parameters["makeActive"]?.ToObject<bool?>() ?? true;

            if (string.IsNullOrEmpty(sceneName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'sceneName' not provided",
                    "validation_error"
                );
            }

            // Default folder path
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = "Assets";
            }

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                // Attempt to create nested folders as needed
                string[] parts = folderPath.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);
                string current = parts.Length > 0 && parts[0] == "Assets" ? "Assets" : "Assets";
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i == 0 && parts[i] == "Assets") continue;
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }

            // Create unique path for the scene
            string basePath = folderPath.TrimEnd('/');
            string scenePath = AssetDatabase.GenerateUniqueAssetPath($"{basePath}/{sceneName}.unity");

            try
            {
                // Ensure all existing scenes are saved cleanly before creating a new scene
                // to prevent Unity from popping up a modal Save dialog on untitled scenes.
                SceneSaveHelper.EnsureAllScenesSavedSilently();

                // NewSceneMode.Single unconditionally replaces every currently loaded scene, so
                // it can only be used when the caller actually asked for that (makeActive: true).
                // Creating Additive instead when makeActive is false leaves whatever the caller
                // already had open untouched - the new scene is closed again below once it has
                // been saved, since the caller asked for it to be created, not left open.
                var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                    makeActive ? UnityEditor.SceneManagement.NewSceneMode.Single : UnityEditor.SceneManagement.NewSceneMode.Additive);

                bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, scenePath);
                if (!saved)
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Failed to save scene at '{scenePath}'",
                        "save_error"
                    );
                }

                AssetDatabase.Refresh();

                // Optionally add to build settings
                if (addToBuildSettings)
                {
                    AddSceneToBuildSettings(scenePath);
                }

                // makeActive: false means "create and save it, don't open it" - close the
                // additively-loaded scene now that it's on disk, unless it's the only scene left
                // loaded (EditorSceneManager refuses to close the last remaining open scene).
                if (!makeActive && UnityEngine.SceneManagement.SceneManager.loadedSceneCount > 1)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(newScene, true);
                }

                McpLogger.LogInfo($"Created scene '{sceneName}' at path '{scenePath}'");

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Successfully created scene '{sceneName}' at path '{scenePath}'",
                    ["scenePath"] = scenePath
                };
            }
            catch (Exception ex)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Error creating scene: {ex.Message}",
                    "scene_creation_error"
                );
            }
        }

        private void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = UnityEditor.EditorBuildSettings.scenes;

            // Check if already present
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                {
                    return;
                }
            }

            var newList = new UnityEditor.EditorBuildSettingsScene[scenes.Length + 1];
            for (int i = 0; i < scenes.Length; i++)
            {
                newList[i] = scenes[i];
            }
            newList[newList.Length - 1] = new UnityEditor.EditorBuildSettingsScene(scenePath, true);
            UnityEditor.EditorBuildSettings.scenes = newList;
        }
    }
}


