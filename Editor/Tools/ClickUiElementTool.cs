using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for clicking a UI Toolkit button/clickable element inside an open Editor window.
    /// Complements execute_menu_item for the many editor actions (including third-party
    /// package windows like control panels) that are only exposed as an in-window button,
    /// not a top-level MenuItem.
    /// </summary>
    public class ClickUiElementTool : McpToolBase
    {
        public ClickUiElementTool()
        {
            Name = "click_ui_element";
            Description = "Clicks a Button (or Button-like) UI Toolkit element inside an open Editor window, matched by elementName (VisualElement.name) or elementText (visible label). Use list_editor_windows first to discover available windows/elements. Set dryRun to true to list matching-window candidates without clicking.";
        }

        public override JObject Execute(JObject parameters)
        {
            string windowTitle = parameters["windowTitle"]?.ToObject<string>();
            string elementName = parameters["elementName"]?.ToObject<string>();
            string elementText = parameters["elementText"]?.ToObject<string>();
            bool dryRun = parameters["dryRun"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(windowTitle))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'windowTitle' not provided", "validation_error");
            }

            if (string.IsNullOrEmpty(elementName) && string.IsNullOrEmpty(elementText))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Provide at least one of 'elementName' or 'elementText'", "validation_error");
            }

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            EditorWindow window = windows.FirstOrDefault(w => w != null && (
                (w.titleContent != null && w.titleContent.text.IndexOf(windowTitle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                w.GetType().Name.IndexOf(windowTitle, StringComparison.OrdinalIgnoreCase) >= 0));

            if (window == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"No open window matched '{windowTitle}'. Use list_editor_windows to see what's open.",
                    "not_found_error");
            }

            VisualElement root = window.rootVisualElement;
            if (root == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Window '{window.titleContent.text}' has no UI Toolkit rootVisualElement (likely an IMGUI-only window; click_ui_element can't target it).",
                    "unsupported_error");
            }

            List<VisualElement> candidates = root.Query<VisualElement>().Build().ToList()
                .Where(e =>
                {
                    bool nameMatch = !string.IsNullOrEmpty(elementName) &&
                        string.Equals(e.name, elementName, StringComparison.OrdinalIgnoreCase);
                    string text = (e as TextElement)?.text ?? string.Empty;
                    bool textMatch = !string.IsNullOrEmpty(elementText) &&
                        text.IndexOf(elementText, StringComparison.OrdinalIgnoreCase) >= 0;
                    return nameMatch || textMatch;
                })
                .ToList();

            if (dryRun || candidates.Count == 0)
            {
                JArray matches = new JArray(candidates.Select(e => new JObject
                {
                    ["name"] = e.name ?? string.Empty,
                    ["text"] = (e as TextElement)?.text ?? string.Empty,
                    ["typeName"] = e.GetType().Name,
                    ["enabledInHierarchy"] = e.enabledInHierarchy
                }));

                return new JObject
                {
                    ["success"] = candidates.Count > 0,
                    ["type"] = "text",
                    ["dryRun"] = dryRun,
                    ["matches"] = matches,
                    ["message"] = candidates.Count == 0
                        ? $"No element matched name='{elementName}' text='{elementText}' in '{window.titleContent.text}'. Call list_editor_windows with windowTitle to see candidates."
                        : $"Found {candidates.Count} matching element(s) in '{window.titleContent.text}' (dry run, nothing clicked)."
                };
            }

            VisualElement target = candidates[0];

            if (!target.enabledInHierarchy)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Element '{target.name}' in '{window.titleContent.text}' is disabled and cannot be clicked.",
                    "disabled_error");
            }

            bool invoked = TryInvokeClickedEvent(target) || TrySimulatePointerClick(target);

            if (!invoked)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Found element '{target.name}' but could not trigger a click on it (unsupported element type).",
                    "click_failed_error");
            }

            McpLogger.LogInfo($"[MCP Unity] Clicked UI element '{target.name}' ({target.GetType().Name}) in window '{window.titleContent.text}'");

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = $"Clicked '{target.name}' ({target.GetType().Name}) in '{window.titleContent.text}'."
            };
        }

        /// <summary>
        /// Fast path for UnityEngine.UIElements.Button: invoke its "clicked" event's backing
        /// delegate directly via reflection. Works regardless of what the button is wired to.
        /// </summary>
        private static bool TryInvokeClickedEvent(VisualElement element)
        {
            if (!(element is Button button)) return false;

            FieldInfo field = typeof(Button).GetField("clicked", BindingFlags.Instance | BindingFlags.NonPublic);
            Action del = field?.GetValue(button) as Action;
            if (del == null) return false;

            del.Invoke();
            return true;
        }

        /// <summary>
        /// Fallback for any clickable VisualElement (custom Clickable manipulators, ToolbarButton,
        /// etc.): synthesize a pointer-down + pointer-up at the element's center, which is what
        /// UI Toolkit's Clickable manipulator listens for.
        /// </summary>
        private static bool TrySimulatePointerClick(VisualElement element)
        {
            try
            {
                Vector2 center = element.worldBound.center;

                using (PointerDownEvent down = PointerDownEvent.GetPooled(
                    new Event { type = EventType.MouseDown, mousePosition = center, button = 0, clickCount = 1 }))
                {
                    down.target = element;
                    element.SendEvent(down);
                }

                using (PointerUpEvent up = PointerUpEvent.GetPooled(
                    new Event { type = EventType.MouseUp, mousePosition = center, button = 0, clickCount = 1 }))
                {
                    up.target = element;
                    element.SendEvent(up);
                }

                return true;
            }
            catch (Exception ex)
            {
                McpLogger.LogWarning($"[MCP Unity] Pointer click simulation failed: {ex.Message}");
                return false;
            }
        }
    }
}
