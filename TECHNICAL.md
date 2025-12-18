# nUtilities - Technical Architecture

This document provides technical details for developers who want to understand, modify, or extend nUtilities.

## 🏗️ Architecture Overview

nUtilities follows a modular architecture with three main subsystems:

1. **Favorites System** - Asset bookmarking and organization
2. **Scene Organizer** - Scene management and grouping
3. **nSearch** - Quick search and navigation
4. **Shared Notes System** - Cross-window asset annotation

## 📁 File Structure

```
com.nutilities.editor/
├── package.json                          # UPM package manifest
├── Editor/
│   ├── com.nutilities.editor.asmdef      # Assembly definition
│   └── nUtils/
│       ├── Core Components
│       ├── AssetNotesData.cs             # Shared notes data (ScriptableObject)
│       ├── FavoritesData.cs              # Favorites data structure
│       ├── FavoritesWindow.cs            # Main favorites window (EditorWindow)
│       ├── nUtilsWelcome.cs              # Welcome/onboarding window
│       │
│       ├── SceneOrganizer/               # Scene management subsystem
│       │   ├── SceneOrganizerWindow.cs   # Main window (split-view)
│       │   ├── SceneGroupData.cs         # Scene groups data
│       │   ├── SceneOrganizerConstants.cs # Constants/preferences keys
│       │   ├── CreateNewSceneWindow.cs   # Scene creation dialog
│       │   └── SettingsWindow.cs         # Settings dialog
│       │
│       └── nSearch/                      # Search subsystem
│           ├── SpotlightSearch.cs        # Main search window
│           ├── HierarchySearcher.cs      # Scene hierarchy search
│           ├── CreateMenuSearcher.cs     # Create menu search
│           ├── FileUtilities.cs          # File system utilities
│           ├── WindowUtilities.cs        # Window management utilities
│           ├── ArithmeticEvaluator.cs    # Basic calculator
│           ├── AdvancedMathEvalulator.cs # Advanced math functions
│           └── TrieNode.cs               # Trie data structure for search
```

## 🧩 Component Details

### 1. Favorites System

**Main Class:** `FavoritesWindow : EditorWindow`

**Data Model:**
```csharp
// FavoritesData.cs
[CreateAssetMenu]
public class FavoritesData : ScriptableObject
{
    public List<FavoriteGroup> groups;
    public List<FavoriteItem> ungroupedItems;
}

[Serializable]
public class FavoriteGroup
{
    public string name;
    public Color color;
    public bool isExpanded;
    public string note;
    public List<FavoriteItem> items;
    public List<FavoriteGroup> childGroups;  // Nested groups
    public int indentLevel;
}

[Serializable]
public class FavoriteItem
{
    public Object asset;
    public string note;  // Deprecated - now uses AssetNotesData
}
```

**Key Features:**
- **IMGUI-based** for maximum compatibility
- **Drag-and-drop** using Unity's DragAndDrop API
- **Event-driven** item management
- **Hierarchical groups** with unlimited nesting
- **Grid/List views** with dynamic layout

**Data Persistence:**
- Location: `Assets/Editor/FavoritesData.asset`
- Type: ScriptableObject
- Saved via: `EditorUtility.SetDirty()` + `AssetDatabase.SaveAssets()`

**Integration Points:**
```csharp
// Context menu integration
[MenuItem("Assets/Add to Favorites", false, 2000)]
private static void AddToFavoritesContextMenu()

// Window menu integration
[MenuItem("Window/Favorites ⭐")]
[MenuItem("Window/nUtilities/Favorites ⭐")]
public static void ShowWindow()
```

### 2. Scene Organizer

**Main Class:** `SceneOrganizerWindow : EditorWindow`

**Data Model:**
```csharp
// SceneGroupData.cs
[CreateAssetMenu]
public class SceneGroupData : ScriptableObject
{
    [Serializable]
    public class SceneGroup
    {
        public string name;
        public Color color;
        public List<string> scenes;  // Scene asset paths
        public bool isExpanded;
        public string note;
    }
    
    public List<SceneGroup> groups;
    public List<string> recentScenes;
    public int maxRecentScenes = 10;
}
```

