using UnityEngine;
using UnityEditor;

namespace nUtils.HierarchyOrganizer
{
    public class CreateSeparatorWindow : EditorWindow
    {
        private string separatorName = "--- NEW SECTION ---";
        private HierarchySeparator.SeparatorColor selectedColor = HierarchySeparator.SeparatorColor.Blue;
        private Color customColor = new Color(0.3f, 0.5f, 0.8f, 1f);
        private bool isConvertingExisting = false;
        private GameObject targetGameObject;

        private GUIStyle _sectionHeader;
        private GUIStyle _previewLabel;
        private GUIStyle _presetButton;
        private GUIStyle _createButton;
        private bool _stylesReady;

        private static readonly (string name, HierarchySeparator.SeparatorColor color)[] presets = new[]
        {
            ("GAMEPLAY", HierarchySeparator.SeparatorColor.Blue),
            ("ENVIRONMENT", HierarchySeparator.SeparatorColor.Green),
            ("UI", HierarchySeparator.SeparatorColor.Purple),
            ("MANAGERS", HierarchySeparator.SeparatorColor.Orange),
            ("LIGHTING", HierarchySeparator.SeparatorColor.Yellow),
            ("AUDIO", HierarchySeparator.SeparatorColor.Cyan),
            ("EFFECTS", HierarchySeparator.SeparatorColor.Red),
            ("CAMERAS", HierarchySeparator.SeparatorColor.Purple),
        };

        private static readonly HierarchySeparator.SeparatorColor[] colorPalette = new[]
        {
            HierarchySeparator.SeparatorColor.Blue,
            HierarchySeparator.SeparatorColor.Green,
            HierarchySeparator.SeparatorColor.Orange,
            HierarchySeparator.SeparatorColor.Purple,
            HierarchySeparator.SeparatorColor.Red,
            HierarchySeparator.SeparatorColor.Cyan,
            HierarchySeparator.SeparatorColor.Yellow,
            HierarchySeparator.SeparatorColor.Custom,
        };

        public static void ShowWindow()
        {
            var window = GetWindow<CreateSeparatorWindow>(true, "Create Separator", true);
            window.minSize = new Vector2(320, 380);
            window.maxSize = new Vector2(400, 480);

            if (Selection.activeGameObject != null &&
                Selection.activeGameObject.GetComponent<HierarchySeparator>() == null)
            {
                window.isConvertingExisting = true;
                window.targetGameObject = Selection.activeGameObject;
                window.separatorName = Selection.activeGameObject.name;
            }
            else
            {
                window.isConvertingExisting = false;
                window.targetGameObject = null;
                window.separatorName = "--- NEW SECTION ---";
            }

            window.ShowUtility();
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _sectionHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                margin = new RectOffset(0, 0, 0, 2),
            };

            _previewLabel = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
            };

