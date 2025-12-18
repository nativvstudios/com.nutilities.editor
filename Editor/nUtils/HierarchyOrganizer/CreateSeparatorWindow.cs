using UnityEngine;
using UnityEditor;

namespace nUtils.HierarchyOrganizer
{
    public class CreateSeparatorWindow : EditorWindow
    {
        private string separatorName = "--- NEW SECTION ---";
        private HierarchySeparator.SeparatorColor selectedColor = HierarchySeparator.SeparatorColor.Blue;
        private Color customColor = new Color(0.3f, 0.5f, 0.8f, 1f);
        private Vector2 scrollPosition;

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

        public static void ShowWindow()
        {
            CreateSeparatorWindow window = GetWindow<CreateSeparatorWindow>(true, "Create Separator", true);
            window.minSize = new Vector2(280, 200);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(8);

            // Name field
            EditorGUILayout.LabelField("Name", EditorStyles.boldLabel);
            separatorName = EditorGUILayout.TextField(separatorName);

            EditorGUILayout.Space(8);

            // Presets
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

            int columns = Mathf.Max(2, (int)(position.width / 100));
            int index = 0;

            while (index < presets.Length)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < columns && index < presets.Length; col++, index++)
                {
                    var preset = presets[index];
                    Color btnColor = GetColorForPreset(preset.color);

                    GUI.backgroundColor = btnColor;
                    if (GUILayout.Button(preset.name, GUILayout.Height(24)))
                    {
                        separatorName = $"--- {preset.name} ---";
                        selectedColor = preset.color;
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);

            // Color selection
            EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawColorButton(HierarchySeparator.SeparatorColor.Blue);
            DrawColorButton(HierarchySeparator.SeparatorColor.Green);
            DrawColorButton(HierarchySeparator.SeparatorColor.Orange);
            DrawColorButton(HierarchySeparator.SeparatorColor.Purple);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawColorButton(HierarchySeparator.SeparatorColor.Red);
            DrawColorButton(HierarchySeparator.SeparatorColor.Cyan);
            DrawColorButton(HierarchySeparator.SeparatorColor.Yellow);
            DrawColorButton(HierarchySeparator.SeparatorColor.Custom);
            EditorGUILayout.EndHorizontal();

            if (selectedColor == HierarchySeparator.SeparatorColor.Custom)
            {
                customColor = EditorGUILayout.ColorField("Custom Color", customColor);
            }

            EditorGUILayout.Space(8);

            // Preview
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            Rect previewRect = EditorGUILayout.GetControlRect(false, 22);
            Color previewColor = GetPreviewColor();
            EditorGUI.DrawRect(previewRect, previewColor);

            GUIStyle previewStyle = new GUIStyle(EditorStyles.boldLabel);
            previewStyle.alignment = TextAnchor.MiddleCenter;
            previewStyle.normal.textColor = GetContrastColor(previewColor);
            GUI.Label(previewRect, separatorName, previewStyle);

            EditorGUILayout.Space(12);

            // Create button
            GUI.backgroundColor = new Color(0.4f, 0.7f, 0.4f);
            if (GUILayout.Button("Create", GUILayout.Height(28)))
            {
                CreateSeparator();
                Close();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            EditorGUILayout.EndScrollView();

            HandleKeyboard();
        }

        private void DrawColorButton(HierarchySeparator.SeparatorColor color)
        {
            Color btnColor = GetColorForPreset(color);
            bool isSelected = selectedColor == color;

            GUIStyle style = new GUIStyle(GUI.skin.button);
            if (isSelected)
            {
                style.fontStyle = FontStyle.Bold;
            }

            GUI.backgroundColor = btnColor;
            if (GUILayout.Button(isSelected ? "●" : "", style, GUILayout.Height(22)))
            {
                selectedColor = color;
            }
            GUI.backgroundColor = Color.white;
        }

        private void HandleKeyboard()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    CreateSeparator();
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
                case HierarchySeparator.SeparatorColor.Blue: return new Color(0.3f, 0.5f, 0.8f, 1f);
                case HierarchySeparator.SeparatorColor.Green: return new Color(0.5f, 0.7f, 0.4f, 1f);
                case HierarchySeparator.SeparatorColor.Orange: return new Color(0.9f, 0.6f, 0.3f, 1f);
                case HierarchySeparator.SeparatorColor.Purple: return new Color(0.7f, 0.4f, 0.7f, 1f);
                case HierarchySeparator.SeparatorColor.Red: return new Color(0.8f, 0.3f, 0.4f, 1f);
                case HierarchySeparator.SeparatorColor.Cyan: return new Color(0.4f, 0.7f, 0.7f, 1f);
                case HierarchySeparator.SeparatorColor.Yellow: return new Color(0.9f, 0.8f, 0.3f, 1f);
                default: return customColor;
            }
        }

        private Color GetContrastColor(Color bg)
        {
            float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            return luminance > 0.5f ? Color.black : Color.white;
        }

        private void CreateSeparator()
        {
            if (string.IsNullOrWhiteSpace(separatorName))
                return;

            GameObject separator = new GameObject(separatorName);
            HierarchySeparator comp = separator.AddComponent<HierarchySeparator>();
            comp.ColorPreset = selectedColor;

            if (selectedColor == HierarchySeparator.SeparatorColor.Custom)
                comp.CustomColor = customColor;

            if (Selection.activeTransform != null)
            {
                separator.transform.SetParent(Selection.activeTransform.parent);
                separator.transform.SetSiblingIndex(Selection.activeTransform.GetSiblingIndex() + 1);
            }

            separator.transform.localPosition = Vector3.zero;
            separator.transform.localRotation = Quaternion.identity;
            separator.transform.localScale = Vector3.one;

            Undo.RegisterCreatedObjectUndo(separator, "Create Hierarchy Separator");
            Selection.activeGameObject = separator;
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