**Architecture:**
- **Split-view layout** with resizable divider
- **Left panel:** Scene list with search
- **Right panel:** Group management
- **Settings system** using EditorPrefs

**Scene Loading:**
```csharp
// Single scene (additive or single mode)
EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

// Batch loading
foreach (var scenePath in group.scenes)
{
    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
}
```

**Backup System:**
```csharp
// Automatic backups to prevent data loss
private void BackupGroups()
{
    string backupPath = Path.Combine(backupDirectory, 
        $"SceneGroupData_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.asset");
    AssetDatabase.CopyAsset(assetPath, backupPath);
}
```

**Keyboard Shortcuts:**
- Registered via `[MenuItem("Edit/...", false, priority)]`
- Handled in `HandleKeyboardShortcuts()`
- Uses `Event.current` for key detection

### 3. nSearch

**Main Class:** `SpotlightSearch : EditorWindow`

**Search Modes:**
1. **Hierarchy Search** - Scene GameObjects
2. **Asset Search** - Project assets
3. **Create Menu** - GameObject creation
4. **Calculator** - Math expressions

**Search Algorithm:**
```csharp
// Fuzzy matching using Trie data structure
public class TrieNode
{
    public Dictionary<char, TrieNode> children;
    public bool isEndOfWord;
    public object data;
}

// Matching algorithm
private bool FuzzyMatch(string query, string target)
{
    int queryIndex = 0;
    foreach (char c in target.ToLower())
    {
        if (queryIndex < query.Length && 
            c == query[queryIndex])
            queryIndex++;
    }
    return queryIndex == query.Length;
}
```

**Calculator Implementation:**
```csharp
// ArithmeticEvaluator.cs - Basic operations
public static double Evaluate(string expression)
{
    // Shunting-yard algorithm
    // Handles: +, -, *, /, (, ), ^
}

// AdvancedMathEvaluator.cs - Functions
public static double Evaluate(string expression)
{
    // Pattern matching for functions:
    // sin, cos, tan, sqrt, log, abs, etc.
}
```

**Shortcut Registration:**
```csharp
[MenuItem("Window/nUtilities/nSearch %k")]  // Ctrl/Cmd + K
public static void ShowWindow()
{
    var window = GetWindow<SpotlightSearch>();
    window.ShowPopup();
}
```

### 4. Shared Notes System

**Core Class:** `AssetNotesData : ScriptableObject`

**Architecture:**
```csharp
[CreateAssetMenu]
public class AssetNotesData : ScriptableObject
{
    [Serializable]
    public class AssetNote
    {
        public string assetPath;  // Key: Asset path
        public string note;       // Value: Note text
    }
    
    [SerializeField]
    private List<AssetNote> assetNotes;
    
    // Public API
    public string GetNote(string assetPath);
    public void SetNote(string assetPath, string noteText);
    public bool HasNote(string assetPath);
    public void RemoveNote(string assetPath);
}
```

**Integration Pattern:**
```csharp
// In any window (Favorites, Scene Organizer, etc.)
public class MyWindow : EditorWindow
{
    private AssetNotesData assetNotesData;
    
    private void OnEnable()
    {
        LoadAssetNotes();
    }
    
    private void LoadAssetNotes()
    {
        string path = "Assets/Editor/AssetNotesData.asset";
        assetNotesData = AssetDatabase.LoadAssetAtPath<AssetNotesData>(path);
        
        if (assetNotesData == null)
        {
            assetNotesData = CreateInstance<AssetNotesData>();
            AssetDatabase.CreateAsset(assetNotesData, path);
            AssetDatabase.SaveAssets();
        }
    }
    
    // Usage
    string assetPath = AssetDatabase.GetAssetPath(asset);
    if (assetNotesData.HasNote(assetPath))
    {
        string note = assetNotesData.GetNote(assetPath);
        // Display note indicator
    }
}
```

**Why Asset Paths?**
- **Unique identifiers** across Unity projects
- **Persistent** across window instances
- **Sharable** between different tools
- **Version control friendly** (text-based)

## 🎨 UI/UX Patterns

### IMGUI Best Practices

**1. Event Handling Order:**
```csharp
private void OnGUI()
{
    DrawToolbar();
    
    // Draw content first (specific handlers)
    DrawContent();
    
    // General handlers last (fallback)
    HandleDragAndDrop();
    
    DrawFooter();
}
```

