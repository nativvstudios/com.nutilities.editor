using UnityEngine;

public static class SceneOrganizerConstants
{
    // Paths
    public const string ASSET_PATH = "Assets/Editor/SceneGroupData.asset";

    /// <summary>
    /// Where backups go unless the user picks a folder: the project's UserSettings, which
    /// Unity does not import. Backups inside Assets became assets themselves, each one
    /// imported (and a project change) as it was written.
    /// </summary>
    public static string DefaultBackupDirectory =>
        System.IO.Path.GetFullPath("UserSettings/nUtilities/SceneOrganizerBackups").Replace('\\', '/');

    /// <summary>True for a folder inside the project's Assets folder.</summary>
    public static bool IsInsideAssets(string directory)
    {
        if (string.IsNullOrEmpty(directory))
            return false;

        string assets = System.IO.Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
        string full = System.IO.Path.GetFullPath(directory).Replace('\\', '/').TrimEnd('/');
        return full.Equals(assets, System.StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(assets + "/", System.StringComparison.OrdinalIgnoreCase);
    }

    // EditorPrefs keys for the Create Scene window.
    public const string PREF_NEW_SCENE_FOLDER = "SceneOrganizer_NewSceneFolder";

    // EditorPrefs Keys
    public const string PREF_ENABLE_BACKUP = "SceneOrganizer_EnableBackup";
    public const string PREF_BACKUP_DIR = "SceneOrganizer_BackupDirectory";
    public const string PREF_SCENE_AREA_HEIGHT = "SceneOrganizer_SceneAreaHeight";
    public const string PREF_SHOW_RECENT = "SceneOrganizer_ShowRecent";

    // Limits
    public const int MAX_GROUP_NAME_LENGTH = 50;
    public const int MAX_BACKUP_COPIES = 5;
    public const float DOUBLE_CLICK_THRESHOLD = 0.3f;
    public const float DRAG_THRESHOLD = 10f;

    // UI Sizes
    public const float MIN_SCENE_AREA_HEIGHT = 100f;
    public const float DEFAULT_SCENE_AREA_HEIGHT = 200f;
    public const float RESIZE_HANDLE_HEIGHT = 5f;
    public const float BUTTON_WIDTH_SMALL = 30f;
    public const float BUTTON_WIDTH_MEDIUM = 60f;
    public const float BUTTON_WIDTH_LARGE = 70f;

    // Colors
    public static readonly Color HIGHLIGHT_COLOR = new Color(0.24f, 0.49f, 0.91f);
    public static readonly Color DROP_TARGET_COLOR = new Color(0.24f, 0.49f, 0.91f, 0.25f);
    public static readonly Color WATERMARK_COLOR = new Color(0.5f, 0.5f, 0.5f, 0.8f);
}