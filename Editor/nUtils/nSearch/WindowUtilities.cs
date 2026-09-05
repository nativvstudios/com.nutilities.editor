using UnityEditor;
using UnityEngine;

public static class WindowUtilities
{
    public static void CenterWindow(EditorWindow window)
    {
        PositionForSpotlight(window, 0.5f);
    }

    /// <summary>
    /// Horizontally centred on the main editor window, with the top edge placed at
    /// <paramref name="verticalFraction"/> of its height. Launchers sit above the middle
    /// so the results have somewhere to grow into.
    /// </summary>
    public static void PositionForSpotlight(EditorWindow window, float verticalFraction)
    {
        Rect mainRect = EditorGUIUtility.GetMainWindowPosition();

        float x = mainRect.x + (mainRect.width - window.position.width) * 0.5f;
        float y = mainRect.y + mainRect.height * Mathf.Clamp01(verticalFraction);

        window.position = new Rect(x, y, window.position.width, window.position.height);
    }
}
