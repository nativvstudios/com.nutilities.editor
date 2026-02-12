using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class nSearch : EditorWindow
{
    private string searchQuery = "";
    private Vector2 scrollPosition;
    private List<SearchResult> searchResults = new List<SearchResult>();
    private TrieNode root = new TrieNode();
    private static nSearch currentWindow;
    private static double lastCloseTime = 0;
    private bool indexingComplete = false;
    private IEnumerator<string> indexIterator = null;
    private bool displaySettings = false;
    private int selectedIndex = 0;
    private bool keyboardNavigationActive = false;

    // Layout
    private readonly float windowWidth = 600f;
    private readonly float minWindowHeight = 62f;
    private readonly float maxWindowHeight = 500f;
    private readonly float itemHeight = 48f;

    // Settings (persisted via EditorPrefs)
    private int maxVisibleResults = 8;
    private bool includePackages = false;
    private bool enableCalculator = true;
    private bool alsoSearchHierarchy = false;

    // Cached styles
    private GUIStyle _searchStyle;
    private GUIStyle _placeholderStyle;
    private GUIStyle _clearBtnStyle;
    private GUIStyle _resultNameStyle;
    private GUIStyle _resultPathStyle;
    private GUIStyle _noResultsStyle;
    private GUIStyle _noResultsHintStyle;
    private GUIStyle _footerStyle;
    private bool _stylesReady;

    // Cached icon
    private Texture _searchIcon;

    // Placeholder text
    private string _placeholderText = "Search or calculate...";
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
        currentWindow._placeholderText = placeholderTexts[UnityEngine.Random.Range(0, placeholderTexts.Length)];
        currentWindow.ShowPopup();

        float height = currentWindow.minWindowHeight;
        currentWindow.minSize = new Vector2(currentWindow.windowWidth, height);
        currentWindow.maxSize = new Vector2(currentWindow.windowWidth, height);

        WindowUtilities.CenterWindow(currentWindow);
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
        if (!indexingComplete && indexIterator == null)
            BuildFileIndex();
    }

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

    void EnsureStyles()
    {
        if (_stylesReady) return;

        // Try loading Unity's built-in search icon
        var iconContent = EditorGUIUtility.IconContent("d_Search Icon");
        if (iconContent != null && iconContent.image != null)
            _searchIcon = iconContent.image;
        if (_searchIcon == null)
        {
            iconContent = EditorGUIUtility.IconContent("Search Icon");
            if (iconContent != null) _searchIcon = iconContent.image;
        }

        _searchStyle = new GUIStyle(EditorStyles.textField)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 36,
            padding = new RectOffset(32, 28, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
        };

        _placeholderStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.4f) },
        };

        _clearBtnStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.6f) },
            hover = { textColor = new Color(0.7f, 0.7f, 0.7f, 0.9f) },
            padding = new RectOffset(0, 0, 0, 2),
        };

        _resultNameStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            normal = { textColor = EditorGUIUtility.isProSkin
                ? new Color(0.88f, 0.88f, 0.88f)
                : new Color(0.1f, 0.1f, 0.1f) },
        };

        _resultPathStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            normal = { textColor = EditorGUIUtility.isProSkin
                ? new Color(0.5f, 0.5f, 0.5f)
                : new Color(0.45f, 0.45f, 0.45f) },
        };

        _noResultsStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            normal = { textColor = EditorGUIUtility.isProSkin
                ? new Color(0.45f, 0.45f, 0.45f)
                : new Color(0.5f, 0.5f, 0.5f) },
        };

        _noResultsHintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            normal = { textColor = EditorGUIUtility.isProSkin
                ? new Color(0.38f, 0.38f, 0.38f)
                : new Color(0.55f, 0.55f, 0.55f) },
        };

        _footerStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            normal = { textColor = EditorGUIUtility.isProSkin
                ? new Color(0.45f, 0.45f, 0.45f)
                : new Color(0.5f, 0.5f, 0.5f) },
        };

        _stylesReady = true;
    }

    void OnGUI()
    {
        EnsureStyles();
        HandleKeyboardInput();

        // Window background
        Rect bgRect = new Rect(0, 0, position.width, position.height);
        Color windowBg = EditorGUIUtility.isProSkin
            ? new Color(0.18f, 0.18f, 0.18f, 0.99f)
            : new Color(0.92f, 0.92f, 0.92f, 0.99f);
        EditorGUI.DrawRect(bgRect, windowBg);

        // 1px border around the popup
        DrawWindowBorder();

        GUILayout.BeginVertical();

        // Search bar area
        DrawSearchBar();

        // Content area
        if (displaySettings)
        {
            DrawSettings();
        }
        else if (searchResults.Count > 0)
        {
            DrawSeparatorLine();
            DrawResults();
        }
        else if (!string.IsNullOrEmpty(searchQuery))
        {
            DrawSeparatorLine();
            DrawNoResults();
        }

        GUILayout.EndVertical();
    }

    void DrawWindowBorder()
    {
        Color border = EditorGUIUtility.isProSkin
            ? new Color(0.08f, 0.08f, 0.08f, 0.9f)
            : new Color(0.55f, 0.55f, 0.55f, 0.9f);

        float w = position.width;
        float h = position.height;

        EditorGUI.DrawRect(new Rect(0, 0, w, 1), border);
        EditorGUI.DrawRect(new Rect(0, h - 1, w, 1), border);
        EditorGUI.DrawRect(new Rect(0, 0, 1, h), border);
        EditorGUI.DrawRect(new Rect(w - 1, 0, 1, h), border);
    }

    void DrawSearchBar()
    {
        // Search area background (slightly lighter/darker than window)
        float searchAreaHeight = 54f;
        Rect searchBgRect = new Rect(1, 1, position.width - 2, searchAreaHeight);
        Color searchBg = EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 1f)
            : new Color(0.96f, 0.96f, 0.96f, 1f);
        EditorGUI.DrawRect(searchBgRect, searchBg);

        GUILayout.Space(9);

        GUILayout.BeginHorizontal();
        GUILayout.Space(10);

        EditorGUI.BeginChangeCheck();
        GUI.SetNextControlName("SearchField");
        searchQuery = GUILayout.TextField(searchQuery, _searchStyle, GUILayout.ExpandWidth(true));
        Rect searchRect = GUILayoutUtility.GetLastRect();

        // Search icon
        if (_searchIcon != null)
        {
            Rect iconRect = new Rect(searchRect.x + 7, searchRect.y + 9, 18, 18);
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.DrawTexture(iconRect, _searchIcon, ScaleMode.ScaleToFit);
            GUI.color = prevColor;
        }

        // Placeholder text
        if (string.IsNullOrEmpty(searchQuery))
        {
            Rect placeholderRect = new Rect(
                searchRect.x + 32, searchRect.y, searchRect.width - 60, searchRect.height);
            GUI.Label(placeholderRect, _placeholderText, _placeholderStyle);
        }

        // Clear button
        if (!string.IsNullOrEmpty(searchQuery))
        {
            Rect clearRect = new Rect(searchRect.xMax - 26, searchRect.y, 24, searchRect.height);
            EditorGUIUtility.AddCursorRect(clearRect, MouseCursor.Link);

            if (GUI.Button(clearRect, "\u00d7", _clearBtnStyle))
            {
                searchQuery = "";
                GUI.FocusControl("SearchField");
                selectedIndex = 0;
                scrollPosition = Vector2.zero;
                PerformSearch();
                ResizeWindow();
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            displaySettings = searchQuery.StartsWith("s:");
            selectedIndex = 0;
            scrollPosition = Vector2.zero;
            PerformSearch();
            ResizeWindow();
        }

        GUILayout.Space(10);
        GUILayout.EndHorizontal();

        // Auto-focus
        if (Event.current.type == EventType.Layout)
        {
            if (GUI.GetNameOfFocusedControl() != "SearchField")
            {
                GUI.FocusControl("SearchField");
                Repaint();
            }
        }

        GUILayout.Space(8);
    }

    void DrawSeparatorLine()
    {
        Rect sepRect = GUILayoutUtility.GetRect(position.width, 1);
        Color sepColor = EditorGUIUtility.isProSkin
            ? new Color(0.10f, 0.10f, 0.10f, 0.8f)
            : new Color(0.72f, 0.72f, 0.72f, 0.6f);
        EditorGUI.DrawRect(sepRect, sepColor);
        GUILayout.Space(4);
    }

    void DrawResults()
    {
        float topHeight = 54f + 1f + 4f + 8f; // search area + sep + gap + space
        float footerHeight = 24f;
        float scrollViewHeight = position.height - topHeight - footerHeight;

        Rect scrollRect = GUILayoutUtility.GetRect(position.width, scrollViewHeight);
        float contentHeight = searchResults.Count * itemHeight;
        Rect contentRect = new Rect(0, 0, scrollRect.width - 14, contentHeight);

        scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, contentRect);

        for (int i = 0; i < searchResults.Count; i++)
        {
            Rect itemRect = new Rect(0, i * itemHeight, contentRect.width, itemHeight);
            DrawResultItem(searchResults[i], i, itemRect);
        }

        GUI.EndScrollView();
        DrawFooter();
    }

    void DrawResultItem(SearchResult result, int index, Rect rect)
    {
        Event e = Event.current;
        bool isHovered = rect.Contains(e.mousePosition - scrollPosition);

        if (isHovered && e.type == EventType.MouseMove)
        {
            selectedIndex = index;
            keyboardNavigationActive = false;
            Repaint();
        }

        // Subtle separator between items
        if (index > 0)
        {
            Rect sepRect = new Rect(rect.x + 10, rect.y, rect.width - 20, 1);
            Color sepColor = EditorGUIUtility.isProSkin
                ? new Color(0.14f, 0.14f, 0.14f, 0.4f)
                : new Color(0.72f, 0.72f, 0.72f, 0.25f);
            EditorGUI.DrawRect(sepRect, sepColor);
        }

        // Selection highlight
        if (index == selectedIndex)
        {
            Rect highlightRect = new Rect(rect.x + 5, rect.y + 2, rect.width - 10, rect.height - 4);
            Color selColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.50f, 0.90f, 0.50f)
                : new Color(0.23f, 0.50f, 0.87f, 0.30f);
            EditorGUI.DrawRect(highlightRect, selColor);
        }

        // Click
        if (e.type == EventType.MouseDown && e.button == 0 && isHovered)
        {
            SelectResult(result);
            e.Use();
            return;
        }

        // Icon
        Rect iconRect = new Rect(rect.x + 14, rect.y + 6, 36, 36);
        if (result.Icon != null)
            GUI.DrawTexture(iconRect, result.Icon, ScaleMode.ScaleToFit);

        // Name
        Rect labelRect = new Rect(rect.x + 58, rect.y + 6, rect.width - 70, 20);
        GUI.Label(labelRect, result.Name, _resultNameStyle);

        // Path
        Rect pathRect = new Rect(rect.x + 58, rect.y + 26, rect.width - 70, 16);
        GUI.Label(pathRect, result.Path, _resultPathStyle);
    }

    void DrawNoResults()
    {
        GUILayout.FlexibleSpace();
        GUILayout.Label("No results found", _noResultsStyle);
        GUILayout.Label("Try a different search term", _noResultsHintStyle);
        GUILayout.FlexibleSpace();
    }

    void DrawFooter()
    {
        // Separator
        Rect sepRect = new Rect(0, position.height - 24, position.width, 1);
        Color sepColor = EditorGUIUtility.isProSkin
            ? new Color(0.10f, 0.10f, 0.10f, 0.7f)
            : new Color(0.72f, 0.72f, 0.72f, 0.5f);
        EditorGUI.DrawRect(sepRect, sepColor);

        // Footer background
        Rect footerBg = new Rect(0, position.height - 23, position.width, 23);
        Color footerBgColor = EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.16f, 0.16f, 1f)
            : new Color(0.88f, 0.88f, 0.88f, 1f);
        EditorGUI.DrawRect(footerBg, footerBgColor);

        // Hint text
        Rect hintRect = new Rect(0, position.height - 22, position.width, 20);
        GUI.Label(hintRect, "\u2191\u2193 Navigate  \u2022  Enter Select  \u2022  Esc Close", _footerStyle);
    }

    void DrawSettings()
    {
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        GUILayout.Space(14);
        GUILayout.BeginVertical();

        GUILayout.Label("Settings", EditorStyles.boldLabel);
        GUILayout.Space(6);

        EditorGUI.BeginChangeCheck();

        maxVisibleResults = EditorGUILayout.IntSlider(
            new GUIContent("Max Visible Results", "How many results to show before scrolling"),
            maxVisibleResults, 4, 12);

        GUILayout.Space(4);

        alsoSearchHierarchy = EditorGUILayout.Toggle(
            new GUIContent("Also Search Hierarchy", "Include scene objects in asset search results (no h: prefix needed)"),
            alsoSearchHierarchy);

        enableCalculator = EditorGUILayout.Toggle(
            new GUIContent("Enable Calculator", "Evaluate math expressions like 2+2 or sin(45)"),
            enableCalculator);

        bool prevIncludePackages = includePackages;
        includePackages = EditorGUILayout.Toggle(
            new GUIContent("Include Packages", "Also index assets from the Packages folder"),
            includePackages);

        if (EditorGUI.EndChangeCheck())
        {
            SaveSettings();

            // Rebuild index if package scope changed
            if (includePackages != prevIncludePackages)
            {
                indexingComplete = false;
                root = new TrieNode();
                BuildFileIndex();
            }
        }

        GUILayout.Space(8);

        // Separator
        Rect sepRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(sepRect, EditorGUIUtility.isProSkin
            ? new Color(0.25f, 0.25f, 0.25f) : new Color(0.72f, 0.72f, 0.72f));

        GUILayout.Space(8);

        if (GUILayout.Button("Rebuild Index", GUILayout.Height(24)))
        {
            indexingComplete = false;
            root = new TrieNode();
            BuildFileIndex();
        }

        GUILayout.FlexibleSpace();

        string status = indexingComplete ? "\u2713 Index ready" : "\u21bb Indexing...";
        GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = indexingComplete
                ? new Color(0.3f, 0.8f, 0.3f)
                : new Color(0.8f, 0.7f, 0.2f) }
        };
        GUILayout.Label(status, statusStyle);

        GUILayout.Space(8);
        GUILayout.EndVertical();
        GUILayout.Space(14);
        GUILayout.EndHorizontal();
    }

    void HandleKeyboardInput()
    {
        if (Event.current.type != EventType.KeyDown) return;

        if (Event.current.keyCode == KeyCode.Space &&
            Event.current.shift &&
            (Event.current.control || Event.current.command))
        {
            lastCloseTime = EditorApplication.timeSinceStartup;
            Close();
            Event.current.Use();
            return;
        }

        switch (Event.current.keyCode)
        {
            case KeyCode.DownArrow:
                selectedIndex = (selectedIndex + 1) % Mathf.Max(1, searchResults.Count);
                keyboardNavigationActive = true;
                Event.current.Use();
                EnsureVisible();
                Repaint();
                break;

            case KeyCode.UpArrow:
                selectedIndex = (selectedIndex - 1 + searchResults.Count) % Mathf.Max(1, searchResults.Count);
                keyboardNavigationActive = true;
                Event.current.Use();
                EnsureVisible();
                Repaint();
                break;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                if (selectedIndex >= 0 && selectedIndex < searchResults.Count)
                {
                    SelectResult(searchResults[selectedIndex]);
                    Event.current.Use();
                }
                break;

            case KeyCode.Escape:
                Close();
                Event.current.Use();
                break;
        }
    }

    void EnsureVisible()
    {
        float itemTop = selectedIndex * itemHeight;
        float itemBottom = itemTop + itemHeight;
        float viewHeight = position.height - 67f - 24f; // top area - footer

        if (itemBottom > scrollPosition.y + viewHeight)
            scrollPosition.y = itemBottom - viewHeight;
        else if (itemTop < scrollPosition.y)
            scrollPosition.y = itemTop;
    }

    void SelectResult(SearchResult result)
    {
        if (result.Target != null)
        {
            Selection.activeObject = result.Target;
            EditorGUIUtility.PingObject(result.Target);
        }
        Close();
    }

    void ResizeWindow()
    {
        float topArea = 62f; // search bar area + padding

        float contentHeight = topArea;

        if (displaySettings)
        {
            contentHeight += 300;
        }
        else if (searchResults.Count > 0)
        {
            int visibleCount = Mathf.Min(searchResults.Count, maxVisibleResults);
            contentHeight += 5 + visibleCount * itemHeight + 24; // sep+gap + items + footer
        }
        else if (!string.IsNullOrEmpty(searchQuery))
        {
            contentHeight += 50;
        }

        float targetHeight = Mathf.Clamp(contentHeight, minWindowHeight, maxWindowHeight);
        minSize = new Vector2(windowWidth, targetHeight);
        maxSize = new Vector2(windowWidth, targetHeight);
    }

    void BuildFileIndex()
    {
        root = new TrieNode();
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        EditorApplication.update += IndexNextBatch;
        indexIterator = ((IEnumerable<string>)allAssets).GetEnumerator();
    }

    void IndexNextBatch()
    {
        int processed = 0;
        int batchSize = 200;

        while (processed < batchSize && indexIterator.MoveNext())
        {
            string path = indexIterator.Current;
            if (path.StartsWith("Assets/") || (includePackages && path.StartsWith("Packages/")))
            {
                string fileName = Path.GetFileNameWithoutExtension(path).ToLower();
                root.Insert(fileName, path);
            }
            processed++;
        }

        if (!indexIterator.MoveNext())
        {
            EditorApplication.update -= IndexNextBatch;
            indexIterator = null;
            indexingComplete = true;

            if (!string.IsNullOrEmpty(searchQuery))
                PerformSearch();
        }
    }

    void PerformSearch()
    {
        searchResults.Clear();

        if (string.IsNullOrEmpty(searchQuery) || displaySettings)
            return;

        if (searchQuery.StartsWith("h:"))
        {
            SearchHierarchy(searchQuery.Substring(2).Trim());
            return;
        }

        // Math evaluation
        if (enableCalculator)
        {
            string mathResult = AdvancedMathEvaluator.EvaluateExpression(searchQuery);
            if (mathResult != null)
            {
                searchResults.Add(new SearchResult
                {
                    Name = mathResult,
                    Path = "= " + searchQuery,
                    Icon = EditorGUIUtility.IconContent("console.infoicon").image as Texture2D
                });
                return;
            }
        }

        // File search
        var paths = root.Search(searchQuery.ToLower());
        var rankedResults = RankResults(paths, searchQuery.ToLower());

        foreach (var path in rankedResults)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
            {
                Texture2D icon = null;

                if (path.EndsWith(".unity"))
                {
                    icon = FileUtilities.GetFileTypeIcon(path);
                }
                else
                {
                    icon = AssetPreview.GetMiniThumbnail(asset);
                    if (icon == null)
                        icon = FileUtilities.GetFileTypeIcon(path);
                }

                searchResults.Add(new SearchResult
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    Icon = icon,
                    Target = asset
                });
            }
        }

        // Also search hierarchy if enabled
        if (alsoSearchHierarchy)
            SearchHierarchy(searchQuery);
    }

    void SearchHierarchy(string query)
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        query = query.ToLower();

        foreach (GameObject go in allObjects)
        {
            if (go.name.ToLower().Contains(query))
            {
                searchResults.Add(new SearchResult
                {
                    Name = go.name,
                    Path = "Scene: " + go.scene.name,
                    Icon = HierarchySearcher.GetHierarchyIcon(go),
                    Target = go
                });

                if (searchResults.Count >= 50) break;
            }
        }
    }

    List<string> RankResults(List<string> paths, string query)
    {
        var scored = new List<(string path, int score)>();

        foreach (var path in paths)
        {
            string fileName = Path.GetFileNameWithoutExtension(path).ToLower();
            int score = 0;

            if (fileName == query) score += 1000;
            else if (fileName.StartsWith(query)) score += 500;
            else if (fileName.Contains(query)) score += 250;
            else score += FuzzyScore(fileName, query);

            score -= fileName.Length;
            scored.Add((path, score));
        }

        scored.Sort((a, b) => b.score.CompareTo(a.score));
        return scored.ConvertAll(x => x.path);
    }

    int FuzzyScore(string text, string query)
    {
        int score = 0;
        int textIndex = 0;

        foreach (char c in query)
        {
            int foundAt = text.IndexOf(c, textIndex);
            if (foundAt >= 0)
            {
                score += 10;
                if (foundAt == textIndex) score += 5;
                textIndex = foundAt + 1;
            }
        }

        return score;
    }

    private class SearchResult
    {
        public string Name;
        public string Path;
        public Texture2D Icon;
        public UnityEngine.Object Target;
    }
}
