using UnityEngine;
using UnityEditor;

namespace nUtils.HierarchyOrganizer
{
    [InitializeOnLoad]
    public static class HierarchySeparatorEditor
    {
        private static readonly GUIStyle separatorStyle;

        static HierarchySeparatorEditor()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
        }

        private static void OnHierarchyWindowItemGUI(int instanceID, Rect selectionRect)
        {
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

            if (obj == null)
                return;

            HierarchySeparator separator = obj.GetComponent<HierarchySeparator>();

            if (separator == null)
                return;

            // Draw colored background across entire row
            Rect bgRect = selectionRect;
            bgRect.xMin = 32; // Start after the fold arrow
            bgRect.xMax = selectionRect.xMax + 16; // Extend to the right edge

            Color backgroundColor = separator.GetColor();
            EditorGUI.DrawRect(bgRect, backgroundColor);

            // Create style for the text
            GUIStyle textStyle = new GUIStyle(EditorStyles.label);
            textStyle.fontStyle = FontStyle.Bold;
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.normal.textColor = GetContrastColor(backgroundColor);

            // Draw the separator name
            Rect labelRect = selectionRect;
            labelRect.xMin = 32;

            GUI.Label(labelRect, obj.name, textStyle);

            // Optionally draw icon indicator
            Rect iconRect = new Rect(selectionRect.x, selectionRect.y, 16, 16);
            Color iconColor = separator.GetColor();
            iconColor.a = 1f;

            // Draw a small colored square as an icon
            Rect smallIconRect = new Rect(selectionRect.x + 2, selectionRect.y + 2, 12, 12);
            EditorGUI.DrawRect(smallIconRect, iconColor);
        }

        public static Color GetContrastColor(Color backgroundColor)
        {
            // Calculate luminance to determine if text should be black or white
            float luminance = 0.299f * backgroundColor.r + 0.587f * backgroundColor.g + 0.114f * backgroundColor.b;

            if (luminance > 0.5f)
                return Color.black;
            else
                return Color.white;
        }
    }

    [CustomEditor(typeof(HierarchySeparator))]
    public class HierarchySeparatorInspector : Editor
    {
        private SerializedProperty separatorColorProp;
        private SerializedProperty customColorProp;

        private void OnEnable()
        {
            separatorColorProp = serializedObject.FindProperty("separatorColor");
            customColorProp = serializedObject.FindProperty("customColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            HierarchySeparator separator = (HierarchySeparator)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hierarchy Separator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This GameObject will be displayed as a colored separator in the hierarchy window.", MessageType.Info);
            EditorGUILayout.Space(5);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(separatorColorProp, new GUIContent("Color Preset"));

            if (separatorColorProp.enumValueIndex == (int)HierarchySeparator.SeparatorColor.Custom)
            {
                EditorGUILayout.PropertyField(customColorProp, new GUIContent("Custom Color"));
            }

            // Preview
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);

            Rect previewRect = EditorGUILayout.GetControlRect(false, 20);
            Color previewColor = separator.GetColor();
            EditorGUI.DrawRect(previewRect, previewColor);

            GUIStyle previewStyle = new GUIStyle(EditorStyles.label);
            previewStyle.fontStyle = FontStyle.Bold;
            previewStyle.alignment = TextAnchor.MiddleCenter;
            previewStyle.normal.textColor = HierarchySeparatorEditor.GetContrastColor(previewColor);

            GUI.Label(previewRect, separator.gameObject.name, previewStyle);

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorApplication.RepaintHierarchyWindow();
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Rename Separator"))
            {
                EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                Selection.activeGameObject = separator.gameObject;
                EditorApplication.delayCall += () =>
                {
                    var window = EditorWindow.focusedWindow;
                    if (window != null)
                    {
                        Event e = new Event();
                        e.type = EventType.ValidateCommand;
                        e.commandName = "Rename";
                        window.SendEvent(e);

                        e.type = EventType.ExecuteCommand;
                        window.SendEvent(e);
                    }
                };
            }
        }
    }
}
