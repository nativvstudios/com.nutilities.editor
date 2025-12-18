using UnityEngine;

namespace nUtils.HierarchyOrganizer
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class HierarchySeparator : MonoBehaviour
    {
        public enum SeparatorColor
        {
            Blue,
            Green,
            Orange,
            Purple,
            Red,
            Cyan,
            Yellow,
            Custom
        }

        [SerializeField]
        private SeparatorColor separatorColor = SeparatorColor.Blue;

        [SerializeField]
        private Color customColor = new Color(0.3f, 0.5f, 0.8f, 1f);

        public Color GetColor()
        {
            switch (separatorColor)
            {
                case SeparatorColor.Blue:
                    return new Color(0.3f, 0.5f, 0.8f, 1f);
                case SeparatorColor.Green:
                    return new Color(0.5f, 0.7f, 0.4f, 1f);
                case SeparatorColor.Orange:
                    return new Color(0.9f, 0.6f, 0.3f, 1f);
                case SeparatorColor.Purple:
                    return new Color(0.7f, 0.4f, 0.7f, 1f);
                case SeparatorColor.Red:
                    return new Color(0.8f, 0.3f, 0.4f, 1f);
                case SeparatorColor.Cyan:
                    return new Color(0.4f, 0.7f, 0.7f, 1f);
                case SeparatorColor.Yellow:
                    return new Color(0.9f, 0.8f, 0.3f, 1f);
                case SeparatorColor.Custom:
                    return customColor;
                default:
                    return new Color(0.3f, 0.5f, 0.8f, 1f);
            }
        }

        public SeparatorColor ColorPreset
        {
            get { return separatorColor; }
            set { separatorColor = value; }
        }

        public Color CustomColor
        {
            get { return customColor; }
            set { customColor = value; }
        }

        private void Awake()
        {
            // Hide all components in the inspector except this one
            hideFlags = HideFlags.None;
        }

        private void OnValidate()
        {
            // Update hierarchy when color changes
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.RepaintHierarchyWindow();
            #endif
        }
    }
}
