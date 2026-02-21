using UnityEditor;
using UnityEngine;

public static class HierarchySearcher
{
    public static Texture2D GetHierarchyIcon(GameObject go)
    {
        string iconName = "GameObject Icon";

        if (go.GetComponent<Camera>())
        {
            iconName = "Camera Icon";
        }
        else if (go.GetComponent<Light>())
        {
            iconName = "Light Icon";
        }
        else if (go.GetComponent<MeshRenderer>() || go.GetComponent<SkinnedMeshRenderer>())
        {
            iconName = "MeshRenderer Icon";
        }
        else if (go.GetComponent<ParticleSystem>())
        {
            iconName = "ParticleSystem Icon";
        }

        return EditorGUIUtility.IconContent(iconName).image as Texture2D;
    }
}
