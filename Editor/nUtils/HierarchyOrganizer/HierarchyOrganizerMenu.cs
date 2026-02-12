using UnityEngine;
using UnityEditor;

namespace nUtils.HierarchyOrganizer
{
    public static class HierarchyOrganizerMenu
    {
        [MenuItem("GameObject/nUtilities/Create Separator", false, 0)]
        private static void OpenCreationWindow()
        {
            CreateSeparatorWindow.ShowWindow();
        }

        [MenuItem("Window/nUtilities/Hierarchy Organizer Manager")]
        private static void OpenManagerWindow()
        {
            HierarchyOrganizerWindow.ShowWindow();
        }

        // Context menu in hierarchy
        [MenuItem("GameObject/Convert to Separator", false, 0)]
        private static void ConvertToSeparator()
        {
            if (Selection.activeGameObject != null)
            {
                GameObject obj = Selection.activeGameObject;

                // Check if it already has the component
                if (obj.GetComponent<HierarchySeparator>() == null)
                {
                    Undo.AddComponent<HierarchySeparator>(obj);
                    EditorUtility.SetDirty(obj);
                }
            }
        }

        [MenuItem("GameObject/Convert to Separator", true)]
        private static bool ConvertToSeparatorValidate()
        {
            return Selection.activeGameObject != null &&
                   Selection.activeGameObject.GetComponent<HierarchySeparator>() == null;
        }

        // Public method for creating separators programmatically
        public static void CreateSeparator(string name, HierarchySeparator.SeparatorColor color)
        {
            GameObject separator = new GameObject(name);

            // Add the separator component
            HierarchySeparator separatorComponent = separator.AddComponent<HierarchySeparator>();
            separatorComponent.ColorPreset = color;

            // Position in hierarchy
            if (Selection.activeTransform != null)
            {
                separator.transform.SetParent(Selection.activeTransform.parent);
                separator.transform.SetSiblingIndex(Selection.activeTransform.GetSiblingIndex() + 1);
            }

            // Reset transform
            separator.transform.localPosition = Vector3.zero;
            separator.transform.localRotation = Quaternion.identity;
            separator.transform.localScale = Vector3.one;

            // Register undo
            Undo.RegisterCreatedObjectUndo(separator, "Create Hierarchy Separator");

            // Select the new separator
            Selection.activeGameObject = separator;

            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
