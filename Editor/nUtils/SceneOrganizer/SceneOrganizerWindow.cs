using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using System.Linq;
using UnityEditor.IMGUI.Controls;

public class SceneOrganizerWindow : EditorWindow
{
    // Data
    private SceneGroupData sceneGroupData;
    private AssetNotesData assetNotesData;
    private List<string> allScenes = new List<string>();

    // Split View
    private float splitViewPercent = 0.5f;
    private bool isResizingSplitView = false;

    // UI State
    private Vector2 leftScrollPosition;
    private Vector2 rightScrollPosition;

    // Interaction State
    private string selectedScene;
    private int selectedGroupIndex = -1;
    private string draggingScene;
    private string targetGroup;
    private float lastClickTime;

    // Rename State
    private string renamingScene;
    private string renamingGroup;
    private string renamingGroupText = ""; // Add this

    // New Group State
    private bool isCreatingNewGroup = false;
    private string newGroupName = "";

    // Settings
    private string searchQuery = "";
    private bool showRecentScenes = true;
    private bool enableBackup;
    private string backupDirectory;

    // View Options
    private bool showScenePath = false;
    private SceneSortMode sortMode = SceneSortMode.Name;
    private bool debugMode = false;

    private enum SceneSortMode { Name, Path, RecentlyUsed }

    [MenuItem("Window/nUtilities/Scene Organizer")]
    public static void ShowWindow()
    {
        var window = GetWindow<SceneOrganizerWindow>("Scene Organizer");
        window.minSize = new Vector2(600, 400);
    }

    private void OnEnable()
    {
        LoadScenes();
        LoadGroups();
        LoadSettings();
        LoadAssetNotes();
    }

    private void OnDisable()
    {
        SaveSettings();
        SaveGroups();
    }

    private void LoadSettings()
    {
        enableBackup = EditorPrefs.GetBool(SceneOrganizerConstants.PREF_ENABLE_BACKUP, false);
        backupDirectory = EditorPrefs.GetString(SceneOrganizerConstants.PREF_BACKUP_DIR, Application.dataPath);
        showRecentScenes = EditorPrefs.GetBool(SceneOrganizerConstants.PREF_SHOW_RECENT, true);
        splitViewPercent = EditorPrefs.GetFloat("SceneOrganizer_SplitView", 0.5f);
        showScenePath = EditorPrefs.GetBool("SceneOrganizer_ShowPath", false);
        sortMode = (SceneSortMode)EditorPrefs.GetInt("SceneOrganizer_SortMode", 0);
    }

    private void SaveSettings()
    {
        EditorPrefs.SetBool(SceneOrganizerConstants.PREF_SHOW_RECENT, showRecentScenes);
        EditorPrefs.SetFloat("SceneOrganizer_SplitView", splitViewPercent);
        EditorPrefs.SetBool("SceneOrganizer_ShowPath", showScenePath);
        EditorPrefs.SetInt("SceneOrganizer_SortMode", (int)sortMode);
    }

    public void LoadScenes()
    {
        allScenes.Clear();
        string[] sceneGUIDs = AssetDatabase.FindAssets("t:Scene");
        foreach (string guid in sceneGUIDs)
        {
            allScenes.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        SortScenes();
        Repaint();
    }

    private void SortScenes()
    {
        switch (sortMode)
        {
            case SceneSortMode.Name:
                allScenes.Sort((a, b) => Path.GetFileNameWithoutExtension(a).CompareTo(Path.GetFileNameWithoutExtension(b)));
                break;
            case SceneSortMode.Path:
                allScenes.Sort();
                break;
            case SceneSortMode.RecentlyUsed:
                if (sceneGroupData != null)
                {
                    allScenes = allScenes.OrderByDescending(s =>
                        sceneGroupData.recentScenes.IndexOf(s)).ToList();
                }
                break;
        }
    }

    private SceneGroupData GetSceneGroupData()
    {
        return sceneGroupData;
    }

    private void LoadAssetNotes()
    {
        string assetPath = "Assets/Editor/AssetNotesData.asset";
        assetNotesData = AssetDatabase.LoadAssetAtPath<AssetNotesData>(assetPath);

        if (assetNotesData == null)
        {
            assetNotesData = CreateInstance<AssetNotesData>();
            AssetDatabase.CreateAsset(assetNotesData, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Scene Organizer] Created AssetNotesData at " + assetPath);
        }
        else
        {
            Debug.Log($"[Scene Organizer] Loaded AssetNotesData with {assetNotesData.GetNoteCount()} note(s)");
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        Rect contentRect = new Rect(0, EditorGUIUtility.singleLineHeight + 4,
            position.width, position.height - EditorGUIUtility.singleLineHeight - 4);

        DrawSplitView(contentRect);

        HandleKeyboardShortcuts();
        HandleDragAndDrop();
    }

    #region Toolbar
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Scene controls
        if (GUILayout.Button("Create Scene", EditorStyles.toolbarButton))
        {
            CreateNewSceneWindow.ShowWindow(this);
        }

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
        {
            LoadScenes();
        }

        GUILayout.Space(10);

        // Search
        GUILayout.Label("Search:", GUILayout.Width(50));
        searchQuery = GUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(200));

        GUILayout.FlexibleSpace();

        // View options
        if (GUILayout.Button("Sort: " + sortMode.ToString(), EditorStyles.toolbarDropDown, GUILayout.Width(120)))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Name"), sortMode == SceneSortMode.Name, () => { sortMode = SceneSortMode.Name; SortScenes(); });
            menu.AddItem(new GUIContent("Path"), sortMode == SceneSortMode.Path, () => { sortMode = SceneSortMode.Path; SortScenes(); });
            menu.AddItem(new GUIContent("Recently Used"), sortMode == SceneSortMode.RecentlyUsed, () => { sortMode = SceneSortMode.RecentlyUsed; SortScenes(); });
            menu.ShowAsContext();
        }

