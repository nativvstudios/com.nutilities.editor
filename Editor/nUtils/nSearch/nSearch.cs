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

    // Fixed window dimensions
    private readonly float windowWidth = 600f;
    private readonly float minWindowHeight = 60f;
    private readonly float maxWindowHeight = 480f;
    private readonly float itemHeight = 50f;
    private readonly float searchFieldHeight = 36f;

    // Settings
    private bool option1 = false;
    private string option2 = "";
    private int option3 = 0;

    [MenuItem("Window/nUtilities/nSearch %#SPACE")]
    public static void ToggleWindow()
    {
        // Prevent immediate reopening after close (cooldown period)
        double timeSinceClose = EditorApplication.timeSinceStartup - lastCloseTime;
        if (timeSinceClose < 0.2) // 200ms cooldown
        {
            return;
        }

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

        currentWindow.ShowPopup();

        float height = currentWindow.minWindowHeight;
        currentWindow.minSize = new Vector2(currentWindow.windowWidth, height);
        currentWindow.maxSize = new Vector2(currentWindow.windowWidth, height);

        WindowUtilities.CenterWindow(currentWindow);
    }

    void OnDestroy()
    {
        if (currentWindow == this)
        {
            currentWindow = null;
        }
    }

    void OnEnable()
    {
        if (!indexingComplete && indexIterator == null)
        {
            BuildFileIndex();
        }
    }

    void OnGUI()
    {
        HandleKeyboardInput();

        // Clean background
        Rect bgRect = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(bgRect, EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 0.98f)
            : new Color(0.76f, 0.76f, 0.76f, 0.98f));

        GUILayout.BeginVertical();
        GUILayout.Space(8);

        // Search field area with slight padding
        GUILayout.BeginHorizontal();
        GUILayout.Space(8);

        // Create search field with proper styling
        EditorGUI.BeginChangeCheck();

        GUI.SetNextControlName("SearchField");

        // Use a styled text field that looks like Unity's search
        GUIStyle searchFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 22,
            padding = new RectOffset(26, 24, 3, 3),
            margin = new RectOffset(0, 0, 0, 0)
        };

        searchQuery = GUILayout.TextField(searchQuery, searchFieldStyle, GUILayout.ExpandWidth(true));

        // Get the rect of the search field for overlay elements
        Rect searchRect = GUILayoutUtility.GetLastRect();

        // Draw search icon
        Rect iconRect = new Rect(searchRect.x + 6, searchRect.y + 4, 14, 14);
        GUIStyle iconStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.7f) }
        };
        GUI.Label(iconRect, "🔍", iconStyle);

        // Draw placeholder text when empty
        if (string.IsNullOrEmpty(searchQuery) && GUI.GetNameOfFocusedControl() != "SearchField")
        {
            GUIStyle placeholderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.5f) }
            };
            Rect placeholderRect = new Rect(searchRect.x + 26, searchRect.y + 3, searchRect.width - 50, searchRect.height);
            GUI.Label(placeholderRect, "Search or calculate...", placeholderStyle);
        }

        // Clear button
        Rect clearButtonRect = new Rect(searchRect.xMax - 20, searchRect.y + 2, 18, 18);
        if (!string.IsNullOrEmpty(searchQuery))
        {
            if (GUI.Button(clearButtonRect, "×", EditorStyles.miniButton))
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

        GUILayout.Space(8);
        GUILayout.EndHorizontal();

        // Focus on first show
        if (Event.current.type == EventType.Layout)
        {
            if (GUI.GetNameOfFocusedControl() != "SearchField")
            {
                GUI.FocusControl("SearchField");
                Repaint();
            }
        }

        GUILayout.Space(4);

        if (displaySettings)
        {
            DrawSettings();
        }
        else
        {
            DrawResults();
        }

        GUILayout.EndVertical();
    }



    void DrawResults()
    {
        if (searchResults.Count == 0)
        {
            if (!string.IsNullOrEmpty(searchQuery))
            {
                // No results state
                GUILayout.FlexibleSpace();
                GUIStyle noResultsStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    normal = { textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.5f, 0.5f, 0.5f)
                        : new Color(0.45f, 0.45f, 0.45f) }
                };
                GUILayout.Label("No results found", noResultsStyle);

                GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.4f, 0.4f, 0.4f)
                        : new Color(0.5f, 0.5f, 0.5f) }
                };
                GUILayout.Label("Try a different search term", hintStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                // Initial empty state with helpful hints
                GUILayout.FlexibleSpace();
                GUIStyle titleStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.7f, 0.7f, 0.7f)
                        : new Color(0.3f, 0.3f, 0.3f) }
                };
                GUILayout.Label("🔍 nSearch", titleStyle);

                GUILayout.Space(10);

                GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    wordWrap = true,
                    normal = { textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.5f, 0.5f, 0.5f)
                        : new Color(0.45f, 0.45f, 0.45f) }
                };

                GUILayout.Label("Type to search hierarchy, assets, or use calculator", hintStyle);
                GUILayout.Space(4);
                GUILayout.Label("Examples: \"Player\", \"2+2\", \"sin(45)\"", hintStyle);
                GUILayout.FlexibleSpace();
            }
            return;
        }

        float topHeight = searchFieldHeight + 18 + 10;
        float bottomHeight = 20;
        float scrollViewHeight = position.height - topHeight - bottomHeight;

        Rect scrollRect = GUILayoutUtility.GetRect(position.width, scrollViewHeight);
        float contentHeight = searchResults.Count * itemHeight;
        Rect contentRect = new Rect(0, 0, scrollRect.width - 20, contentHeight);

        scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, contentRect);

        for (int i = 0; i < searchResults.Count; i++)
        {
            Rect itemRect = new Rect(0, i * itemHeight, contentRect.width, itemHeight);
            DrawResultItem(searchResults[i], i, itemRect);
        }

        GUI.EndScrollView();
        DrawHint();
    }

    void DrawResultItem(SearchResult result, int index, Rect rect)
    {
        // Convert rect to screen space for proper hover/click detection
        Rect itemRect = new Rect(rect.x, rect.y, rect.width, rect.height);

        // Hover detection
        Event e = Event.current;
        bool isHovered = itemRect.Contains(e.mousePosition - scrollPosition);

        // Only update selection on mouse move, not during keyboard navigation
        if (isHovered && e.type == EventType.MouseMove)
        {
            selectedIndex = index;
            keyboardNavigationActive = false;
            Repaint();
        }

        // Draw subtle separator between items
        if (index > 0)
        {
            Rect separatorRect = new Rect(rect.x + 12, rect.y, rect.width - 24, 1);
            EditorGUI.DrawRect(separatorRect, EditorGUIUtility.isProSkin
                ? new Color(0.15f, 0.15f, 0.15f, 0.5f)
                : new Color(0.7f, 0.7f, 0.7f, 0.3f));
        }

        // Selection highlight using native selection color with rounded corners
        if (index == selectedIndex)
        {
            Rect highlightRect = new Rect(rect.x + 6, rect.y + 2, rect.width - 12, rect.height - 4);

            Color selectionColor = EditorGUIUtility.isProSkin
                ? new Color(0.24f, 0.48f, 0.90f, 0.75f)
                : new Color(0.23f, 0.45f, 0.87f, 0.5f);

            // Draw rounded selection background
            EditorGUI.DrawRect(highlightRect, selectionColor);

            // Add subtle left accent bar for selected item
            Rect accentRect = new Rect(rect.x + 6, rect.y + 2, 3, rect.height - 4);
            Color accentColor = EditorGUIUtility.isProSkin
                ? new Color(0.3f, 0.6f, 1f, 1f)
                : new Color(0.2f, 0.4f, 0.9f, 0.8f);
            EditorGUI.DrawRect(accentRect, accentColor);
        }

        // Click handling
        if (e.type == EventType.MouseDown && e.button == 0 && isHovered)
        {
            SelectResult(result);
            e.Use();
            return;
        }

        // Draw content
        Rect iconRect = new Rect(rect.x + 12, rect.y + 2, 40, 40);
        Rect labelRect = new Rect(rect.x + 58, rect.y + 6, rect.width - 68, 18);
        Rect pathRect = new Rect(rect.x + 58, rect.y + 24, rect.width - 68, 14);

        if (result.Icon != null)
        {
            GUI.DrawTexture(iconRect, result.Icon, ScaleMode.ScaleToFit);
        }

        // Use native label style
        GUIStyle nameStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Normal,
            normal = { textColor = EditorGUIUtility.isProSkin
            ? new Color(0.85f, 0.85f, 0.85f)
            : new Color(0.1f, 0.1f, 0.1f) }
        };

        GUIStyle pathStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            normal = { textColor = EditorGUIUtility.isProSkin
            ? new Color(0.55f, 0.55f, 0.55f)
            : new Color(0.45f, 0.45f, 0.45f) }
        };

        GUI.Label(labelRect, result.Name, nameStyle);
        GUI.Label(pathRect, result.Path, pathStyle);
    }

    void DrawSettings()
    {
        GUILayout.Space(12);
        GUILayout.BeginHorizontal();
        GUILayout.Space(12);
        GUILayout.BeginVertical();

        GUILayout.Label("Settings", EditorStyles.boldLabel);
        GUILayout.Space(8);

        if (GUILayout.Button("Clear Cache", GUILayout.Height(26)))
        {
            Debug.Log("Cache Cleared");
            indexingComplete = false;
            root = new TrieNode();
            BuildFileIndex();
        }

        GUILayout.Space(8);

        // Make settings controls focusable
        GUI.SetNextControlName("Option1Toggle");
        option1 = EditorGUILayout.Toggle("Enable Option 1", option1);

        GUI.SetNextControlName("Option2Field");
        option2 = EditorGUILayout.TextField("Option 2", option2);

        GUI.SetNextControlName("Option3Slider");
        option3 = EditorGUILayout.IntSlider("Option 3", option3, 0, 100);

        GUILayout.FlexibleSpace();

        string status = indexingComplete ? "✓ Indexing complete" : "⟳ Indexing...";
        GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = indexingComplete
            ? new Color(0.3f, 0.8f, 0.3f)
            : new Color(0.8f, 0.7f, 0.2f) }
        };
        GUILayout.Label(status, statusStyle);

        GUILayout.Space(8);
        GUILayout.EndVertical();
        GUILayout.Space(12);
        GUILayout.EndHorizontal();
    }

    void DrawHint()
    {
        // Draw separator line
        Rect separatorRect = new Rect(0, position.height - 22, position.width, 1);
        EditorGUI.DrawRect(separatorRect, EditorGUIUtility.isProSkin
            ? new Color(0.1f, 0.1f, 0.1f, 0.8f)
            : new Color(0.6f, 0.6f, 0.6f, 0.5f));

        // Draw hint bar background
        Rect hintBgRect = new Rect(0, position.height - 21, position.width, 21);
        EditorGUI.DrawRect(hintBgRect, EditorGUIUtility.isProSkin
            ? new Color(0.19f, 0.19f, 0.19f, 1f)
            : new Color(0.8f, 0.8f, 0.8f, 1f));

        GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = EditorGUIUtility.isProSkin
                ? new Color(0.6f, 0.6f, 0.6f)
                : new Color(0.4f, 0.4f, 0.4f) },
            fontSize = 10
        };

        Rect hintRect = new Rect(0, position.height - 19, position.width, 18);
        GUI.Label(hintRect, "↑↓: Navigate  •  Enter: Select  •  Esc/Ctrl+Shift+Space: Close", hintStyle);
    }

    void HandleKeyboardInput()
    {
        if (Event.current.type != EventType.KeyDown) return;

        // Check for toggle shortcut (Ctrl/Cmd + Shift + Space)
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
        float viewHeight = position.height - searchFieldHeight - 40;

        if (itemBottom > scrollPosition.y + viewHeight)
        {
            scrollPosition.y = itemBottom - viewHeight;
        }
        else if (itemTop < scrollPosition.y)
        {
            scrollPosition.y = itemTop;
        }
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
        float contentHeight = searchFieldHeight + 18 + 10;

        if (displaySettings)
        {
            contentHeight += 300;
        }
        else if (searchResults.Count > 0)
        {
            int visibleCount = Mathf.Min(searchResults.Count, 8);
            contentHeight += visibleCount * itemHeight + 20;
        }
        else if (!string.IsNullOrEmpty(searchQuery))
        {
            contentHeight += 40;
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
            if (path.StartsWith("Assets/"))
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
            {
                PerformSearch();
            }
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

        // Try advanced math evaluation first
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

        // Continue with file search...
        var paths = root.Search(searchQuery.ToLower());
        var rankedResults = RankResults(paths, searchQuery.ToLower());

        foreach (var path in rankedResults)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
            {
                Texture2D icon = null;

                // For scenes, always use the scene icon
                if (path.EndsWith(".unity"))
                {
                    icon = FileUtilities.GetFileTypeIcon(path);
                }
                else
                {
                    // Try to get mini thumbnail first (more reliable than preview)
                    icon = AssetPreview.GetMiniThumbnail(asset);

                    // If mini thumbnail is null or default, use file type icon
                    if (icon == null)
                    {
                        icon = FileUtilities.GetFileTypeIcon(path);
                    }
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