            _presetButton = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                fixedHeight = 24,
                padding = new RectOffset(6, 6, 2, 2),
            };

            _createButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 32,
            };

            _stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureStyles();

            // Outer padding
            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14);
            EditorGUILayout.BeginVertical();

            // --- Converting info ---
            if (isConvertingExisting && targetGameObject != null)
            {
                EditorGUILayout.HelpBox(
                    $"Converting \"{targetGameObject.name}\" to a hierarchy separator.",
                    MessageType.Info);
                GUILayout.Space(8);
            }

            // --- Name ---
            DrawSectionHeader("Separator Name");
            GUILayout.Space(3);
            separatorName = EditorGUILayout.TextField(separatorName);
            GUILayout.Space(14);

            // --- Quick Presets ---
            DrawSectionHeader("Quick Presets");
            GUILayout.Space(4);
            DrawPresets();
            GUILayout.Space(14);

            // --- Color ---
            DrawSectionHeader("Color");
            GUILayout.Space(6);
            DrawColorPalette();

            if (selectedColor == HierarchySeparator.SeparatorColor.Custom)
            {
                GUILayout.Space(6);
                customColor = EditorGUILayout.ColorField(
                    new GUIContent("Custom Color"), customColor, true, false, false);
            }
            GUILayout.Space(14);

            // --- Preview ---
            DrawSectionHeader("Preview");
            GUILayout.Space(4);
            DrawPreview();

            // Push create button to bottom
            GUILayout.FlexibleSpace();

            // --- Create button ---
            string btnLabel = isConvertingExisting ? "Convert to Separator" : "Create Separator";
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.35f, 0.7f, 0.4f);
            if (GUILayout.Button(btnLabel, _createButton))
            {
                PerformCreate();
                Close();
            }
            GUI.backgroundColor = prevBg;

            GUILayout.Space(10);

            EditorGUILayout.EndVertical();
            GUILayout.Space(14);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();

            HandleKeyboard();
        }

        private void DrawSectionHeader(string title)
        {
            EditorGUILayout.LabelField(title, _sectionHeader);

            Rect lineRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            Color lineColor = EditorGUIUtility.isProSkin
                ? new Color(0.25f, 0.25f, 0.25f)
                : new Color(0.72f, 0.72f, 0.72f);
            EditorGUI.DrawRect(lineRect, lineColor);
        }

        private void DrawPresets()
        {
            int columns = 4;
            int index = 0;

            while (index < presets.Length)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < columns && index < presets.Length; col++, index++)
                {
                    var preset = presets[index];
                    Color btnColor = GetColorForPreset(preset.color);

                    Color prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = Color.Lerp(btnColor, Color.white, 0.2f);

                    if (GUILayout.Button(preset.name, _presetButton))
                    {
                        separatorName = $"--- {preset.name} ---";
                        selectedColor = preset.color;
                    }
                    GUI.backgroundColor = prevBg;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawColorPalette()
        {
            float swatchSize = 26;
            float gap = 6;
            int count = colorPalette.Length;
            float totalWidth = count * swatchSize + (count - 1) * gap;

            Rect area = GUILayoutUtility.GetRect(totalWidth, swatchSize + 6);
            float startX = area.x + (area.width - totalWidth) * 0.5f;
            float y = area.y + 3;

            for (int i = 0; i < count; i++)
            {
                var colorEnum = colorPalette[i];
                float x = startX + i * (swatchSize + gap);
                Rect swatch = new Rect(x, y, swatchSize, swatchSize);

                Color color = colorEnum == HierarchySeparator.SeparatorColor.Custom
                    ? customColor
                    : GetColorForPreset(colorEnum);

                bool isSelected = selectedColor == colorEnum;

                // Selection ring (bright outline)
                if (isSelected)
                {
                    Rect ring = new Rect(swatch.x - 3, swatch.y - 3,
                                         swatch.width + 6, swatch.height + 6);
                    Color ringColor = EditorGUIUtility.isProSkin
                        ? new Color(0.85f, 0.85f, 0.85f)
                        : Color.white;
                    EditorGUI.DrawRect(ring, ringColor);
                }

                // Dark border
                Rect border = new Rect(swatch.x - 1, swatch.y - 1,
                                       swatch.width + 2, swatch.height + 2);
                EditorGUI.DrawRect(border, new Color(0.12f, 0.12f, 0.12f));

                // Color fill
                EditorGUI.DrawRect(swatch, color);

                // "+" indicator on the custom swatch
                if (colorEnum == HierarchySeparator.SeparatorColor.Custom)
                {
                    GUIStyle indicator = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize = 14,
                    };
                    indicator.normal.textColor = GetContrastColor(color);
                    GUI.Label(swatch, "+", indicator);
                }

                // Click detection
                if (Event.current.type == EventType.MouseDown &&
                    swatch.Contains(Event.current.mousePosition))
                {
                    selectedColor = colorEnum;
                    Event.current.Use();
                    Repaint();
                }
            }
        }

        private void DrawPreview()
        {
            Color previewColor = GetPreviewColor();

            // Outer frame
            Rect outer = EditorGUILayout.GetControlRect(false, 26);
            Color borderColor = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f)
                : new Color(0.55f, 0.55f, 0.55f);
            EditorGUI.DrawRect(outer, borderColor);

            // Inner colored fill
            Rect inner = new Rect(outer.x + 1, outer.y + 1,
                                  outer.width - 2, outer.height - 2);
            EditorGUI.DrawRect(inner, previewColor);

            // Centered label
            _previewLabel.normal.textColor = GetContrastColor(previewColor);
            GUI.Label(inner, separatorName, _previewLabel);
        }

        private void HandleKeyboard()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    PerformCreate();
                    Close();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    Close();
                    e.Use();
                }
            }
        }

        private Color GetPreviewColor()
        {
            return selectedColor == HierarchySeparator.SeparatorColor.Custom
                ? customColor
                : GetColorForPreset(selectedColor);
        }

        private Color GetColorForPreset(HierarchySeparator.SeparatorColor preset)
        {
            switch (preset)
            {
                case HierarchySeparator.SeparatorColor.Blue:   return new Color(0.3f, 0.5f, 0.8f, 1f);
                case HierarchySeparator.SeparatorColor.Green:  return new Color(0.5f, 0.7f, 0.4f, 1f);
                case HierarchySeparator.SeparatorColor.Orange: return new Color(0.9f, 0.6f, 0.3f, 1f);
                case HierarchySeparator.SeparatorColor.Purple: return new Color(0.7f, 0.4f, 0.7f, 1f);
                case HierarchySeparator.SeparatorColor.Red:    return new Color(0.8f, 0.3f, 0.4f, 1f);
                case HierarchySeparator.SeparatorColor.Cyan:   return new Color(0.4f, 0.7f, 0.7f, 1f);
                case HierarchySeparator.SeparatorColor.Yellow: return new Color(0.9f, 0.8f, 0.3f, 1f);
                default: return customColor;
            }
        }

        private Color GetContrastColor(Color bg)
        {
            float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            return luminance > 0.5f ? Color.black : Color.white;
        }

        private void PerformCreate()
        {
            if (string.IsNullOrWhiteSpace(separatorName))
                return;

            GameObject separator;
            HierarchySeparator comp;

            if (isConvertingExisting && targetGameObject != null)
            {
                separator = targetGameObject;
                separator.name = separatorName;
                comp = Undo.AddComponent<HierarchySeparator>(separator);
            }
            else
            {
                separator = new GameObject(separatorName);
                comp = separator.AddComponent<HierarchySeparator>();

                if (Selection.activeTransform != null)
                {
                    separator.transform.SetParent(Selection.activeTransform.parent);
                    separator.transform.SetSiblingIndex(
                        Selection.activeTransform.GetSiblingIndex() + 1);
                }

                separator.transform.localPosition = Vector3.zero;
                separator.transform.localRotation = Quaternion.identity;
                separator.transform.localScale = Vector3.one;

                Undo.RegisterCreatedObjectUndo(separator, "Create Hierarchy Separator");
            }

            comp.ColorPreset = selectedColor;

            if (selectedColor == HierarchySeparator.SeparatorColor.Custom)
                comp.CustomColor = customColor;

            Selection.activeGameObject = separator;
            EditorUtility.SetDirty(separator);
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
