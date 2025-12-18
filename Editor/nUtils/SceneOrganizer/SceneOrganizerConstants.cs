using UnityEngine;

public static class SceneOrganizerConstants
{
    // Paths
    public const string ASSET_PATH = "Assets/Editor/SceneGroupData.asset";

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