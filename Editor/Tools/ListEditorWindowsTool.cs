using System.Collections.Generic;
using System.Linq;
using McpUnity.Unity;
using UnityEditor;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for listing open Editor windows and, optionally, the clickable UI Toolkit
    /// elements inside one of them. Use this before click_ui_element to discover the
    /// element names/text you need, since custom editor windows (like third-party
    /// package control panels) expose no other way to introspect their UI.
    /// </summary>
    public class ListEditorWindowsTool : McpToolBase
    {
        public ListEditorWindowsTool()
        {
            Name = "list_editor_windows";
            Description = "Lists all open Unity Editor windows (title, type name). Pass windowTitle to additionally list every clickable UI Toolkit element (Button and Button-like elements) inside that window, with its name/text/type - use this to discover what click_ui_element can target.";
        }

        public override JObject Execute(JObject parameters)
        {
            string windowTitleFilter = parameters["windowTitle"]?.ToObject<string>();

            EditorWindow[] windows = UnityEngine.Resources.FindObjectsOfTypeAll<EditorWindow>();

            JArray windowsArray = new JArray();
            EditorWindow matchedWindow = null;

            foreach (EditorWindow window in windows)
            {
                if (window == null) continue;

                string title = window.titleContent != null ? window.titleContent.text : string.Empty;
                string typeName = window.GetType().Name;

                windowsArray.Add(new JObject
                {
                    ["title"] = title,
                    ["typeName"] = typeName,
                    ["focused"] = window == EditorWindow.focusedWindow
                });

                if (!string.IsNullOrEmpty(windowTitleFilter) && matchedWindow == null &&
                    (title.IndexOf(windowTitleFilter, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     typeName.IndexOf(windowTitleFilter, System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    matchedWindow = window;
                }
            }

            JObject result = new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["windows"] = windowsArray
            };

            if (string.IsNullOrEmpty(windowTitleFilter))
            {
                result["message"] = $"Found {windowsArray.Count} open editor window(s).";
                return result;
            }

            if (matchedWindow == null)
            {
                result["message"] = $"No open window matched '{windowTitleFilter}'.";
                return result;
            }

            VisualElement root = matchedWindow.rootVisualElement;
            JArray elements = new JArray();

            if (root != null)
            {
                List<VisualElement> descendants = root.Query<VisualElement>().Build().ToList();
                foreach (VisualElement element in descendants)
                {
                    bool looksClickable = element is Button
                        || element.GetType().Name.IndexOf("Button", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || element.GetType().Name.IndexOf("Toolbar", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!looksClickable) continue;

                    string text = (element as TextElement)?.text ?? string.Empty;

                    elements.Add(new JObject
                    {
                        ["name"] = element.name ?? string.Empty,
                        ["text"] = text,
                        ["typeName"] = element.GetType().Name,
                        ["enabledInHierarchy"] = element.enabledInHierarchy,
                        ["className"] = string.Join(" ", element.GetClasses())
                    });
                }
            }

            result["matchedWindow"] = new JObject
            {
                ["title"] = matchedWindow.titleContent.text,
                ["typeName"] = matchedWindow.GetType().Name
            };
            result["clickableElements"] = elements;
            result["message"] = $"Found {elements.Count} clickable-looking element(s) in '{matchedWindow.titleContent.text}'.";

            return result;
        }
    }
}
