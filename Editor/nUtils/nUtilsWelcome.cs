using UnityEditor;
using UnityEngine;

public class WelcomeWindow : EditorWindow
{
    private const string SHOW_ON_STARTUP_KEY = "nUtilities.ShowWelcomeOnStartup";
    private const string PACKAGE_NAME = "com.nutilities.editor";

    private static string Version
    {
        get
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WelcomeWindow).Assembly);
            return packageInfo != null ? packageInfo.version : "Unknown";
        }
    }

    private static bool showOnStartup = true;
    private Vector2 scrollPosition;

    [InitializeOnLoadMethod]
    private static void InitializeOnLoad()
    {
        EditorApplication.delayCall += ShowWindowOnStartup;
    }

    private static void ShowWindowOnStartup()
    {
        showOnStartup = EditorPrefs.GetBool(SHOW_ON_STARTUP_KEY, true);

        if (showOnStartup)
        {
            ShowWindow();
        }
    }

    [MenuItem("Window/nUtilities/Welcome")]
    public static void ShowWindow()
    {
        WelcomeWindow window = GetWindow<WelcomeWindow>(true, "Welcome to nUtilities", true);
        window.minSize = new Vector2(600, 550);
        window.maxSize = new Vector2(600, 550);
        window.Show();
    }

    private void OnEnable()
    {
        showOnStartup = EditorPrefs.GetBool(SHOW_ON_STARTUP_KEY, true);
    }

    private void OnGUI()
    {
        // Draw gradient background
        DrawGradientBackground();

        GUILayout.Space(15);

        // Header with icon
        DrawHeader();

        GUILayout.Space(10);

        // Scrollable content area
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(380));

        // Welcome message
        DrawWelcomeMessage();

        GUILayout.Space(15);

        // Utility cards
        DrawUtilityCard(
            "nSearch",
            "Quick access to everything in your project",
            "Fast, fuzzy search for finding files, hierarchy objects, and assets. " +
            "Press Ctrl+Shift+Space anywhere in Unity to open/close.",
            new Color(0.4f, 0.6f, 0.9f),
            () => EditorApplication.ExecuteMenuItem("Window/nUtilities/nSearch %#SPACE")
        );

        GUILayout.Space(10);

        DrawUtilityCard(
            "Scene Organizer",
            "Organize and manage your scenes with ease",
            "Create custom scene groups, organize scenes by project phase, and quickly load " +
            "multiple scenes. Perfect for managing large projects with many scenes.",
            new Color(0.5f, 0.7f, 0.4f),
            () => EditorApplication.ExecuteMenuItem("Window/nUtilities/Scene Organizer")
        );

        GUILayout.Space(10);

        DrawUtilityCard(
            "Favorites",
            "Keep your most-used assets at your fingertips",
            "Bookmark frequently used prefabs, scripts, materials, and more. Organize favorites " +
            "into groups with custom colors. Drag and drop support for quick access.",
            new Color(0.9f, 0.6f, 0.3f),
            () => EditorApplication.ExecuteMenuItem("Window/nUtilities/Favorites")
        );

        GUILayout.Space(10);

        DrawUtilityCard(
            "Hierarchy Organizer",
            "Keep your scene hierarchy clean and organized",
            "Create colored separator objects in your hierarchy to visually group GameObjects. " +
            "Quick presets for common categories like Gameplay, Environment, UI, and more.",
            new Color(0.7f, 0.4f, 0.7f),
            () => EditorApplication.ExecuteMenuItem("Window/nUtilities/Hierarchy Organizer Manager")
        );

        GUILayout.Space(15);

        // Support section
        DrawSupportSection();

        GUILayout.EndScrollView();

        GUILayout.FlexibleSpace();

        // Footer
        DrawFooter();
    }

    private void DrawGradientBackground()
    {
        Rect rect = new Rect(0, 0, position.width, position.height);
        Color topColor = EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.24f)
            : new Color(0.76f, 0.76f, 0.78f);
        Color bottomColor = EditorGUIUtility.isProSkin
            ? new Color(0.18f, 0.18f, 0.20f)
            : new Color(0.82f, 0.82f, 0.84f);

        DrawGradient(rect, topColor, bottomColor);
    }

    private void DrawGradient(Rect rect, Color topColor, Color bottomColor)
    {
        Texture2D texture = new Texture2D(1, 2);
        texture.SetPixel(0, 0, bottomColor);
        texture.SetPixel(0, 1, topColor);
        texture.Apply();
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill);
    }

    private void DrawHeader()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUILayout.BeginVertical();

        // Title with shadow effect
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 28,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.3f, 0.7f, 1.0f) },
            fontStyle = FontStyle.Bold
        };

        GUILayout.Label("nUtilities", titleStyle);

        // Subtitle
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f) },
            fontStyle = FontStyle.Italic
        };

        GUILayout.Label("Your Unity Workflow Supercharger", subtitleStyle);

        GUILayout.Space(3);

        // Version badge
        GUIStyle versionStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };

        GUILayout.Label($"v{Version}", versionStyle);

        GUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawWelcomeMessage()
    {
        GUIStyle messageBoxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(15, 15, 12, 12),
            margin = new RectOffset(20, 20, 0, 0)
        };

        GUILayout.BeginVertical(messageBoxStyle);

        GUIStyle messageStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.2f, 0.2f, 0.2f) }
        };

        GUILayout.Label(
            "Welcome! nUtilities is a collection of powerful editor tools designed to boost your productivity " +
            "and streamline your Unity workflow. Click the buttons below to explore each utility.",
            messageStyle
        );

        GUILayout.EndVertical();
    }

    private void DrawUtilityCard(string title, string subtitle, string description, Color accentColor, System.Action onLaunch)
    {
        // Card background
        GUIStyle cardStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(15, 15, 12, 12),
            margin = new RectOffset(20, 20, 0, 0)
        };

        GUILayout.BeginVertical(cardStyle);

        // Accent bar
        Rect accentRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(3), GUILayout.ExpandWidth(true));
        accentRect.width += 30;
        accentRect.x -= 15;
        EditorGUI.DrawRect(accentRect, accentColor);

        GUILayout.Space(8);

        // Title
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            normal = { textColor = accentColor },
            fontStyle = FontStyle.Bold
        };

        GUILayout.Label(title, titleStyle);

        GUILayout.Space(2);

        // Subtitle
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Italic,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f) }
        };

        GUILayout.Label(subtitle, subtitleStyle);

        GUILayout.Space(6);

        // Description
        GUIStyle descStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = 11,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.25f, 0.25f, 0.25f) }
        };

        GUILayout.Label(description, descStyle);

        GUILayout.Space(8);

        // Launch button
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = accentColor;

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            fixedHeight = 28
        };

        if (GUILayout.Button("Open Utility", buttonStyle))
        {
            onLaunch?.Invoke();
        }

        GUI.backgroundColor = originalColor;

        GUILayout.EndVertical();
    }

    private void DrawSupportSection()
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(15, 15, 12, 12),
            margin = new RectOffset(20, 20, 0, 0)
        };

        GUILayout.BeginVertical(boxStyle);

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.5f, 0.8f, 1.0f) }
        };

        GUILayout.Label("Support & Resources", headerStyle);

        GUILayout.Space(8);

        // Buttons row
        GUILayout.BeginHorizontal();

        // Website button
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.7f, 1.0f);

        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            fixedHeight = 32,
            alignment = TextAnchor.MiddleCenter
        };

        if (GUILayout.Button("Website\nnativvstudios.com", buttonStyle))
        {
            Application.OpenURL("https://nativvstudios.com");
        }

        GUILayout.Space(8);

        // GitHub button
        GUI.backgroundColor = new Color(0.4f, 0.4f, 0.45f);

        if (GUILayout.Button("GitHub\ngithub.com/nativvstudios", buttonStyle))
        {
            Application.OpenURL("https://github.com/nativvstudios");
        }

        GUI.backgroundColor = originalColor;

        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUIStyle contactStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.45f, 0.45f, 0.45f) }
        };

        GUILayout.Label("For support, bug reports, or feature requests, visit the links above", contactStyle);

        GUILayout.EndVertical();
    }

    private void DrawFooter()
    {
        DrawUILine(EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.6f, 0.6f, 0.6f), 1, 5);

        GUILayout.Space(8);

        // Show on startup toggle
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUIStyle toggleStyle = new GUIStyle(EditorStyles.toggle)
        {
            fontSize = 11
        };

        bool newShowOnStartup = GUILayout.Toggle(showOnStartup, " Show this window on startup", toggleStyle);
        if (newShowOnStartup != showOnStartup)
        {
            showOnStartup = newShowOnStartup;
            EditorPrefs.SetBool(SHOW_ON_STARTUP_KEY, showOnStartup);
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
    }

    private void DrawUILine(Color color, int thickness = 1, int padding = 10)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2f;
        r.x = 0;
        r.width = position.width;
        EditorGUI.DrawRect(r, color);
    }
}
