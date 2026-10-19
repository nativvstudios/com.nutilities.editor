using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace nUtils
{
    /// <summary>Which click opens the pick list.</summary>
    public enum PickTrigger { RightClick, ShiftRightClick, AltRightClick }

    /// <summary>
    /// nSelect's settings, per user on this machine (EditorPrefs), in Preferences > nUtilities > nSelect.
    /// </summary>
    public static class nSelectSettings
    {
        const string Prefix = "nUtilities.nSelect.";
        const string DefaultHidden = "Transform, RectTransform, CanvasRenderer, SortingGroup";
        static readonly Color DefaultHighlight = new Color(1f, 0.78f, 0.25f, 1f);

        public static bool Enabled { get => EditorPrefs.GetBool(Prefix + "Enabled", true); set => EditorPrefs.SetBool(Prefix + "Enabled", value); }
        public static PickTrigger Trigger { get => (PickTrigger)EditorPrefs.GetInt(Prefix + "Trigger", 0); set => EditorPrefs.SetInt(Prefix + "Trigger", (int)value); }
        /// <summary>How far (pixels) the mouse may move between press and release and still open the list (a drag pans).</summary>
        public static float DeadZone { get => EditorPrefs.GetFloat(Prefix + "DeadZone", 6f); set => EditorPrefs.SetFloat(Prefix + "DeadZone", Mathf.Clamp(value, 1f, 40f)); }
        public static bool ShowSearch { get => EditorPrefs.GetBool(Prefix + "ShowSearch", true); set => EditorPrefs.SetBool(Prefix + "ShowSearch", value); }
        public static bool ShowFilters { get => EditorPrefs.GetBool(Prefix + "ShowFilters", true); set => EditorPrefs.SetBool(Prefix + "ShowFilters", value); }
        public static bool ShowPaths { get => EditorPrefs.GetBool(Prefix + "ShowPaths", true); set => EditorPrefs.SetBool(Prefix + "ShowPaths", value); }
        public static bool IncludeTiles { get => EditorPrefs.GetBool(Prefix + "IncludeTiles", true); set => EditorPrefs.SetBool(Prefix + "IncludeTiles", value); }
        public static bool IncludeColliders { get => EditorPrefs.GetBool(Prefix + "IncludeColliders", true); set => EditorPrefs.SetBool(Prefix + "IncludeColliders", value); }
        public static bool HoverHighlight { get => EditorPrefs.GetBool(Prefix + "HoverHighlight", true); set => EditorPrefs.SetBool(Prefix + "HoverHighlight", value); }

        public static Color HighlightColor
        {
            get => ColorUtility.TryParseHtmlString("#" + EditorPrefs.GetString(Prefix + "Highlight", ColorUtility.ToHtmlStringRGBA(DefaultHighlight)), out var c) ? c : DefaultHighlight;
            set => EditorPrefs.SetString(Prefix + "Highlight", ColorUtility.ToHtmlStringRGBA(value));
        }

        /// <summary>Component icons left out of the list (every object has a Transform, so its icon says nothing).</summary>
        public static string HiddenIconsText { get => EditorPrefs.GetString(Prefix + "HiddenIcons", DefaultHidden); set => EditorPrefs.SetString(Prefix + "HiddenIcons", value ?? ""); }
        public static HashSet<string> HiddenIcons =>
            new HashSet<string>(HiddenIconsText.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0), System.StringComparer.OrdinalIgnoreCase);

        static void ResetAll()
        {
            foreach (var key in new[] { "Enabled", "Trigger", "DeadZone", "ShowSearch", "ShowFilters", "ShowPaths", "IncludeTiles", "IncludeColliders", "HoverHighlight", "Highlight", "HiddenIcons" })
                EditorPrefs.DeleteKey(Prefix + key);
        }

        [MenuItem("Window/nUtilities/nSelect Settings")]
        public static void OpenSettings() => SettingsService.OpenUserPreferences("Preferences/nUtilities/nSelect");

        [SettingsProvider]
        static SettingsProvider Create() => new SettingsProvider("Preferences/nUtilities/nSelect", SettingsScope.User)
        {
            keywords = new[] { "selection", "pick", "right click", "scene", "popup", "tile", "overlap" },
            guiHandler = _ =>
            {
                EditorGUIUtility.labelWidth = 220f;
                EditorGUILayout.HelpBox("Right-click in the Scene view to list everything under the cursor (sprites, the tile in each " +
                                        "tilemap, colliders and triggers) and pick one. Right-clicking empty space opens Unity's own menu.", MessageType.None);
                EditorGUI.BeginChangeCheck();
                Enabled = EditorGUILayout.Toggle("Enabled", Enabled);
                using (new EditorGUI.DisabledScope(!Enabled))
                {
                    Trigger = (PickTrigger)EditorGUILayout.EnumPopup(new GUIContent("Open with", "The click that opens the list."), Trigger);
                    DeadZone = EditorGUILayout.Slider(new GUIContent("Click dead zone (px)", "How far the mouse may move between press and release; more is a drag (it pans)."), DeadZone, 1f, 40f);
                    EditorGUILayout.Space();
                    IncludeTiles = EditorGUILayout.Toggle(new GUIContent("List tiles", "The tile in each tilemap under the cursor, as its own entry."), IncludeTiles);
                    IncludeColliders = EditorGUILayout.Toggle(new GUIContent("List colliders and triggers", "Objects that draw nothing but have a 2D collider there (markers, areas, doors)."), IncludeColliders);
                    ShowSearch = EditorGUILayout.Toggle("Search field", ShowSearch);
                    ShowFilters = EditorGUILayout.Toggle("Filter tabs", ShowFilters);
                    ShowPaths = EditorGUILayout.Toggle(new GUIContent("Parent path", "Where each object sits in the hierarchy, under its name."), ShowPaths);
                    EditorGUILayout.Space();
                    HoverHighlight = EditorGUILayout.Toggle(new GUIContent("Highlight on hover", "Outline the hovered entry in the Scene view."), HoverHighlight);
                    using (new EditorGUI.DisabledScope(!HoverHighlight))
                        HighlightColor = EditorGUILayout.ColorField("Highlight colour", HighlightColor);
                    HiddenIconsText = EditorGUILayout.TextField(new GUIContent("Hidden component icons", "Component types (comma separated) whose icons aren't shown."), HiddenIconsText);
                }
                EditorGUILayout.Space();
                if (GUILayout.Button("Use defaults", GUILayout.Width(120f))) ResetAll();
                if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            },
        };
    }
}
