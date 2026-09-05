using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public class nSearch : EditorWindow
{
    private enum ResultAction
    {
        Open,
        Ping,
        Reveal,
    }

    private string searchQuery = "";
    private readonly List<SearchResult> searchResults = new List<SearchResult>();
    private static nSearch currentWindow;
    private static double lastCloseTime = 0;
    private bool displaySettings = false;

    // The index is shared editor state, not window state: the window is recreated on
    // every toggle, and rebuilding a large project's index each time is what made
    // opening nSearch feel slow.
    private static readonly FileIndex sharedIndex = new FileIndex();
    private static IEnumerator<string> indexIterator;
    private static bool indexReady;
    private static bool indexBuilding;
    private static bool indexStale = true;
    private static bool indexIncludesPackages;

    // Searching is debounced so a fast typist runs one search, not one per character.
    private const long SearchDebounceMs = 50;
    private bool searchPending;
    private IVisualElementScheduledItem debounce;

    private int selectedIndex = 0;
    private string highlightQuery = "";
    private List<ISearchCommand> commands;

    // Layout
    private const float WindowWidth = 640f;
    private const float SearchRowHeight = 46f;
    private const float RowHeight = 38f;
    private const float FooterHeight = 24f;
    private const float ResultsPadding = 6f;
    private const float EmptyStateHeight = 60f;
    private const float SettingsHeight = 340f;
    private const float SettingsPadding = 14f;
    private const float MinWindowHeight = SearchRowHeight + 2f;
    private const float MaxWindowHeight = 560f;

    // Settings (persisted via EditorPrefs)
    private int maxVisibleResults = 8;
    private bool includePackages = false;
    private bool enableCalculator = true;
    private bool alsoSearchHierarchy = false;

    // UI
    private VisualElement root;
    private TextField field;
    private Button clearButton;
    private ScrollView scroll;
    private VisualElement emptyState;
    private Label emptyHint;
    private VisualElement settingsPanel;
    private VisualElement settingsContent;
    private Label statusLabel;
    private VisualElement footer;
    private Label countLabel;
    private VisualElement hints;
    private readonly List<VisualElement> rows = new List<VisualElement>();

    private static readonly string[] placeholderTexts = new[]
    {
        "Searching the galaxy...",
        "Looking through the multiverse...",
        "What are we hunting today?",
        "Find anything. Seriously, anything.",
        "Lost something? I got you.",
        "Your assets called, they miss you.",
        "Ctrl+Z won't help you find it...",
        "Faster than scrolling the hierarchy...",
        "Where did I put that prefab...",
        "It's not lost, it's just hiding.",
        "The answer is 42. But what was the question?",
        "Searching at the speed of light...",
        "Have you tried turning it off and on?",
        "I promise I won't judge your naming conventions.",
        "sin(my_assets) = found",
        "grep -r 'that thing I need'",
        "SELECT * FROM your_brain WHERE remember = true",
        "No Google needed. I'm right here.",
        "Ask and you shall receive.",
        "I've seen things you wouldn't believe...",
    };

    private string placeholderText = "Search or calculate...";

    [MenuItem("Window/nUtilities/nSearch %#SPACE")]
    public static void ToggleWindow()
    {
        double timeSinceClose = EditorApplication.timeSinceStartup - lastCloseTime;
        if (timeSinceClose < 0.2)
            return;

        if (currentWindow != null)
        {
            currentWindow.Close();
            currentWindow = null;
            lastCloseTime = EditorApplication.timeSinceStartup;
        }
        else
        {
            OpenWindow();
        }
    }

    static void OpenWindow()
    {
        currentWindow = CreateInstance<nSearch>();
        currentWindow.titleContent = new GUIContent("nSearch");
        currentWindow.placeholderText = placeholderTexts[UnityEngine.Random.Range(0, placeholderTexts.Length)];
        currentWindow.ShowPopup();

        currentWindow.minSize = new Vector2(WindowWidth, MinWindowHeight);
        currentWindow.maxSize = new Vector2(WindowWidth, MinWindowHeight);

        WindowUtilities.PositionForSpotlight(currentWindow, 0.26f);
    }

    void OnDestroy()
    {
        if (currentWindow == this)
            currentWindow = null;
    }

    void OnLostFocus()
    {
        Close();
    }

    void OnEnable()
    {
        LoadSettings();
        commands = new List<ISearchCommand> { new CreateCommand() };
        EnsureIndex();
    }

    // ------------------------------------------------------------------ UI setup

    void CreateGUI()
    {
        root = rootVisualElement;
        root.AddToClassList("nsearch-root");

        var sheet = LoadStyleSheet();
        if (sheet != null)
            root.styleSheets.Add(sheet);

        BuildSearchRow();
        BuildResults();
        BuildEmptyState();
        BuildSettings();
        BuildFooter();

        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        RefreshResults();

        // The popup has to hand focus to the field itself, and only once layout exists.
        root.schedule.Execute(() =>
        {
            field.Focus();
            field.SelectAll();
        }).StartingIn(0);
    }

    StyleSheet LoadStyleSheet()
    {
        // Resolve the sheet next to this script rather than hard-coding a package path,
        // so the package can be renamed, embedded or moved without breaking the styling.
        var script = MonoScript.FromScriptableObject(this);
        string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : null;

        if (!string.IsNullOrEmpty(scriptPath))
        {
            string dir = Path.GetDirectoryName(scriptPath);
            if (!string.IsNullOrEmpty(dir))
            {
                string ussPath = dir.Replace('\\', '/') + "/nSearchStyles.uss";
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
                if (sheet != null)
                    return sheet;
            }
        }

        Debug.LogWarning("nSearch: could not locate nSearchStyles.uss next to nSearch.cs; the window will render unstyled.");
        return null;
    }

    void BuildSearchRow()
    {
        var searchRow = new VisualElement();
        searchRow.AddToClassList("nsearch-search-row");

        var icon = new Image { scaleMode = ScaleMode.ScaleToFit };
        icon.AddToClassList("nsearch-search-icon");
        var iconContent = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Search Icon" : "Search Icon");
        if (iconContent != null)
            icon.image = iconContent.image;
        searchRow.Add(icon);

        field = new TextField();
        field.AddToClassList("nsearch-field");
        field.textEdition.placeholder = placeholderText;
        field.textEdition.hidePlaceholderOnFocus = false;
        field.RegisterValueChangedCallback(OnQueryChanged);
        searchRow.Add(field);

        clearButton = new Button(ClearQuery) { text = "×" };
        clearButton.AddToClassList("nsearch-clear");
        clearButton.style.display = DisplayStyle.None;
        searchRow.Add(clearButton);

        root.Add(searchRow);
    }

    void BuildResults()
    {
        scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("nsearch-scroll");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        root.Add(scroll);
    }

    void BuildEmptyState()
    {
        emptyState = new VisualElement();
        emptyState.AddToClassList("nsearch-empty");

        var title = new Label("No results found");
        title.AddToClassList("nsearch-empty__title");
        emptyState.Add(title);

        emptyHint = new Label("Try a different search term");
        emptyHint.AddToClassList("nsearch-empty__hint");
        emptyState.Add(emptyHint);

        root.Add(emptyState);
    }

    void BuildFooter()
    {
        footer = new VisualElement();
        footer.AddToClassList("nsearch-footer");

        countLabel = new Label();
        countLabel.AddToClassList("nsearch-footer__count");
        footer.Add(countLabel);

        hints = new VisualElement();
        hints.AddToClassList("nsearch-hints");
        footer.Add(hints);

        root.Add(footer);
    }

    static VisualElement MakeHint(string key, string label)
    {
        var hint = new VisualElement();
        hint.AddToClassList("nsearch-hint");

        var keyLabel = new Label(key);
        keyLabel.AddToClassList("nsearch-hint__key");
        hint.Add(keyLabel);

        var textLabel = new Label(label);
        textLabel.AddToClassList("nsearch-hint__label");
        hint.Add(textLabel);

        return hint;
    }

    // ------------------------------------------------------------------- typing

    void OnQueryChanged(ChangeEvent<string> evt)
    {
        searchQuery = evt.newValue;
        clearButton.style.display = string.IsNullOrEmpty(searchQuery) ? DisplayStyle.None : DisplayStyle.Flex;

        bool wasSettings = displaySettings;
        displaySettings = searchQuery.StartsWith("s:");
        selectedIndex = 0;

        if (displaySettings != wasSettings)
        {
            // Switching in or out of settings resizes the window, so don't defer it.
            searchPending = false;
            RunSearch();
        }
        else
        {
            ScheduleSearch();
        }
    }

    void ClearQuery()
    {
        field.SetValueWithoutNotify("");
        searchQuery = "";
        clearButton.style.display = DisplayStyle.None;
        displaySettings = false;
        selectedIndex = 0;
        searchPending = false;
        RunSearch();
        field.Focus();
    }

    void ScheduleSearch()
    {
        searchPending = true;

        if (debounce == null)
            debounce = root.schedule.Execute(() => { if (searchPending) RunSearch(); });

        debounce.ExecuteLater(SearchDebounceMs);
    }

    void FlushPendingSearch()
    {
        if (searchPending)
            RunSearch();
    }

    void RunSearch()
    {
        searchPending = false;
        PerformSearch();
        RefreshResults();
        ResizeWindow();
    }

    // ------------------------------------------------------------------ results

    void RefreshResults()
    {
        bool showSettings = displaySettings;
        bool showResults = !showSettings && searchResults.Count > 0;
        bool showEmpty = !showSettings && !showResults && !string.IsNullOrEmpty(searchQuery);

        settingsPanel.style.display = showSettings ? DisplayStyle.Flex : DisplayStyle.None;
        scroll.style.display = showResults ? DisplayStyle.Flex : DisplayStyle.None;
        emptyState.style.display = showEmpty ? DisplayStyle.Flex : DisplayStyle.None;
        footer.style.display = (showResults || showSettings) ? DisplayStyle.Flex : DisplayStyle.None;

        if (showSettings)
        {
            RefreshStatusLabel();
            countLabel.text = "Settings";
            hints.Clear();
            hints.Add(MakeHint("esc", "Close"));
            return;
        }

        if (showEmpty)
            emptyHint.text = indexReady ? "Try a different search term" : "Still indexing the project…";

        if (!showResults)
            return;

        EnsureRowCount(searchResults.Count);

        for (int i = 0; i < searchResults.Count; i++)
            BindRow(rows[i], searchResults[i], i);

        for (int i = searchResults.Count; i < rows.Count; i++)
            rows[i].style.display = DisplayStyle.None;

        if (selectedIndex >= searchResults.Count)
            selectedIndex = searchResults.Count - 1;
        if (selectedIndex < 0)
            selectedIndex = 0;

        UpdateSelection(false);

        countLabel.text = searchResults.Count == 1 ? "1 result" : searchResults.Count + " results";
    }

    void EnsureRowCount(int count)
    {
        while (rows.Count < count)
        {
            var row = MakeRow();
            rows.Add(row);
            scroll.Add(row);
        }
    }

    VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.AddToClassList("nsearch-row");

        var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
        icon.AddToClassList("nsearch-row__icon");
        row.Add(icon);

        var text = new VisualElement();
        text.AddToClassList("nsearch-row__text");

        var nameLabel = new Label { name = "name" };
        nameLabel.AddToClassList("nsearch-row__name");
        text.Add(nameLabel);

        var pathLabel = new Label { name = "path" };
        pathLabel.AddToClassList("nsearch-row__path");
        text.Add(pathLabel);

        row.Add(text);

        var badge = new Label { name = "badge" };
        badge.AddToClassList("nsearch-row__badge");
        row.Add(badge);

        row.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (row.userData is int index && index != selectedIndex)
            {
                selectedIndex = index;
                UpdateSelection(false);
            }
        });

        row.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.button != 0 || !(row.userData is int index))
                return;

            selectedIndex = index;
            UpdateSelection(false);
            Activate(index, evt.actionKey ? ResultAction.Reveal : evt.shiftKey ? ResultAction.Ping : ResultAction.Open);
            evt.StopPropagation();
        });

        return row;
    }

    void BindRow(VisualElement row, SearchResult result, int index)
    {
        row.style.display = DisplayStyle.Flex;
        row.userData = index;

        var icon = row.Q<Image>("icon");
        icon.image = result.Icon;
        icon.style.display = result.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;

        ApplyHighlightedName(row.Q<Label>("name"), result.Name, index == selectedIndex);

        row.Q<Label>("path").text = result.Path;

        var badge = row.Q<Label>("badge");
        string badgeText = BadgeFor(result);
        badge.text = badgeText;
        badge.style.display = string.IsNullOrEmpty(badgeText) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>
    /// Tints the matched run of the query inside the result name. Rich text is switched
    /// off for names containing '&lt;', which would otherwise be parsed as markup.
    /// </summary>
    void ApplyHighlightedName(Label label, string name, bool selected)
    {
        int at = string.IsNullOrEmpty(name) || string.IsNullOrEmpty(highlightQuery) || name.IndexOf('<') >= 0
            ? -1
            : name.IndexOf(highlightQuery, StringComparison.OrdinalIgnoreCase);

        if (at < 0)
        {
            label.enableRichText = false;
            label.text = name;
            return;
        }

        // The selected row sits on Unity's selection blue, where the accent would vanish.
        string accent = selected
            ? "#FFFFFF"
            : EditorGUIUtility.isProSkin ? "#6EA8FF" : "#1D4E89";

        label.enableRichText = true;
        label.text = name.Substring(0, at)
                     + "<color=" + accent + "><b>"
                     + name.Substring(at, highlightQuery.Length)
                     + "</b></color>"
                     + name.Substring(at + highlightQuery.Length);
    }

    static string BadgeFor(SearchResult result)
    {
        if (!string.IsNullOrEmpty(result.AssetPath))
        {
            if (AssetDatabase.IsValidFolder(result.AssetPath))
                return "FOLDER";

            string ext = Path.GetExtension(result.AssetPath);
            return string.IsNullOrEmpty(ext) ? "ASSET" : ext.TrimStart('.').ToUpperInvariant();
        }

        if (result.Target != null)
            return "SCENE";

        if (result.OnSelect != null)
            return result.ActionLabel == "Copy" ? "MATH" : "MENU";

        return string.Empty;
    }

    // ---------------------------------------------------------------- selection

    void UpdateSelection(bool ping)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            bool selected = i == selectedIndex;
            rows[i].EnableInClassList("nsearch-row--selected", selected);

            // Re-tint the match: it flips to white on the row that gains the blue fill.
            if (i < searchResults.Count)
                ApplyHighlightedName(rows[i].Q<Label>("name"), searchResults[i].Name, selected);
        }

        if (selectedIndex >= 0 && selectedIndex < rows.Count)
            scroll.ScrollTo(rows[selectedIndex]);

        UpdateHints();

        if (ping)
            PingSelectedResult();
    }

    void UpdateHints()
    {
        hints.Clear();

        if (selectedIndex < 0 || selectedIndex >= searchResults.Count)
        {
            hints.Add(MakeHint("esc", "Close"));
            return;
        }

        var result = searchResults[selectedIndex];
        hints.Add(MakeHint("↵", result.ActionLabel ?? "Open"));

        bool isAsset = !string.IsNullOrEmpty(result.AssetPath);
        bool isSceneObject = result.Target != null && !isAsset;

        if (isAsset || isSceneObject)
            hints.Add(MakeHint("⇧↵", "Ping"));

        if (isAsset)
            hints.Add(MakeHint(ActionKeyName + "↵", "Explorer"));

        hints.Add(MakeHint("esc", "Close"));
    }

    static string ActionKeyName
    {
        get { return Application.platform == RuntimePlatform.OSXEditor ? "⌘" : "Ctrl"; }
    }

    void NavigateSelection(int direction)
    {
        if (searchResults.Count == 0)
            return;

        int next = selectedIndex + direction;
        if (next < 0 || next >= searchResults.Count)
            return;

        selectedIndex = next;
        UpdateSelection(true);
    }

    void PingSelectedResult()
    {
        if (selectedIndex < 0 || selectedIndex >= searchResults.Count)
            return;

        var target = searchResults[selectedIndex].ResolveTarget();
        if (target != null)
            EditorGUIUtility.PingObject(target);
    }

    // ------------------------------------------------------------------ keyboard

    void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Space && evt.shiftKey && evt.actionKey)
        {
            lastCloseTime = EditorApplication.timeSinceStartup;
            Close();
            evt.StopPropagation();
            evt.PreventDefault();
            return;
        }

        if (evt.keyCode == KeyCode.Escape)
        {
            Close();
            evt.StopPropagation();
            evt.PreventDefault();
            return;
        }

        // In settings, Tab/arrows/Enter drive the controls; swallowing them for result
        // navigation is what left that panel keyboard-dead.
        if (displaySettings)
            return;

        switch (evt.keyCode)
        {
            case KeyCode.Tab:
                FlushPendingSearch();
                NavigateSelection(evt.shiftKey ? -1 : 1);
                break;

            case KeyCode.DownArrow:
                FlushPendingSearch();
                NavigateSelection(1);
                break;

            case KeyCode.UpArrow:
                FlushPendingSearch();
                NavigateSelection(-1);
                break;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                FlushPendingSearch();
                Activate(selectedIndex,
                    evt.actionKey ? ResultAction.Reveal :
                    evt.shiftKey ? ResultAction.Ping :
                    ResultAction.Open);
                break;

            default:
                return;
        }

        evt.StopPropagation();
        evt.PreventDefault();
    }

    // -------------------------------------------------------------- activation

    void Activate(int index, ResultAction action)
    {
        if (index < 0 || index >= searchResults.Count)
            return;

        var result = searchResults[index];

        if (result.OnSelect != null)
        {
            result.OnSelect();
            Close();
            return;
        }

        var target = result.ResolveTarget();
        if (target == null)
            return;

        switch (action)
        {
            case ResultAction.Reveal:
                string path = !string.IsNullOrEmpty(result.AssetPath)
                    ? result.AssetPath
                    : AssetDatabase.GetAssetPath(target);

                if (string.IsNullOrEmpty(path))
                    return; // A scene object has no file to reveal.

                EditorUtility.RevealInFinder(path);
                break;

            case ResultAction.Ping:
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
                break;

            case ResultAction.Open:
                if (!OpenTarget(result, target))
                    return; // User cancelled; leave the window up.
                break;
        }

        Close();
    }

    bool OpenTarget(SearchResult result, UnityEngine.Object target)
    {
        // Scene objects: select, ping, and frame them in the Scene view.
        if (string.IsNullOrEmpty(result.AssetPath))
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);

            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            return true;
        }

        // Folders have nothing to open, so reveal them in the Project window instead.
        if (AssetDatabase.IsValidFolder(result.AssetPath))
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            return true;
        }

        // Scenes replace what's loaded, so give the user the chance to save first.
        if (result.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EditorSceneManager.OpenScene(result.AssetPath);
            return true;
        }

        Selection.activeObject = target;
        EditorGUIUtility.PingObject(target);
        AssetDatabase.OpenAsset(target);
        return true;
    }

    // ------------------------------------------------------------------ settings

    void LoadSettings()
    {
        maxVisibleResults = EditorPrefs.GetInt("nSearch.MaxVisibleResults", 8);
        includePackages = EditorPrefs.GetBool("nSearch.IncludePackages", false);
        enableCalculator = EditorPrefs.GetBool("nSearch.EnableCalculator", true);
        alsoSearchHierarchy = EditorPrefs.GetBool("nSearch.AlsoSearchHierarchy", false);
    }

    void SaveSettings()
    {
        EditorPrefs.SetInt("nSearch.MaxVisibleResults", maxVisibleResults);
        EditorPrefs.SetBool("nSearch.IncludePackages", includePackages);
        EditorPrefs.SetBool("nSearch.EnableCalculator", enableCalculator);
        EditorPrefs.SetBool("nSearch.AlsoSearchHierarchy", alsoSearchHierarchy);
    }

    void BuildSettings()
    {
        settingsPanel = new VisualElement();
        settingsPanel.AddToClassList("nsearch-settings");

        settingsContent = new VisualElement();
        settingsContent.AddToClassList("nsearch-settings__content");
        settingsContent.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            if (displaySettings)
                ResizeWindow();
        });
        settingsPanel.Add(settingsContent);

        var title = new Label("Settings");
        title.AddToClassList("nsearch-settings__title");
        settingsContent.Add(title);

        var visible = new SliderInt("Max Visible Results", 4, 12)
        {
            value = maxVisibleResults,
            showInputField = true,
        };
        visible.tooltip = "How many results to show before scrolling";
        visible.RegisterValueChangedCallback(evt =>
        {
            maxVisibleResults = evt.newValue;
            SaveSettings();
            NoteSettingChange("Max Visible Results: " + evt.newValue);
        });
        settingsContent.Add(visible);

        settingsContent.Add(MakeSettingToggle(
            "Also Search Hierarchy",
            "Include scene objects in asset search results (no h: prefix needed)",
            alsoSearchHierarchy,
            value => alsoSearchHierarchy = value));

        settingsContent.Add(MakeSettingToggle(
            "Enable Calculator",
            "Evaluate math expressions like 2+2 or sin(45)",
            enableCalculator,
            value => enableCalculator = value));

        settingsContent.Add(MakeSettingToggle(
            "Include Packages",
            "Also index assets from the Packages folder",
            includePackages,
            value =>
            {
                includePackages = value;
                EnsureIndex();
                RefreshStatusLabel();
            }));

        settingsContent.Add(MakeRule());

        var rebuild = new Button(() =>
        {
            indexStale = true;
            EnsureIndex();
            RefreshStatusLabel();
        })
        { text = "Rebuild Index" };
        rebuild.AddToClassList("nsearch-rebuild");
        settingsContent.Add(rebuild);

        settingsContent.Add(MakeRule());

        var commandsTitle = new Label("Commands");
        commandsTitle.AddToClassList("nsearch-settings__section");
        settingsContent.Add(commandsTitle);

        settingsContent.Add(MakeHelpRow("s:", "Open settings"));
        settingsContent.Add(MakeHelpRow("h:", "Search scene hierarchy"));

        foreach (var cmd in commands)
            settingsContent.Add(MakeHelpRow(cmd.Prefix, cmd.Description));

        settingsContent.Add(MakeHelpRow("(math)", "Type any expression, e.g. sqrt(144)"));

        statusLabel = new Label();
        statusLabel.AddToClassList("nsearch-status");
        settingsContent.Add(statusLabel);

        settingsPanel.style.display = DisplayStyle.None;
        root.Add(settingsPanel);
    }

    /// <summary>
    /// A toggle that behaves the way Unity's IMGUI ones do. In UI Toolkit the label is a
    /// separate, inert element, so clicking the words - which is where the habit sends
    /// you - does nothing at all; this wires it up to the value.
    /// </summary>
    Toggle MakeSettingToggle(string label, string tooltip, bool value, Action<bool> apply)
    {
        var toggle = new Toggle(label) { value = value, tooltip = tooltip };

        toggle.labelElement.RegisterCallback<ClickEvent>(_ => toggle.value = !toggle.value);

        toggle.RegisterValueChangedCallback(evt =>
        {
            apply(evt.newValue);
            SaveSettings();
            NoteSettingChange(label + ": " + (evt.newValue ? "On" : "Off"));
        });

        return toggle;
    }

    /// <summary>
    /// Confirms a change in the footer. Settings take effect on the next search, so
    /// without this the panel gives no sign that anything happened.
    /// </summary>
    void NoteSettingChange(string message)
    {
        if (countLabel != null)
            countLabel.text = message;
    }

    static VisualElement MakeRule()
    {
        var rule = new VisualElement();
        rule.AddToClassList("nsearch-settings__rule");
        return rule;
    }

    static VisualElement MakeHelpRow(string prefix, string description)
    {
        var row = new VisualElement();
        row.AddToClassList("nsearch-help-row");

        var prefixLabel = new Label(prefix);
        prefixLabel.AddToClassList("nsearch-help-row__prefix");
        row.Add(prefixLabel);

        var descLabel = new Label(description);
        descLabel.AddToClassList("nsearch-help-row__desc");
        row.Add(descLabel);

        return row;
    }

    void RefreshStatusLabel()
    {
        if (statusLabel == null)
            return;

        statusLabel.text = indexReady
            ? "✓ Index ready — " + sharedIndex.Count + " assets"
            : "↻ Indexing…";
        statusLabel.style.color = indexReady
            ? new Color(0.3f, 0.8f, 0.3f)
            : new Color(0.8f, 0.7f, 0.2f);
    }

    // -------------------------------------------------------------------- window

    void ResizeWindow()
    {
        float contentHeight = MinWindowHeight;

        if (displaySettings)
        {
            // Measure the real content once it has laid out, so the panel doesn't leave
            // a slab of dead space below it.
            float measured = settingsContent != null ? settingsContent.resolvedStyle.height : 0f;
            float panelHeight = measured > 1f ? measured + SettingsPadding : SettingsHeight;
            contentHeight += panelHeight + FooterHeight;
        }
        else if (searchResults.Count > 0)
        {
            int visibleCount = Mathf.Min(searchResults.Count, maxVisibleResults);
            contentHeight += ResultsPadding + visibleCount * RowHeight + FooterHeight;
        }
        else if (!string.IsNullOrEmpty(searchQuery))
        {
            contentHeight += EmptyStateHeight;
        }

        float targetHeight = Mathf.Clamp(contentHeight, MinWindowHeight, MaxWindowHeight);
        minSize = new Vector2(WindowWidth, targetHeight);
        maxSize = new Vector2(WindowWidth, targetHeight);
    }

    // ------------------------------------------------------------------- indexing

    /// <summary>Marks the shared index for a rebuild the next time nSearch opens.</summary>
    internal static void InvalidateIndex()
    {
        indexStale = true;
    }

    /// <summary>
    /// The index maps asset names to paths, so only added, deleted and moved assets can
    /// invalidate it. Re-importing something already indexed - which is what saving a
    /// script does - leaves it perfectly valid.
    /// </summary>
    internal static void NotifyAssetsChanged(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (indexStale)
            return;

        if (deleted.Length > 0 || moved.Length > 0 || movedFrom.Length > 0)
        {
            indexStale = true;
            return;
        }

        for (int i = 0; i < imported.Length; i++)
        {
            if (!sharedIndex.ContainsPath(imported[i]))
            {
                indexStale = true;
                return;
            }
        }
    }

    void EnsureIndex()
    {
        bool scopeChanged = indexIncludesPackages != includePackages;

        if (!indexStale && !scopeChanged && (indexReady || indexBuilding))
            return;

        StartIndexBuild();
    }

    void StartIndexBuild()
    {
        // Safe whether or not a previous build is still running.
        EditorApplication.update -= IndexNextBatch;

        indexIncludesPackages = includePackages;
        indexStale = false;
        indexReady = false;
        indexBuilding = true;

        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        sharedIndex.Clear();
        sharedIndex.Reserve(allAssets.Length);
        indexIterator = ((IEnumerable<string>)allAssets).GetEnumerator();

        EditorApplication.update += IndexNextBatch;
    }

    /// <summary>
    /// Indexes on a time budget instead of a fixed item count, so a small project
    /// finishes in one tick and a huge one still leaves the editor responsive.
    /// </summary>
    static void IndexNextBatch()
    {
        const double budgetSeconds = 0.006;
        const int checkInterval = 512;

        double deadline = EditorApplication.timeSinceStartup + budgetSeconds;
        int sinceTimeCheck = 0;

        while (indexIterator.MoveNext())
        {
            string path = indexIterator.Current;

            if (path.StartsWith("Assets/", StringComparison.Ordinal) ||
                (indexIncludesPackages && path.StartsWith("Packages/", StringComparison.Ordinal)))
            {
                sharedIndex.Add(Path.GetFileNameWithoutExtension(path).ToLowerInvariant(), path);
            }

            if (++sinceTimeCheck >= checkInterval)
            {
                sinceTimeCheck = 0;
                if (EditorApplication.timeSinceStartup >= deadline)
                    return; // Resume on the next editor tick.
            }
        }

        EditorApplication.update -= IndexNextBatch;
        indexIterator = null;
        indexBuilding = false;
        indexReady = true;

        if (currentWindow != null && currentWindow.root != null)
        {
            currentWindow.RefreshStatusLabel();

            if (!string.IsNullOrEmpty(currentWindow.searchQuery))
                currentWindow.RunSearch();
        }
    }

    // -------------------------------------------------------------------- search

    void PerformSearch()
    {
        searchResults.Clear();
        highlightQuery = "";

        if (string.IsNullOrEmpty(searchQuery) || displaySettings)
            return;

        // Check registered commands
        foreach (var cmd in commands)
        {
            if (searchQuery.StartsWith(cmd.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                highlightQuery = searchQuery.Substring(cmd.Prefix.Length).Trim();
                cmd.GetResults(highlightQuery, searchResults);
                return;
            }
        }

        if (searchQuery.StartsWith("h:"))
        {
            highlightQuery = searchQuery.Substring(2).Trim();
            SearchHierarchy(highlightQuery);
            return;
        }

        // Math evaluation
        if (enableCalculator)
        {
            string mathResult = AdvancedMathEvaluator.EvaluateExpression(searchQuery);
            if (mathResult != null)
            {
                string value = mathResult;
                searchResults.Add(new SearchResult
                {
                    Name = mathResult,
                    Path = "= " + searchQuery,
                    Icon = EditorGUIUtility.IconContent("console.infoicon").image as Texture2D,
                    ActionLabel = "Copy",
                    OnSelect = () => EditorGUIUtility.systemCopyBuffer = value
                });
                return;
            }
        }

        highlightQuery = searchQuery;

        // File search. Icons come from the asset database's cache and the object itself
        // is loaded lazily, so a hundred hits cost a hundred dictionary lookups rather
        // than a hundred asset loads.
        var rankedResults = sharedIndex.Search(searchQuery.ToLowerInvariant());

        foreach (var path in rankedResults)
        {
            Texture2D icon = AssetDatabase.GetCachedIcon(path) as Texture2D;
            if (icon == null)
                icon = FileUtilities.GetFileTypeIcon(path);

            searchResults.Add(new SearchResult
            {
                Name = Path.GetFileName(path),
                Path = path,
                Icon = icon,
                AssetPath = path
            });
        }

        // Also search hierarchy if enabled
        if (alsoSearchHierarchy)
            SearchHierarchy(searchQuery);
    }

    void SearchHierarchy(string query)
    {
        if (string.IsNullOrEmpty(query))
            return;

        // Cached between keystrokes and invalidated on hierarchy changes, so a big scene
        // is walked once rather than re-collected for every character typed.
        GameObject[] sceneObjects = HierarchySearcher.GetSceneObjects();
        int added = 0;

        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject go = sceneObjects[i];

            // The cache can outlive an object destroyed without a hierarchy event.
            if (go == null)
                continue;

            if (go.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            searchResults.Add(new SearchResult
            {
                Name = go.name,
                Path = "Scene: " + go.scene.name,
                Icon = HierarchySearcher.GetHierarchyIcon(go),
                Target = go
            });

            // Counted separately from file results, which would otherwise have already
            // filled the list and starved hierarchy hits entirely.
            if (++added >= 50)
                break;
        }
    }
}

/// <summary>Keeps the shared nSearch asset index honest when the project changes.</summary>
internal class nSearchIndexWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        nSearch.NotifyAssetsChanged(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
    }
}