**2. Drag and Drop:**
```csharp
// DragUpdated - Show visual feedback
if (Event.current.type == EventType.DragUpdated)
{
    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
    Event.current.Use();
}

// DragPerform - Execute action
if (Event.current.type == EventType.DragPerform)
{
    DragAndDrop.AcceptDrag();
    // Process DragAndDrop.objectReferences
    Event.current.Use();  // CRITICAL: Prevent other handlers
}
```

**3. Scroll Views:**
```csharp
Rect scrollRect = new Rect(0, 22, position.width, height);
Rect viewRect = new Rect(0, 0, position.width - 20, contentHeight);

scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);
// Draw content
GUI.EndScrollView();
```

**4. Custom Context Menus:**
```csharp
private void ShowContextMenu()
{
    GenericMenu menu = new GenericMenu();
    
    menu.AddItem(new GUIContent("Action"), false, () => {
        // Handler
    });
    
    menu.AddSeparator("");
    menu.ShowAsContext();
}
```

### Layout Calculations

**Dynamic Height Calculation:**
```csharp
private float CalculateContentHeight()
{
    float height = 10;
    
    // Ungrouped section
    height += 25;  // Header
    height += ungroupedItems.Count * 45;  // Items
    
    // Groups
    foreach (var group in groups)
    {
        height += 25;  // Group header
        if (group.isExpanded)
        {
            height += group.items.Count * 45;
        }
    }
    
    return height;
}
```

## 🔧 Extension Points

### Adding New Features

**1. New Window Component:**
```csharp
using UnityEditor;
using UnityEngine;

public class MyUtilityWindow : EditorWindow
{
    [MenuItem("Window/nUtilities/My Utility")]
    public static void ShowWindow()
    {
        var window = GetWindow<MyUtilityWindow>("My Utility");
        window.Show();
    }
    
    private void OnGUI()
    {
        // Your UI
    }
}
```

**2. Integrate with Notes System:**
```csharp
public class MyWindow : EditorWindow
{
    private AssetNotesData notesData;
    
    private void OnEnable()
    {
        notesData = AssetDatabase.LoadAssetAtPath<AssetNotesData>(
            "Assets/Editor/AssetNotesData.asset");
    }
    
    private void DrawAssetWithNote(Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        if (notesData.HasNote(path))
        {
            EditorGUILayout.LabelField("📝 " + notesData.GetNote(path));
        }
    }
}
```

**3. Add Custom Search Mode:**
```csharp
// In SpotlightSearch.cs
public enum SearchMode
{
    Hierarchy,
    Assets,
    CreateMenu,
    Calculator,
    MyCustomMode  // Add here
}

private void PerformSearch()
{
    switch (currentMode)
    {
        case SearchMode.MyCustomMode:
            PerformMyCustomSearch();
            break;
    }
}
```

### Data Migration

**Adding Fields to Existing Data:**
```csharp
// In FavoritesData.cs
[Serializable]
public class FavoriteGroup
{
    // Existing fields
    public string name;
    public Color color;
    
    // New field with default value
    [SerializeField]
    private string newField = "default";
    
    public string NewField
    {
        get { return newField ?? "default"; }
        set { newField = value; }
    }
}
```

**Version Migration:**
```csharp
private void OnEnable()
{
    LoadOrCreateData();
    MigrateDataIfNeeded();
}

private void MigrateDataIfNeeded()
{
    if (favoritesData.version < 2)
    {
        // Migrate from v1 to v2
        foreach (var group in favoritesData.groups)
        {
            if (string.IsNullOrEmpty(group.newField))
                group.newField = "migrated";
        }
        favoritesData.version = 2;
        EditorUtility.SetDirty(favoritesData);
    }
}
```

## 🐛 Debugging

### Enable Debug Logging

```csharp
// Add to any window
private bool debugMode = false;

private void DrawToolbar()
{
    debugMode = GUILayout.Toggle(debugMode, "Debug", EditorStyles.toolbarButton);
}

private void Log(string message)
{
    if (debugMode)
        Debug.Log($"[{GetType().Name}] {message}");
}
```

### Common Issues

