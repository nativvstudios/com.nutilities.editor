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

            if (Event.current.type != EventType.Repaint)
                return;

            // Check if this object is a child of a separator (and not a separator itself)
            if (separator == null)
            {
                DrawChildOutlineIfUnderSeparator(obj, selectionRect);
                return;
            }

            Color backgroundColor = separator.GetColor();
            bool hasChildren = obj.transform.childCount > 0;

            // Calculate the full width of the hierarchy window
            float fullWidth = selectionRect.x + selectionRect.width + 16;

            // Foldout area position
            float foldoutStart = selectionRect.x - 14;
            float foldoutWidth = 14;

            if (hasChildren)
            {
                // Draw background in three parts: left of foldout, semi-transparent foldout area, right of foldout

                // Left portion: from edge to foldout
                Rect leftRect = new Rect(0, selectionRect.y, foldoutStart, selectionRect.height);
                EditorGUI.DrawRect(leftRect, backgroundColor);

                // Foldout area: semi-transparent so Unity's animated arrow shows through
                Rect foldoutRect = new Rect(foldoutStart, selectionRect.y, foldoutWidth, selectionRect.height);
                Color foldoutColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.4f);
                EditorGUI.DrawRect(foldoutRect, foldoutColor);

                // Right portion: from after foldout to end
                Rect rightRect = new Rect(selectionRect.x, selectionRect.y, fullWidth - selectionRect.x, selectionRect.height);
                EditorGUI.DrawRect(rightRect, backgroundColor);
            }
            else
            {
                // No children - draw full width background
                Rect bgRect = new Rect(0, selectionRect.y, fullWidth, selectionRect.height);
                EditorGUI.DrawRect(bgRect, backgroundColor);
            }

            // Draw the GameObject icon on top
            Texture2D icon = AssetPreview.GetMiniThumbnail(obj);
            if (icon != null)
            {
                Rect iconRect = new Rect(selectionRect.x, selectionRect.y, 16, 16);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }

            // Create style for the text
            GUIStyle textStyle = new GUIStyle(EditorStyles.label);
            textStyle.fontStyle = FontStyle.Bold;
            textStyle.alignment = TextAnchor.MiddleCenter;
            textStyle.normal.textColor = GetContrastColor(backgroundColor);

            // Draw the separator name centered across the full width
            Rect labelRect = new Rect(0, selectionRect.y, fullWidth, selectionRect.height);

            GUI.Label(labelRect, obj.name, textStyle);
        }

        private static void DrawChildOutlineIfUnderSeparator(GameObject obj, Rect selectionRect)
        {
            // Look for a parent with HierarchySeparator component
            Transform parent = obj.transform.parent;
            HierarchySeparator parentSeparator = null;

            while (parent != null)
            {
                parentSeparator = parent.GetComponent<HierarchySeparator>();
                if (parentSeparator != null)
                    break;
                parent = parent.parent;
            }

            if (parentSeparator == null)
                return;

            // Check if the parent separator is expanded
            int parentInstanceID = parentSeparator.gameObject.GetInstanceID();
            bool isExpanded = IsGameObjectExpanded(parentInstanceID);

            if (!isExpanded)
                return;

            // Draw outline around this child object
            Color outlineColor = parentSeparator.GetColor();
            float outlineThickness = 1f;

            // Calculate the full width
            float fullWidth = selectionRect.x + selectionRect.width + 16;

            // Draw outline as 4 rects (top, bottom, left, right)
            Rect topRect = new Rect(0, selectionRect.y, fullWidth, outlineThickness);
            Rect bottomRect = new Rect(0, selectionRect.yMax - outlineThickness, fullWidth, outlineThickness);
            Rect leftRect = new Rect(0, selectionRect.y, outlineThickness, selectionRect.height);
            Rect rightRect = new Rect(fullWidth - outlineThickness, selectionRect.y, outlineThickness, selectionRect.height);

            EditorGUI.DrawRect(topRect, outlineColor);
            EditorGUI.DrawRect(bottomRect, outlineColor);
            EditorGUI.DrawRect(leftRect, outlineColor);
            EditorGUI.DrawRect(rightRect, outlineColor);
        }

        private static bool IsGameObjectExpanded(int instanceID)
        {
            // Use reflection to check if the GameObject is expanded in the hierarchy
            var windows = Resources.FindObjectsOfTypeAll<SearchableEditorWindow>();
            foreach (var window in windows)
            {
                if (window.GetType().Name == "SceneHierarchyWindow")
                {
                    var sceneHierarchy = window.GetType()
                        .GetProperty("sceneHierarchy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                        .GetValue(window);

                    if (sceneHierarchy != null)
                    {
                        var treeView = sceneHierarchy.GetType()
                            .GetProperty("treeView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                            .GetValue(sceneHierarchy);

                        if (treeView != null)
                        {
                            var data = treeView.GetType()
                                .GetProperty("data", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?
                                .GetValue(treeView);

                            if (data != null)
                            {
                                var isExpandedMethod = data.GetType()
                                    .GetMethod("IsExpanded", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new[] { typeof(int) }, null);

                                if (isExpandedMethod != null)
                                {
                                    return (bool)isExpandedMethod.Invoke(data, new object[] { instanceID });
                                }
                            }
                        }
                    }
                    break;
                }
            }
            return false;
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
