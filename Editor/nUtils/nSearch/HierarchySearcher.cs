using UnityEditor;
using UnityEngine;

public static class HierarchySearcher
{
    private static GameObject[] cachedSceneObjects;
    private static bool subscribed;

    private static Texture2D gameObjectIcon;
    private static Texture2D cameraIcon;
    private static Texture2D lightIcon;
    private static Texture2D rendererIcon;
    private static Texture2D particleIcon;
    private static bool iconsReady;

    /// <summary>
    /// Every GameObject in the loaded scenes, rebuilt only when the hierarchy actually
    /// changes rather than on every keystroke. Entries can be destroyed between
    /// invalidations, so callers must null-check.
    /// </summary>
    public static GameObject[] GetSceneObjects()
    {
        if (!subscribed)
        {
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.playModeStateChanged += _ => Invalidate();
            subscribed = true;
        }

        if (cachedSceneObjects == null)
            cachedSceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);

        return cachedSceneObjects;
    }

    public static void Invalidate()
    {
        cachedSceneObjects = null;
    }

    public static Texture2D GetHierarchyIcon(GameObject go)
    {
        EnsureIcons();

        if (go.GetComponent<Camera>())
            return cameraIcon;

        if (go.GetComponent<Light>())
            return lightIcon;

        if (go.GetComponent<MeshRenderer>() || go.GetComponent<SkinnedMeshRenderer>())
            return rendererIcon;

        if (go.GetComponent<ParticleSystem>())
            return particleIcon;

        return gameObjectIcon;
    }

    private static void EnsureIcons()
    {
        if (iconsReady)
            return;

        gameObjectIcon = EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D;
        cameraIcon = EditorGUIUtility.IconContent("Camera Icon").image as Texture2D;
        lightIcon = EditorGUIUtility.IconContent("Light Icon").image as Texture2D;
        rendererIcon = EditorGUIUtility.IconContent("MeshRenderer Icon").image as Texture2D;
        particleIcon = EditorGUIUtility.IconContent("ParticleSystem Icon").image as Texture2D;
        iconsReady = true;
    }
}