**1. Events Not Working:**
```csharp
// Problem: Event already consumed
if (Event.current.type == EventType.Used)
{
    Debug.Log("Event was already consumed!");
    return;
}

// Solution: Check event order in OnGUI
```

**2. Data Not Saving:**
```csharp
// Always mark dirty and save
EditorUtility.SetDirty(dataObject);
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();  // If needed
```

**3. Drag State Not Resetting:**
```csharp
// Always reset drag state
if (Event.current.type == EventType.DragPerform ||
    Event.current.type == EventType.DragExited)
{
    draggedItem = null;
    draggedToGroup = null;
    dragToUngrouped = false;
}
```

## 🧪 Testing

### Manual Testing Checklist

**Favorites:**
- [ ] Add single asset via context menu
- [ ] Add multiple assets
- [ ] Drag asset from project to window
- [ ] Drag folder to create nested groups
- [ ] Move item between groups
- [ ] Toggle grid/list view
- [ ] Search functionality
- [ ] Add/edit/clear notes

**Scene Organizer:**
- [ ] Create new group
- [ ] Add scenes to group
- [ ] Open single scene
- [ ] Load all in group
- [ ] Rename scene
- [ ] Search scenes
- [ ] Toggle sort modes
- [ ] Add/edit notes
- [ ] Backup system

**nSearch:**
- [ ] Open with Ctrl/Cmd+K
- [ ] Search hierarchy
- [ ] Search assets
- [ ] Create GameObject
- [ ] Calculator (basic)
- [ ] Calculator (advanced)

**Notes System:**
- [ ] Add note in Favorites
- [ ] Verify note shows in Scene Organizer
- [ ] Add note in Scene Organizer
- [ ] Verify note shows in Favorites
- [ ] Clear note
- [ ] Long text handling

## 📊 Performance Considerations

### Optimization Tips

**1. Lazy Loading:**
```csharp
// Don't load all previews at once
private Dictionary<Object, Texture2D> previewCache = new Dictionary<Object, Texture2D>();

private Texture2D GetPreview(Object obj)
{
    if (!previewCache.ContainsKey(obj))
    {
        previewCache[obj] = AssetPreview.GetAssetPreview(obj);
    }
    return previewCache[obj];
}
```

**2. Avoid Unnecessary Repaints:**
```csharp
private void OnGUI()
{
    EditorGUI.BeginChangeCheck();
    // Draw UI
    if (EditorGUI.EndChangeCheck())
    {
        Repaint();  // Only repaint when needed
    }
}
```

**3. Cache Calculations:**
```csharp
private float cachedContentHeight = -1;

private float CalculateContentHeight()
{
    if (cachedContentHeight < 0)
    {
        cachedContentHeight = /* calculate */;
    }
    return cachedContentHeight;
}

// Invalidate cache when data changes
private void OnDataChanged()
{
    cachedContentHeight = -1;
}
```

## 🔐 Best Practices

1. **Always use EditorUtility.SetDirty()** before saving ScriptableObjects
2. **Call Event.current.Use()** to prevent event propagation
3. **Handle null references** for deleted assets
4. **Use AssetDatabase paths** not system paths
5. **Implement OnEnable/OnDisable** for proper initialization
6. **Cache expensive operations** (previews, searches)
7. **Use SerializeField** for private fields that need persistence
8. **Validate data** after deserialization
9. **Provide defaults** for new fields (migration)
10. **Log errors** with context for debugging

## 📚 Unity API Reference

**Key Classes Used:**
- `EditorWindow` - Base class for all windows
- `ScriptableObject` - Data persistence
- `AssetDatabase` - Asset management
- `EditorPrefs` - User preferences
- `EditorGUIUtility` - GUI utilities
- `GenericMenu` - Context menus
- `DragAndDrop` - Drag and drop operations
- `Event` - Input events
- `EditorSceneManager` - Scene management

## 🤝 Contributing

When contributing code:
1. Follow existing code style
2. Add XML documentation to public methods
3. Test in clean Unity project
4. Update CHANGELOG.md
5. Increment version in package.json
6. Add debug logging for complex operations

---

**For questions about architecture, open an issue with the `question` label.**

**Last Updated:** 2024
**Architecture Version:** 1.0.0