        showScenePath = GUILayout.Toggle(showScenePath, "Show Path", EditorStyles.toolbarButton);
        debugMode = GUILayout.Toggle(debugMode, "Debug", EditorStyles.toolbarButton);

        GUILayout.Space(5);

        if (GUILayout.Button(EditorGUIUtility.IconContent("_Menu"), EditorStyles.toolbarButton))
        {
            ShowOptionsMenu();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ShowOptionsMenu()
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Show Recent Scenes"), showRecentScenes, () => { showRecentScenes = !showRecentScenes; });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Settings"), false, () => SettingsWindow.ShowWindow(this));

        menu.ShowAsContext();
    }
    #endregion

    #region Split View
    private void DrawSplitView(Rect rect)
    {
        float splitX = rect.width * splitViewPercent;

        // Left panel (Scenes)
        Rect leftRect = new Rect(rect.x, rect.y, splitX - 2, rect.height);
        DrawLeftPanel(leftRect);

        // Splitter
        Rect splitterRect = new Rect(rect.x + splitX - 2, rect.y, 4, rect.height);
        DrawSplitter(splitterRect);

        // Right panel (Groups)
        Rect rightRect = new Rect(rect.x + splitX + 2, rect.y, rect.width - splitX - 2, rect.height);
        DrawRightPanel(rightRect);
    }

    private void DrawSplitter(Rect rect)
    {
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            isResizingSplitView = true;
            Event.current.Use();
        }

        if (isResizingSplitView)
        {
            splitViewPercent = Mathf.Clamp(Event.current.mousePosition.x / position.width, 0.2f, 0.8f);
            Repaint();
        }

        if (Event.current.type == EventType.MouseUp)
        {
            isResizingSplitView = false;
        }

        // Draw splitter line
        EditorGUI.DrawRect(new Rect(rect.x + 1, rect.y, 2, rect.height), new Color(0.1f, 0.1f, 0.1f, 0.5f));
    }
    #endregion

    #region Left Panel - Scenes Browser
    private void DrawLeftPanel(Rect rect)
    {
        GUILayout.BeginArea(rect);

        // Header
        Rect headerRect = new Rect(0, 0, rect.width, 22);
        GUI.Box(headerRect, GUIContent.none, EditorStyles.toolbar);
        GUI.Label(new Rect(5, 2, rect.width - 10, 18), "Scenes", EditorStyles.boldLabel);

        // Content area
        Rect contentRect = new Rect(0, 22, rect.width, rect.height - 22);
        GUILayout.BeginArea(contentRect);

        leftScrollPosition = EditorGUILayout.BeginScrollView(leftScrollPosition);

        if (showRecentScenes && sceneGroupData != null)
        {
            DrawRecentScenesList();
        }

        DrawScenesList();

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();

        GUILayout.EndArea();
    }

    private void DrawRecentScenesList()
    {
        if (sceneGroupData.recentScenes.Count == 0) return;

        sceneGroupData.recentScenes.RemoveAll(s => !File.Exists(s));
        if (sceneGroupData.recentScenes.Count == 0) return;

        EditorGUILayout.LabelField("Recent", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        for (int i = 0; i < Mathf.Min(5, sceneGroupData.recentScenes.Count); i++)
        {
            DrawSceneItem(sceneGroupData.recentScenes[i], true);
        }

        EditorGUI.indentLevel--;
        GUILayout.Space(5);
        DrawHorizontalLine();
        GUILayout.Space(5);
    }

    private void DrawScenesList()
    {
        var filteredScenes = GetFilteredScenes();

        if (filteredScenes.Count == 0)
        {
            EditorGUILayout.HelpBox("No scenes found", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"All Scenes ({filteredScenes.Count})", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        foreach (string scene in filteredScenes)
        {
            DrawSceneItem(scene, false);
        }

        EditorGUI.indentLevel--;
    }

    private void DrawSceneItem(string scenePath, bool isRecent)
    {
        if (scenePath == renamingScene)
        {
            DrawRenamingSceneField(scenePath);
            return;
        }

        bool isSelected = scenePath == selectedScene;
        string sceneName = Path.GetFileNameWithoutExtension(scenePath);

        Rect rect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Height(18));

        // Handle selection
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            if (Event.current.button == 0) // Left click
            {
                if (selectedScene == scenePath &&
                    (EditorApplication.timeSinceStartup - lastClickTime) < 0.3f)
                {
                    // Double click - open scene
                    OpenScene(scenePath);
                }
                else
                {
                    selectedScene = scenePath;
                    selectedGroupIndex = -1;
                    lastClickTime = (float)EditorApplication.timeSinceStartup;
                }
                Event.current.Use();
                Repaint();
            }
            else if (Event.current.button == 1) // Right click
            {
                selectedScene = scenePath;
                ShowSceneContextMenu(scenePath);
                Event.current.Use();
            }
        }

        // Draw selection highlight
        if (isSelected)
        {
            EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.90f, 0.5f));
        }
        else if (rect.Contains(Event.current.mousePosition))
        {
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.1f));
        }

        // Draw icon and label
        Rect iconRect = new Rect(rect.x, rect.y, 16, 16);
        float labelX = rect.x + 18;

        GUI.DrawTexture(iconRect, EditorGUIUtility.IconContent("SceneAsset Icon").image);

        // Note indicator
        if (assetNotesData != null && assetNotesData.HasNote(scenePath))
        {
            Rect noteRect = new Rect(labelX, rect.y, 16, 16);
            GUI.Label(noteRect, new GUIContent("📝", assetNotesData.GetNote(scenePath)));
            labelX += 18;
        }

        Rect labelRect = new Rect(labelX, rect.y, rect.width - (labelX - rect.x), rect.height);

        GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
        if (isRecent)
        {
            labelStyle.normal.textColor = new Color(0.6f, 0.8f, 1f);
        }

        GUI.Label(labelRect, sceneName, labelStyle);

        if (showScenePath)
        {
            Rect pathRect = new Rect(rect.x + 18, rect.y, rect.width - 18, rect.height);
            GUI.Label(pathRect, Path.GetDirectoryName(scenePath), EditorStyles.miniLabel);
        }

        // Handle drag start
        if (Event.current.type == EventType.MouseDrag && isSelected && rect.Contains(Event.current.mousePosition))
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.paths = new string[] { scenePath };
            DragAndDrop.objectReferences = new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) };
            DragAndDrop.StartDrag(sceneName);
            draggingScene = scenePath;
            Event.current.Use();
        }
    }

    private void DrawRenamingSceneField(string scenePath)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUI.indentLevel--;

        GUI.SetNextControlName("SceneRenameField");
        string newName = EditorGUILayout.TextField(Path.GetFileNameWithoutExtension(scenePath));

        if (GUILayout.Button("", EditorStyles.label, GUILayout.Width(0), GUILayout.Height(0)))
        {
            GUI.FocusControl("SceneRenameField");
        }

        if (Event.current.isKey && GUI.GetNameOfFocusedControl() == "SceneRenameField")
        {
            if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            {
                RenameScene(scenePath, newName);
                renamingScene = null;
                Event.current.Use();
            }
            else if (Event.current.keyCode == KeyCode.Escape)
            {
                renamingScene = null;
                Event.current.Use();
            }
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.EndHorizontal();
    }

    private void ShowSceneContextMenu(string scenePath)
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Open"), false, () => OpenScene(scenePath));
        menu.AddItem(new GUIContent("Open Additive"), false, () =>
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Edit Note"), false, () =>
        {
            SceneNoteEditorWindow.ShowWindow(scenePath, assetNotesData, this);
        });
        menu.AddSeparator("");

        if (sceneGroupData != null && sceneGroupData.sceneGroups.Count > 0)
        {
            foreach (var group in sceneGroupData.sceneGroups)
            {
                bool inGroup = group.scenes.Contains(scenePath);
                menu.AddItem(new GUIContent("Add to Group/" + group.groupName), inGroup,
                    () => AddSceneToGroup(scenePath, group.groupName));
            }
            menu.AddSeparator("");
        }

        menu.AddItem(new GUIContent("Rename"), false, () => { renamingScene = scenePath; Repaint(); });
        menu.AddItem(new GUIContent("Show in Project"), false, () =>
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Copy Path"), false, () => EditorGUIUtility.systemCopyBuffer = scenePath);

        menu.ShowAsContext();
    }

    private List<string> GetFilteredScenes()
    {
        if (string.IsNullOrEmpty(searchQuery))
            return allScenes;

        return allScenes.Where(s =>
            Path.GetFileNameWithoutExtension(s).ToLower().Contains(searchQuery.ToLower()) ||
            s.ToLower().Contains(searchQuery.ToLower())
        ).ToList();
    }
    #endregion

    #region Right Panel - Groups
    private void DrawRightPanel(Rect rect)
    {
        GUILayout.BeginArea(rect);

        // Header
        Rect headerRect = new Rect(0, 0, rect.width, 22);
        GUI.Box(headerRect, GUIContent.none, EditorStyles.toolbar);

        Rect headerLabelRect = new Rect(5, 2, rect.width - 50, 18);
        GUI.Label(headerLabelRect, "Groups", EditorStyles.boldLabel);

        Rect addButtonRect = new Rect(rect.width - 45, 2, 40, 18);
        if (GUI.Button(addButtonRect, "New", EditorStyles.toolbarButton))
        {
            StartCreatingNewGroup();
        }

        // Content area
        Rect contentRect = new Rect(0, 22, rect.width, rect.height - 22);
        GUILayout.BeginArea(contentRect);

        if (sceneGroupData == null || (sceneGroupData.sceneGroups.Count == 0 && !isCreatingNewGroup))
        {
            DrawEmptyGroupsState();
        }
        else
        {
            DrawGroupsList();
        }

        GUILayout.EndArea();
        GUILayout.EndArea();
    }

    private void DrawEmptyGroupsState()
    {
        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginVertical();

        GUILayout.Label("No groups created yet", EditorStyles.centeredGreyMiniLabel);
        GUILayout.Space(10);
        if (GUILayout.Button("Create First Group", GUILayout.Width(150)))
        {
            StartCreatingNewGroup();
        }

        EditorGUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
    }

    private void DrawGroupsList()
    {
        rightScrollPosition = EditorGUILayout.BeginScrollView(rightScrollPosition);

        // Draw new group creation field at the top if active
        if (isCreatingNewGroup)
        {
            DrawNewGroupCreationField();
            GUILayout.Space(5);
        }

        for (int i = 0; i < sceneGroupData.sceneGroups.Count; i++)
        {
            DrawGroupItem(sceneGroupData.sceneGroups[i], i);
            GUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();

        // Handle drop area for drag and drop
        HandleGroupsDragAndDrop();
    }

    private void StartCreatingNewGroup()
    {
        isCreatingNewGroup = true;
        newGroupName = "";
        Repaint();
    }

    private void DrawNewGroupCreationField()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("New Group", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.SetNextControlName("NewGroupField");
        newGroupName = EditorGUILayout.TextField(newGroupName);

        // Auto-focus the field when it first appears
        if (Event.current.type == EventType.Repaint && string.IsNullOrEmpty(newGroupName))
        {
            EditorGUI.FocusTextInControl("NewGroupField");
        }

        GUI.enabled = !string.IsNullOrWhiteSpace(newGroupName);
        if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            CreateNewGroup(newGroupName);
            isCreatingNewGroup = false;
            newGroupName = "";
            GUI.FocusControl(null);
        }
        GUI.enabled = true;

        if (GUILayout.Button("Cancel", EditorStyles.miniButton, GUILayout.Width(60)))
        {
            isCreatingNewGroup = false;
            newGroupName = "";
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();

        // Handle keyboard shortcuts
        if (Event.current.isKey && GUI.GetNameOfFocusedControl() == "NewGroupField")
        {
            if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            {
                if (!string.IsNullOrWhiteSpace(newGroupName))
                {
                    CreateNewGroup(newGroupName);
                    isCreatingNewGroup = false;
                    newGroupName = "";
                    Event.current.Use();
                }
            }
            else if (Event.current.keyCode == KeyCode.Escape)
            {
                isCreatingNewGroup = false;
                newGroupName = "";
                Event.current.Use();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawGroupItem(SceneGroupData.SceneGroup group, int index)
    {
        bool isSelected = selectedGroupIndex == index;

        // Container with color stripe
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(false));

        // Color stripe on the left (fixed height, no expansion)
        Rect stripeRect = GUILayoutUtility.GetRect(4, 20, GUILayout.Width(4));

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Group header
        Rect headerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(20));

        // Foldout
        bool newCollapsed = !EditorGUILayout.Foldout(!group.isCollapsed, "", true);

        // Check if foldout was clicked
        Rect foldoutRect = GUILayoutUtility.GetLastRect();
        bool foldoutClicked = Event.current.type == EventType.Used && foldoutRect.Contains(Event.current.mousePosition);

        if (!foldoutClicked)
        {
            // Handle selection (but not if foldout was clicked)
            if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 0)
                {
                    selectedGroupIndex = index;
                    selectedScene = null;
                    Event.current.Use();
                    Repaint();
                }
                else if (Event.current.button == 1)
                {
                    selectedGroupIndex = index;
                    ShowGroupContextMenu(group);
                    Event.current.Use();
                }
            }
        }

        group.isCollapsed = newCollapsed;

        if (isSelected)
        {
            EditorGUI.DrawRect(headerRect, new Color(0.24f, 0.48f, 0.90f, 0.3f));
        }

        // Group name or rename field
        if (group.groupName == renamingGroup)
        {
            GUI.SetNextControlName("GroupRenameField");
            renamingGroupText = EditorGUILayout.TextField(renamingGroupText, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "GroupRenameField")
            {
                EditorGUI.FocusTextInControl("GroupRenameField");
            }

            if (Event.current.isKey && GUI.GetNameOfFocusedControl() == "GroupRenameField")
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    RenameGroup(group.groupName, renamingGroupText);
                    renamingGroup = null;
                    renamingGroupText = "";
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.Escape)
                {
                    renamingGroup = null;
                    renamingGroupText = "";
                    Event.current.Use();
                }
            }
        }
        else
        {
            GUILayout.Label($"{group.groupName} ({group.scenes.Count})", EditorStyles.boldLabel);
        }

        GUILayout.FlexibleSpace();

        // Load all button (hide during rename)
        if (group.groupName != renamingGroup && group.scenes.Count > 0)
        {
            GUIContent loadAllContent = new GUIContent("Load All", "Load all scenes in this group together (first scene single, rest additive)");
            if (GUILayout.Button(loadAllContent, EditorStyles.miniButton, GUILayout.Width(60)))
            {
                if (debugMode) Debug.Log($"[Scene Organizer] Load All button clicked for group: {group.groupName}");
                LoadAllScenesInGroup(group);
            }
        }

        EditorGUILayout.EndHorizontal();

        // Group content
        if (!group.isCollapsed)
        {
            DrawGroupContent(group, index);
        }

        EditorGUILayout.EndVertical();

        // Draw the color stripe
        if (Event.current.type == EventType.Repaint)
        {
            Rect verticalRect = GUILayoutUtility.GetLastRect();
            Rect fullStripeRect = new Rect(stripeRect.x, verticalRect.y, 4, verticalRect.height);
            EditorGUI.DrawRect(fullStripeRect, group.groupColor);
        }

        EditorGUILayout.EndHorizontal();

        // Handle drop on group
        if (draggingScene != null && headerRect.Contains(Event.current.mousePosition))
        {
            targetGroup = group.groupName;

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(headerRect, new Color(0.3f, 0.7f, 0.3f, 0.3f));
            }
        }
    }

    private void DrawGroupContent(SceneGroupData.SceneGroup group, int groupIndex)
    {
        EditorGUI.indentLevel++;

        // Clean up missing scenes
        group.scenes.RemoveAll(s => !File.Exists(s));

        if (group.scenes.Count == 0)
        {
            EditorGUILayout.LabelField("Drop scenes here", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            List<string> scenesToRemove = new List<string>();

            foreach (var scene in group.scenes)
            {
                DrawGroupSceneItem(group, scene, scenesToRemove);
            }

            foreach (var scene in scenesToRemove)
            {
                group.scenes.Remove(scene);
                EditorUtility.SetDirty(sceneGroupData);
            }
        }

        EditorGUI.indentLevel--;
    }

    private void DrawGroupSceneItem(SceneGroupData.SceneGroup group, string scenePath, List<string> scenesToRemove)
    {
        EditorGUILayout.BeginHorizontal();

        // Scene icon
        Rect iconRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
        GUI.DrawTexture(iconRect, EditorGUIUtility.IconContent("SceneAsset Icon").image);

        // Note indicator
        if (assetNotesData != null && assetNotesData.HasNote(scenePath))
        {
            Rect noteRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
            GUI.Label(noteRect, new GUIContent("📝", assetNotesData.GetNote(scenePath)));
        }

        // Scene name (non-clickable label)
        GUILayout.Label(Path.GetFileNameWithoutExtension(scenePath), EditorStyles.label);

        GUILayout.FlexibleSpace();

        // Open button
        GUIContent openContent = new GUIContent("Open", "Open this scene (closes other scenes)");
        if (GUILayout.Button(openContent, EditorStyles.miniButton, GUILayout.Width(50)))
        {
            OpenScene(scenePath);
        }

        // Context menu button (three dots)
        if (GUILayout.Button("≡", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            ShowGroupSceneContextMenu(group, scenePath);
        }

        // Remove button
        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            scenesToRemove.Add(scenePath);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ShowGroupContextMenu(SceneGroupData.SceneGroup group)
    {
        GenericMenu menu = new GenericMenu();

        if (group.scenes.Count > 0)
        {
            menu.AddItem(new GUIContent("Load All Scenes"), false, () => LoadAllScenesInGroup(group));
            menu.AddSeparator("");
        }

        menu.AddItem(new GUIContent("Rename"), false, () =>
        {
            renamingGroup = group.groupName;
            renamingGroupText = group.groupName; // Initialize with current name
            Repaint();
        });
        menu.AddItem(new GUIContent("Change Color"), false, () => ShowColorPicker(group));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete Group"), false, () =>
        {
            if (EditorUtility.DisplayDialog("Delete Group",
                $"Are you sure you want to delete '{group.groupName}'?", "Delete", "Cancel"))
            {
                RemoveGroup(group);
            }
        });

        menu.ShowAsContext();
    }

    private void ShowGroupSceneContextMenu(SceneGroupData.SceneGroup group, string scenePath)
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Open (Single)"), false, () => OpenScene(scenePath));
        menu.AddItem(new GUIContent("Open Additive (Multi-Scene)"), false, () =>
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive));
        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Edit Note"), false, () =>
        {
            SceneNoteEditorWindow.ShowWindow(scenePath, assetNotesData, this);
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Remove from Group"), false, () =>
        {
            group.scenes.Remove(scenePath);
            EditorUtility.SetDirty(sceneGroupData);
        });

        if (sceneGroupData.sceneGroups.Count > 1)
        {
            menu.AddSeparator("");
            foreach (var otherGroup in sceneGroupData.sceneGroups)
            {
                if (otherGroup.groupName != group.groupName)
                {
                    menu.AddItem(new GUIContent("Move to/" + otherGroup.groupName), false, () =>
                        MoveSceneToGroup(scenePath, group.groupName, otherGroup.groupName));
                }
            }
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Show in Project"), false, () =>
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)));

        menu.ShowAsContext();
    }

    private void ShowColorPicker(SceneGroupData.SceneGroup group)
    {
        ColorPickerWindow.ShowWindow(group, this);
    }
    #endregion

    #region Drag and Drop
    private void HandleDragAndDrop()
    {
        if (Event.current.type == EventType.DragPerform)
        {
            if (draggingScene != null && targetGroup != null)
            {
                DragAndDrop.AcceptDrag();
                AddSceneToGroup(draggingScene, targetGroup);
                draggingScene = null;
                targetGroup = null;
                Event.current.Use();
                Repaint();
            }
        }

        if (Event.current.type == EventType.DragExited || Event.current.type == EventType.MouseUp)
        {
            draggingScene = null;
            targetGroup = null;
            Repaint();
        }
    }

    private void HandleGroupsDragAndDrop()
    {
        if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
        {
            foreach (var path in DragAndDrop.paths)
            {
                if (path.EndsWith(".unity"))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                }
            }
        }
    }
    #endregion

    #region Keyboard Shortcuts
    private void HandleKeyboardShortcuts()
    {
        if (Event.current.type != EventType.KeyDown) return;

        // Delete - remove from group or delete scene
        if (Event.current.keyCode == KeyCode.Delete)
        {
            if (selectedGroupIndex >= 0 && selectedGroupIndex < sceneGroupData.sceneGroups.Count)
            {
                var group = sceneGroupData.sceneGroups[selectedGroupIndex];
                if (EditorUtility.DisplayDialog("Delete Group",
                    $"Are you sure you want to delete '{group.groupName}'?", "Delete", "Cancel"))
                {
                    RemoveGroup(group);
                    Event.current.Use();
                }
            }
        }

        // F2 - Rename
        if (Event.current.keyCode == KeyCode.F2)
        {
            if (!string.IsNullOrEmpty(selectedScene))
            {
                renamingScene = selectedScene;
                Event.current.Use();
                Repaint();
            }
            else if (selectedGroupIndex >= 0)
            {
                renamingGroup = sceneGroupData.sceneGroups[selectedGroupIndex].groupName;
                renamingGroupText = renamingGroup; // Initialize text
                Event.current.Use();
                Repaint();
            }
        }

        // Escape - Clear selection or cancel rename
        if (Event.current.keyCode == KeyCode.Escape)
        {
            if (renamingScene != null || renamingGroup != null || isCreatingNewGroup)
            {
                renamingScene = null;
                renamingGroup = null;
                isCreatingNewGroup = false;
                newGroupName = "";
                Event.current.Use();
                Repaint();
            }
            else
            {
                selectedScene = null;
                selectedGroupIndex = -1;
                Event.current.Use();
                Repaint();
            }
        }

        // Return - Open scene
        if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
        {
            if (!string.IsNullOrEmpty(selectedScene) && renamingScene == null)
            {
                OpenScene(selectedScene);
                Event.current.Use();
            }
        }
    }
    #endregion

    #region Data Operations
    private void CreateNewGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            EditorUtility.DisplayDialog("Invalid Name", "Group name cannot be empty.", "OK");
            return;
        }

        if (sceneGroupData.sceneGroups.Exists(g => g.groupName == groupName))
        {
            EditorUtility.DisplayDialog("Duplicate Name", "A group with this name already exists.", "OK");
            return;
        }

        var newGroup = new SceneGroupData.SceneGroup
        {
            groupName = groupName,
            groupColor = GetRandomPastelColor()
        };

        sceneGroupData.sceneGroups.Add(newGroup);
        EditorUtility.SetDirty(sceneGroupData);
        SaveGroups();
        Repaint();
    }

    private Color GetRandomPastelColor()
    {
        Color[] colors = new Color[]
        {
        new Color(0.3f, 0.6f, 1f),      // Blue
        new Color(1f, 0.4f, 0.4f),      // Red
        new Color(0.4f, 0.9f, 0.4f),    // Green
        new Color(1f, 0.8f, 0.2f),      // Yellow
        new Color(0.9f, 0.4f, 0.9f),    // Magenta
        new Color(0.4f, 0.9f, 0.9f),    // Cyan
        };
        return colors[UnityEngine.Random.Range(0, colors.Length)];
    }

    private void RenameGroup(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        var group = sceneGroupData.sceneGroups.Find(g => g.groupName == oldName);
        if (group != null)
        {
            group.groupName = newName;
            EditorUtility.SetDirty(sceneGroupData);
            SaveGroups();
        }
    }

    private void RenameScene(string oldScenePath, string newSceneName)
    {
        if (string.IsNullOrWhiteSpace(newSceneName)) return;

        string error = AssetDatabase.RenameAsset(oldScenePath, newSceneName);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Rename Failed", error, "OK");
        }
        else
        {
            AssetDatabase.SaveAssets();
            LoadScenes();
        }
    }

    private void AddSceneToGroup(string scene, string groupName)
    {
        var group = sceneGroupData.sceneGroups.Find(g => g.groupName == groupName);
        if (group != null && !group.scenes.Contains(scene))
        {
            group.scenes.Add(scene);
            EditorUtility.SetDirty(sceneGroupData);
            SaveGroups();
        }
    }

    private void MoveSceneToGroup(string scene, string fromGroup, string toGroup)
    {
        var sourceGroup = sceneGroupData.sceneGroups.Find(g => g.groupName == fromGroup);
        var targetGroup = sceneGroupData.sceneGroups.Find(g => g.groupName == toGroup);

        if (sourceGroup != null && targetGroup != null)
        {
            sourceGroup.scenes.Remove(scene);
            if (!targetGroup.scenes.Contains(scene))
            {
                targetGroup.scenes.Add(scene);
            }
            EditorUtility.SetDirty(sceneGroupData);
            SaveGroups();
        }
    }

    private void RemoveGroup(SceneGroupData.SceneGroup group)
    {
        sceneGroupData.sceneGroups.Remove(group);
        selectedGroupIndex = -1;
        EditorUtility.SetDirty(sceneGroupData);
        SaveGroups();
    }

    private void OpenScene(string scenePath)
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
            sceneGroupData.AddToRecent(scenePath);
            EditorUtility.SetDirty(sceneGroupData);
        }
    }

    private void LoadAllScenesInGroup(SceneGroupData.SceneGroup group)
    {
        if (group.scenes.Count == 0)
        {
            EditorUtility.DisplayDialog("No Scenes", "This group has no scenes to load.", "OK");
            return;
        }

        // Validate that at least the first scene exists
        if (!File.Exists(group.scenes[0]))
        {
            EditorUtility.DisplayDialog("Scene Not Found",
                $"The first scene in group '{group.groupName}' could not be found:\n{group.scenes[0]}\n\nPlease refresh the scene list.",
                "OK");
            Debug.LogError($"Scene not found: {group.scenes[0]}");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("Load All cancelled by user.");
            return;
        }

        try
        {
            // Load first scene normally, then load rest additively (multi-scene editing)
            if (debugMode) Debug.Log($"[Scene Organizer] Loading scene: {group.scenes[0]}");
            EditorSceneManager.OpenScene(group.scenes[0]);

            int loadedCount = 1;
            for (int i = 1; i < group.scenes.Count; i++)
            {
                if (File.Exists(group.scenes[i]))
                {
                    if (debugMode) Debug.Log($"[Scene Organizer] Loading additive scene: {group.scenes[i]}");
                    EditorSceneManager.OpenScene(group.scenes[i], OpenSceneMode.Additive);
                    loadedCount++;
                }
                else
                {
                    Debug.LogWarning($"[Scene Organizer] Scene not found (skipped): {group.scenes[i]}");
                }
            }

            if (debugMode) Debug.Log($"<color=green>[Scene Organizer] Successfully loaded {loadedCount}/{group.scenes.Count} scene(s) from group '{group.groupName}'</color>");

            if (loadedCount < group.scenes.Count)
            {
                EditorUtility.DisplayDialog("Scenes Loaded with Warnings",
                    $"Loaded {loadedCount} out of {group.scenes.Count} scenes.\n\nSome scenes could not be found. Check the Console for details.",
                    "OK");
            }
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error Loading Scenes",
                $"An error occurred while loading scenes:\n{e.Message}",
                "OK");
            Debug.LogError($"Error loading scenes from group '{group.groupName}': {e}");
        }
    }
    #endregion

    #region Save/Load
    public void LoadGroups()
    {
        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                SceneOrganizerConstants.ASSET_PATH));
            string directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            sceneGroupData = AssetDatabase.LoadAssetAtPath<SceneGroupData>(SceneOrganizerConstants.ASSET_PATH);

            if (sceneGroupData == null)
            {
                CreateNewSceneGroupData();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load groups: {ex.Message}");
            CreateNewSceneGroupData();
        }
    }

    private void CreateNewSceneGroupData()
    {
        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                SceneOrganizerConstants.ASSET_PATH));
            string directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            sceneGroupData = CreateInstance<SceneGroupData>();
            AssetDatabase.CreateAsset(sceneGroupData, SceneOrganizerConstants.ASSET_PATH);
            AssetDatabase.SaveAssets();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create SceneGroupData: {ex.Message}");
        }
    }

    private void SaveGroups()
    {
        if (sceneGroupData == null) return;

        EditorUtility.SetDirty(sceneGroupData);
        AssetDatabase.SaveAssets();

        if (enableBackup && !string.IsNullOrEmpty(backupDirectory))
        {
            BackupGroups();
        }
    }

    private void BackupGroups()
    {
        try
        {
            if (!Directory.Exists(backupDirectory))
            {
                Directory.CreateDirectory(backupDirectory);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"SceneGroupData_Backup_{timestamp}.asset";
            string backupPath = Path.Combine(backupDirectory, backupFileName);
            string sourcePath = Path.Combine(Application.dataPath, "..", SceneOrganizerConstants.ASSET_PATH);

            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, backupPath, true);
                ManageBackupCopies();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to backup SceneGroupData: {ex.Message}");
        }
    }

    private void ManageBackupCopies()
    {
        try
        {
            if (!Directory.Exists(backupDirectory)) return;

            string[] backupFiles = Directory.GetFiles(backupDirectory, "SceneGroupData_Backup_*.asset");

            if (backupFiles.Length > SceneOrganizerConstants.MAX_BACKUP_COPIES)
            {
                Array.Sort(backupFiles);

                for (int i = 0; i < backupFiles.Length - SceneOrganizerConstants.MAX_BACKUP_COPIES; i++)
                {
                    File.Delete(backupFiles[i]);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error managing backup copies: {ex.Message}");
        }
    }

    public void UpdateBackupSettings(bool enableBackup, string backupDirectory)
    {
        this.enableBackup = enableBackup;
        this.backupDirectory = backupDirectory;

        EditorPrefs.SetBool(SceneOrganizerConstants.PREF_ENABLE_BACKUP, enableBackup);
        EditorPrefs.SetString(SceneOrganizerConstants.PREF_BACKUP_DIR, backupDirectory);
    }
    #endregion

    #region UI Helpers
    private void DrawHorizontalLine()
    {
        EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }
    #endregion

    public class ColorPickerWindow : EditorWindow
    {
        private SceneGroupData.SceneGroup group;
        private SceneOrganizerWindow parentWindow;
        private Color selectedColor;

        private static readonly Color[] presetColors = new Color[]
        {
        new Color(0.3f, 0.6f, 1f),      // Blue
        new Color(1f, 0.4f, 0.4f),      // Red
        new Color(0.4f, 0.9f, 0.4f),    // Green
        new Color(1f, 0.8f, 0.2f),      // Yellow
        new Color(0.9f, 0.4f, 0.9f),    // Magenta
        new Color(0.4f, 0.9f, 0.9f),    // Cyan
        new Color(1f, 0.6f, 0.2f),      // Orange
        new Color(0.7f, 0.5f, 1f),      // Purple
        new Color(1f, 0.5f, 0.7f),      // Pink
        new Color(0.5f, 0.8f, 0.5f),    // Light Green
        new Color(0.6f, 0.6f, 0.6f),    // Gray
        new Color(0.5f, 0.7f, 0.9f),    // Light Blue
        };

        public static void ShowWindow(SceneGroupData.SceneGroup group, SceneOrganizerWindow parent)
        {
            var window = GetWindow<ColorPickerWindow>(true, "Choose Color", true);
            window.group = group;
            window.parentWindow = parent;
            window.selectedColor = group.groupColor;
            window.minSize = new Vector2(300, 360);
            window.maxSize = new Vector2(300, 360);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Preset Colors", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // Draw preset colors in a grid
            int columns = 4;
            int rows = Mathf.CeilToInt(presetColors.Length / (float)columns);

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= presetColors.Length) break;

                    Color color = presetColors[index];
                    bool isSelected = ColorsEqual(color, selectedColor);

                    GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                    buttonStyle.margin = new RectOffset(4, 4, 4, 4);

                    Rect buttonRect = GUILayoutUtility.GetRect(60, 60, buttonStyle);

                    if (isSelected)
                    {
                        Rect borderRect = new Rect(buttonRect.x - 2, buttonRect.y - 2,
                            buttonRect.width + 4, buttonRect.height + 4);
                        EditorGUI.DrawRect(borderRect, Color.white);
                    }

                    EditorGUI.DrawRect(buttonRect, color);

                    if (GUI.Button(buttonRect, "", GUIStyle.none))
                    {
                        selectedColor = color;
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            DrawHorizontalLine();
            GUILayout.Space(10);

            // Custom color picker
            EditorGUILayout.LabelField("Custom Color", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            selectedColor = EditorGUILayout.ColorField(GUIContent.none, selectedColor,
                false, true, false, GUILayout.Width(260), GUILayout.Height(40));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Apply", GUILayout.Width(80)))
            {
                group.groupColor = selectedColor;
                EditorUtility.SetDirty(parentWindow.GetSceneGroupData());
                parentWindow.Repaint();
                Close();
            }

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
        }

        private bool ColorsEqual(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b);
        }

        private void DrawHorizontalLine()
        {
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1),
                new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }
    }

    // Helper window for editing scene notes
    public class SceneNoteEditorWindow : EditorWindow
    {
        private string scenePath;
        private AssetNotesData notesData;
        private string noteText;
        private Vector2 scrollPos;
        private SceneOrganizerWindow parentWindow;

        public static void ShowWindow(string scenePath, AssetNotesData data, SceneOrganizerWindow parent)
        {
            var window = GetWindow<SceneNoteEditorWindow>(true, "Edit Scene Note", true);
            window.minSize = new Vector2(400, 200);
            window.scenePath = scenePath;
            window.notesData = data;
            window.noteText = data != null ? data.GetNote(scenePath) : "";
            window.parentWindow = parent;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Note for: {Path.GetFileNameWithoutExtension(scenePath)}",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField($"Path: {scenePath}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            noteText = EditorGUILayout.TextArea(noteText,
                GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Clear", GUILayout.Width(80)))
            {
                noteText = "";
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                Close();
            }

            if (GUILayout.Button("Save", GUILayout.Width(80)))
            {
                if (notesData != null)
                {
                    notesData.SetNote(scenePath, noteText);
                    EditorUtility.SetDirty(notesData);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[Scene Organizer] Saved note for scene: {Path.GetFileNameWithoutExtension(scenePath)}");

                    // Repaint parent window to show the note indicator
                    if (parentWindow != null)
                    {
                        parentWindow.Repaint();
                    }
                }
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
