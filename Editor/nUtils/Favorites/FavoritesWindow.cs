using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class FavoritesWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private FavoritesData favoritesData;
    private AssetNotesData assetNotesData;
    private const string DATA_PATH = "Assets/Editor/FavoritesData.asset";

    private string searchQuery = "";
    private bool showGridView = false;

    // Drag state
    private FavoriteItem draggedItem;
    private FavoriteGroup draggedToGroup;
    private bool dragToUngrouped;

    [MenuItem("Window/nUtilities/Favorites")]
    public static void ShowWindow()
    {
        var window = GetWindow<FavoritesWindow>("Favorites");
        window.minSize = new Vector2(300, 200);
        window.Show();
    }

    [MenuItem("Assets/Add to Favorites", false, 2000)]
    private static void AddToFavoritesContextMenu()
    {
        // Load favorites data
        string dataPath = "Assets/Editor/FavoritesData.asset";
        FavoritesData data = AssetDatabase.LoadAssetAtPath<FavoritesData>(dataPath);

        if (data == null)
        {
            // Create if doesn't exist
            if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            {
                AssetDatabase.CreateFolder("Assets", "Editor");
            }

            data = CreateInstance<FavoritesData>();
            data.ungroupedItems = new List<FavoriteItem>();
            data.groups = new List<FavoriteGroup>();
            AssetDatabase.CreateAsset(data, dataPath);
            AssetDatabase.SaveAssets();
        }

        // Get selected objects
        Object[] selectedObjects = Selection.objects;
        int addedCount = 0;
        List<string> alreadyExists = new List<string>();

        foreach (Object obj in selectedObjects)
        {
            if (obj != null)
            {
                // Check if already in favorites
                bool exists = data.ungroupedItems.Any(i => i != null && i.asset == obj);

                if (!exists)
                {
                    // Check in groups too
                    foreach (var group in data.groups)
                    {
                        if (group.items.Any(i => i != null && i.asset == obj))
                        {
                            exists = true;
                            break;
                        }
                    }
                }

                if (!exists)
                {
                    data.ungroupedItems.Add(new FavoriteItem
                    {
                        asset = obj,
                        note = ""
                    });
                    addedCount++;
                }
                else
                {
                    alreadyExists.Add(obj.name);
                }
            }
        }

        if (addedCount > 0)
        {
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            string message = addedCount == 1
                ? $"Added 1 item to Favorites"
                : $"Added {addedCount} items to Favorites";

            if (alreadyExists.Count > 0)
            {
                message += $" ({alreadyExists.Count} already favorited)";
            }

            Debug.Log($"[Favorites] {message}");

            // Try to update any open Favorites window
            var window = Resources.FindObjectsOfTypeAll<FavoritesWindow>().FirstOrDefault();
            if (window != null)
            {
                window.Repaint();
            }
        }
        else if (alreadyExists.Count > 0)
        {
            Debug.Log($"[Favorites] Selected item(s) already in favorites");
        }
    }

    [MenuItem("Assets/Add to Favorites", true)]
    private static bool AddToFavoritesContextMenuValidate()
    {
        // Only show menu item if something is selected
        return Selection.objects != null && Selection.objects.Length > 0;
    }

    private void OnEnable()
    {
        LoadOrCreateData();
        LoadAssetNotes();
    }

    private void LoadOrCreateData()
    {
        favoritesData = AssetDatabase.LoadAssetAtPath<FavoritesData>(DATA_PATH);

        if (favoritesData == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            {
                AssetDatabase.CreateFolder("Assets", "Editor");
            }

            favoritesData = CreateInstance<FavoritesData>();
            favoritesData.groups = new List<FavoriteGroup>();
            favoritesData.ungroupedItems = new List<FavoriteItem>();

            AssetDatabase.CreateAsset(favoritesData, DATA_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Ensure lists are initialized
        if (favoritesData.groups == null)
            favoritesData.groups = new List<FavoriteGroup>();
        if (favoritesData.ungroupedItems == null)
            favoritesData.ungroupedItems = new List<FavoriteItem>();
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
            Debug.Log("[Favorites] Created AssetNotesData at " + assetPath);
        }
        else
        {
            Debug.Log($"[Favorites] Loaded AssetNotesData with {assetNotesData.GetNoteCount()} note(s)");
        }
    }

    private void OnGUI()
    {
        if (favoritesData == null)
        {
            LoadOrCreateData();
            return;
        }

        DrawToolbar();

        EditorGUILayout.Space(2);

        // Calculate height for scrollable area
        float scrollHeight = position.height - 60; // 40 for toolbar, 20 for footer

        // Scrollable content area
        Rect scrollRect = new Rect(0, 22, position.width, scrollHeight);
        Rect viewRect = new Rect(0, 0, position.width - 20,
            CalculateContentHeight());

        scrollPosition = GUI.BeginScrollView(scrollRect, scrollPosition, viewRect);

        GUILayout.BeginArea(new Rect(0, 0, viewRect.width, viewRect.height));

        if (string.IsNullOrEmpty(searchQuery))
        {
            DrawContent();
        }
        else
        {
            DrawSearchResults();
        }

        GUILayout.EndArea();
        GUI.EndScrollView();

        // Handle drag and drop at window level (after content, so group handlers process first)
        HandleDragAndDrop();

        DrawFooter();

        // Display tooltip
        if (!string.IsNullOrEmpty(GUI.tooltip))
        {
            Repaint();
        }
    }

    private float CalculateContentHeight()
    {
        float height = 10;

        // Ungrouped section
        height += 30; // Header
        if (favoritesData.ungroupedItems.Count > 0)
        {
            if (showGridView)
            {
                int itemsPerRow = Mathf.Max(1, (int)(position.width - 50) / 80);
                int rows = Mathf.CeilToInt(favoritesData.ungroupedItems.Count /
                    (float)itemsPerRow);
                height += rows * 95;
            }
            else
            {
                height += favoritesData.ungroupedItems.Count * 50;
            }
        }
        else
        {
            height += 40; // Empty message
        }

        height += 15;

        // Groups (recursively calculate including nested groups)
        foreach (var group in favoritesData.groups)
        {
            height += CalculateGroupHeight(group);
        }

        height += 20;

        return height;
    }

    private float CalculateGroupHeight(FavoriteGroup group)
    {
        float height = 0;

        // Group header
        height += 30;

        if (group.isExpanded)
        {
            // Items in this group
            if (group.items.Count > 0)
            {
                if (showGridView)
                {
                    int itemsPerRow = Mathf.Max(1, (int)(position.width - 50) / 80);
                    int rows = Mathf.CeilToInt(group.items.Count / (float)itemsPerRow);
                    height += rows * 95;
                }
                else
                {
                    height += group.items.Count * 50;
                }
            }

            // Empty message if no items and no child groups
            if (group.items.Count == 0 && group.childGroups.Count == 0)
            {
                height += 40;
            }

            // Add spacing between items and child groups
            if (group.items.Count > 0 && group.childGroups.Count > 0)
            {
                height += 3;
            }

            // Recursively calculate child groups
            foreach (var childGroup in group.childGroups)
            {
                height += CalculateGroupHeight(childGroup);
            }
        }

        height += 10; // Bottom spacing

        return height;
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Search
        GUI.SetNextControlName("SearchField");
        string newSearch = EditorGUILayout.TextField(searchQuery,
            EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
        if (newSearch != searchQuery)
        {
            searchQuery = newSearch;
            Repaint();
        }

        if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            searchQuery = "";
            GUI.FocusControl(null);
        }

        GUILayout.Space(5);

        // View toggle
        showGridView = GUILayout.Toggle(showGridView,
            showGridView ? "▦" : "☰", EditorStyles.toolbarButton,
            GUILayout.Width(25));

        // Collapse/Expand all
        if (GUILayout.Button("⊟", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            foreach (var group in favoritesData.groups)
                group.isExpanded = false;
            EditorUtility.SetDirty(favoritesData);
        }
        if (GUILayout.Button("⊞", EditorStyles.toolbarButton, GUILayout.Width(25)))
        {
            foreach (var group in favoritesData.groups)
                group.isExpanded = true;
            EditorUtility.SetDirty(favoritesData);
        }

        GUILayout.Space(5);

        // Add group button
        if (GUILayout.Button("+ Group", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            AddGroupWindow.ShowWindow(favoritesData);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawContent()
    {
        // Clean up nulls
        favoritesData.ungroupedItems.RemoveAll(item =>
            item == null || item.asset == null);
        foreach (var group in favoritesData.groups)
        {
            if (group.items == null)
                group.items = new List<FavoriteItem>();
            group.items.RemoveAll(item => item == null || item.asset == null);
        }

        bool hasAnyItems = favoritesData.ungroupedItems.Count > 0 ||
            favoritesData.groups.Any(g => g.items.Count > 0);
        bool hasGroups = favoritesData.groups.Count > 0;

        // Only show empty state if there are no groups AND no items
        if (!hasAnyItems && !hasGroups)
        {
            DrawEmptyState();
            return;
        }

        // Draw ungrouped section
        DrawUngroupedSection();

        EditorGUILayout.Space(5);

        // Draw groups
        for (int i = 0; i < favoritesData.groups.Count; i++)
        {
            DrawGroup(favoritesData.groups[i], i);
        }

        EditorGUILayout.Space(10);
    }

    private void DrawUngroupedSection()
    {
        // Header background
        Rect headerRect = GUILayoutUtility.GetRect(position.width - 20, 25);

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(headerRect, new Color(0.25f, 0.25f, 0.25f, 0.3f));
        }

        GUI.Label(new Rect(headerRect.x + 5, headerRect.y + 5,
            headerRect.width - 10, 20),
            $"📌 Ungrouped ({favoritesData.ungroupedItems.Count})",
            EditorStyles.boldLabel);

        // Handle drag onto ungrouped header
        if (Event.current.type == EventType.DragUpdated &&
            headerRect.Contains(Event.current.mousePosition))
        {
            if (draggedItem != null)
            {
                dragToUngrouped = true;
                draggedToGroup = null;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            Event.current.Use();
        }

        if (Event.current.type == EventType.DragPerform &&
            headerRect.Contains(Event.current.mousePosition))
        {
            if (draggedItem != null && dragToUngrouped)
            {
                // Remove from groups
                foreach (var g in favoritesData.groups)
                {
                    if (g.items.Contains(draggedItem))
                    {
                        g.items.Remove(draggedItem);
                        break;
                    }
                }

                // Add to ungrouped if not already there
                if (!favoritesData.ungroupedItems.Contains(draggedItem))
                {
                    favoritesData.ungroupedItems.Add(draggedItem);
                }

                draggedItem = null;
                dragToUngrouped = false;

                EditorUtility.SetDirty(favoritesData);
                AssetDatabase.SaveAssets();
                Event.current.Use();
            }
        }

        // Draw items
        if (favoritesData.ungroupedItems.Count == 0)
        {
            EditorGUILayout.HelpBox("No ungrouped items", MessageType.None);
        }
        else
        {
            if (showGridView)
            {
                DrawItemsGrid(favoritesData.ungroupedItems, null);
            }
            else
            {
                DrawItemsList(favoritesData.ungroupedItems, null);
            }
        }
    }

    private void DrawGroup(FavoriteGroup group, int groupIndex)
    {
        // Calculate indentation
        float indent = group.indentLevel * 20f;

        // Group header background
        Rect headerRect = GUILayoutUtility.GetRect(position.width - 20 - indent, 25);
        headerRect.x += indent;

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(headerRect, group.color);
        }

        // Foldout and label
        Rect foldoutRect = new Rect(headerRect.x + 5, headerRect.y + 4, 15, 15);
        group.isExpanded = EditorGUI.Foldout(foldoutRect, group.isExpanded, "");

        // Calculate total item count including child groups
        int totalItems = CountAssetsInGroup(group);
        string countText = group.childGroups.Count > 0
            ? $"{group.name} ({group.items.Count} | {totalItems} total)"
            : $"{group.name} ({group.items.Count})";

        // Note indicator with tooltip
        if (!string.IsNullOrWhiteSpace(group.note))
        {
            Rect noteRect = new Rect(headerRect.x + headerRect.width - 55,
                headerRect.y + 4, 20, 20);

            GUI.Label(noteRect, new GUIContent("📝", group.note));
        }

        GUI.Label(new Rect(headerRect.x + 25, headerRect.y + 5,
            headerRect.width - 90, 20),
            countText,
            EditorStyles.boldLabel);

        // Settings button
        if (GUI.Button(new Rect(headerRect.x + headerRect.width - 30,
            headerRect.y + 2, 25, 20), "⚙", EditorStyles.miniButton))
        {
            ShowGroupContextMenu(group, groupIndex);
        }

        // Handle drag onto group header
        if (Event.current.type == EventType.DragUpdated &&
            headerRect.Contains(Event.current.mousePosition))
        {
            // Internal drag (moving between groups)
            if (draggedItem != null && draggedToGroup != group)
            {
                draggedToGroup = group;
                dragToUngrouped = false;
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
            }
            // External drag (from project)
            else if (DragAndDrop.objectReferences != null &&
                     DragAndDrop.objectReferences.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }

            Event.current.Use();
        }

        if (Event.current.type == EventType.DragPerform &&
            headerRect.Contains(Event.current.mousePosition))
        {
            DragAndDrop.AcceptDrag();

            // Internal drag
            if (draggedItem != null && draggedToGroup == group)
            {
                // Remove from ungrouped
                favoritesData.ungroupedItems.Remove(draggedItem);

                // Remove from other groups
                foreach (var g in favoritesData.groups)
                {
                    if (g.items.Contains(draggedItem))
                    {
                        g.items.Remove(draggedItem);
                        break;
                    }
                }

                // Add to target group
                if (!group.items.Contains(draggedItem))
                {
                    group.items.Add(draggedItem);
                }

                draggedItem = null;
                draggedToGroup = null;

                EditorUtility.SetDirty(favoritesData);
                AssetDatabase.SaveAssets();
            }
            // External drag from project
            else if (DragAndDrop.objectReferences != null &&
                     DragAndDrop.objectReferences.Length > 0)
            {
                List<string> alreadyFavorited = new List<string>();
                int addedCount = 0;

                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject != null)
                    {
                        string location = FindItemLocation(draggedObject);

                        if (location != null)
                        {
                            alreadyFavorited.Add($"• {draggedObject.name} (in {location})");
                        }
                        else
                        {
                            // Add to this group
                            group.items.Add(new FavoriteItem
                            {
                                asset = draggedObject,
                                note = ""
                            });
                            addedCount++;
                        }
                    }
                }

                if (alreadyFavorited.Count > 0)
                {
                    string message = "The following items are already favorited:\n\n";
                    message += string.Join("\n", alreadyFavorited);

                    if (addedCount > 0)
                    {
                        message += $"\n\n✓ Added {addedCount} new item(s) to '{group.name}'.";
                    }

                    EditorUtility.DisplayDialog("Duplicate Favorites", message, "OK");
                }

                if (addedCount > 0)
                {
                    EditorUtility.SetDirty(favoritesData);
                    AssetDatabase.SaveAssets();
                }

                Repaint();
            }

            Event.current.Use();
        }

        // Group items
        if (group.isExpanded)
        {
            if (group.items.Count == 0 && group.childGroups.Count == 0)
            {
                EditorGUILayout.HelpBox("Drop items here", MessageType.None);
            }
            else
            {
                // Draw items in this group
                if (group.items.Count > 0)
                {
                    if (showGridView)
                    {
                        DrawItemsGrid(group.items, group);
                    }
                    else
                    {
                        DrawItemsList(group.items, group);
                    }
                }

                // Draw child groups recursively
                if (group.childGroups.Count > 0)
                {
                    if (group.items.Count > 0)
                    {
                        EditorGUILayout.Space(3);
                    }

                    for (int i = 0; i < group.childGroups.Count; i++)
                    {
                        DrawGroup(group.childGroups[i], -1);
                    }
                }
            }
        }

        EditorGUILayout.Space(5);
    }
//Test
    private void DrawItemsList(List<FavoriteItem> items, FavoriteGroup group)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null || item.asset == null) continue;

            DrawFavoriteItem(item, items, i, group);
        }
    }

    private void DrawItemsGrid(List<FavoriteItem> items, FavoriteGroup group)
    {
        int itemsPerRow = Mathf.Max(1, (int)(position.width - 50) / 80);
        int rows = Mathf.CeilToInt(items.Count / (float)itemsPerRow);

        for (int row = 0; row < rows; row++)
        {
            GUILayout.BeginHorizontal();

            for (int col = 0; col < itemsPerRow; col++)
            {
                int index = row * itemsPerRow + col;
                if (index >= items.Count) break;

                var item = items[index];
                if (item == null || item.asset == null) continue;

                DrawFavoriteItemGrid(item, items, index, group);
            }

            GUILayout.EndHorizontal();
        }
    }

    private void DrawFavoriteItem(FavoriteItem item, List<FavoriteItem> items,
        int index, FavoriteGroup group)
    {
        Rect itemRect = GUILayoutUtility.GetRect(position.width - 40, 45);
        GUI.Box(itemRect, "", GUI.skin.box);

        // Drag handle
        Rect dragRect = new Rect(itemRect.x + 5, itemRect.y + 12, 20, 20);
        GUI.Label(dragRect, "⋮⋮", EditorStyles.boldLabel);

        HandleItemDrag(dragRect, item);

        // Icon
        Texture2D icon = AssetPreview.GetMiniThumbnail(item.asset);
        GUI.DrawTexture(new Rect(itemRect.x + 30, itemRect.y + 12, 20, 20),
            icon);

        // Name and type
        GUI.Label(new Rect(itemRect.x + 55, itemRect.y + 8,
            itemRect.width - 180, 18),
            item.asset.name, EditorStyles.boldLabel);
        GUI.Label(new Rect(itemRect.x + 55, itemRect.y + 25,
            itemRect.width - 180, 15),
            item.asset.GetType().Name, EditorStyles.miniLabel);

        // Note indicator with tooltip (check shared notes system)
        string assetPath = AssetDatabase.GetAssetPath(item.asset);
        bool hasNote = assetNotesData != null && assetNotesData.HasNote(assetPath);

        if (hasNote)
        {
            string noteText = assetNotesData.GetNote(assetPath);
            Rect noteRect = new Rect(itemRect.x + itemRect.width - 120,
                itemRect.y + 12, 20, 20);

            GUI.Label(noteRect, new GUIContent("📝", noteText));

            // Show tooltip on hover
            if (noteRect.Contains(Event.current.mousePosition))
            {
                GUI.tooltip = noteText;
            }
        }

        // Ping button
        if (GUI.Button(new Rect(itemRect.x + itemRect.width - 90,
            itemRect.y + 12, 25, 20), "→"))
        {
            EditorGUIUtility.PingObject(item.asset);
            Selection.activeObject = item.asset;
        }

        // More options
        if (GUI.Button(new Rect(itemRect.x + itemRect.width - 60,
            itemRect.y + 12, 25, 20), "⋯"))
        {
            ShowItemContextMenu(item, items, index, group);
        }

        // Remove button
        if (GUI.Button(new Rect(itemRect.x + itemRect.width - 30,
            itemRect.y + 12, 25, 20), "✕"))
        {
            items.RemoveAt(index);
            EditorUtility.SetDirty(favoritesData);
            AssetDatabase.SaveAssets();
        }

        // Handle double-click
        if (Event.current.type == EventType.MouseDown &&
            Event.current.clickCount == 2 &&
            itemRect.Contains(Event.current.mousePosition))
        {
            EditorGUIUtility.PingObject(item.asset);
            Selection.activeObject = item.asset;
            Event.current.Use();
        }
    }

    private void DrawFavoriteItemGrid(FavoriteItem item, List<FavoriteItem> items,
        int index, FavoriteGroup group)
    {
        Rect itemRect = GUILayoutUtility.GetRect(70, 85);
        GUI.Box(itemRect, "", GUI.skin.box);

        // Preview
        Texture2D preview = AssetPreview.GetAssetPreview(item.asset);
        if (preview == null)
            preview = AssetPreview.GetMiniThumbnail(item.asset);

        Rect previewRect = new Rect(itemRect.x + 5, itemRect.y + 5, 60, 60);
        GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);

        HandleItemDrag(previewRect, item);

        // Note indicator with tooltip (check shared notes system)
        string assetPath = AssetDatabase.GetAssetPath(item.asset);
        bool hasNote = assetNotesData != null && assetNotesData.HasNote(assetPath);

        if (hasNote)
        {
            string noteText = assetNotesData.GetNote(assetPath);
            Rect noteIconRect = new Rect(previewRect.x + 45, previewRect.y + 2, 15, 15);
            GUI.Label(noteIconRect, new GUIContent("📝", noteText));

            // Show tooltip on hover
            if (noteIconRect.Contains(Event.current.mousePosition))
            {
                GUI.tooltip = noteText;
            }
        }

        // Name
        GUIStyle nameStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        GUI.Label(new Rect(itemRect.x, itemRect.y + 67, 70, 18),
            item.asset.name, nameStyle);

        // Context menu on right-click
        if (Event.current.type == EventType.ContextClick &&
            itemRect.Contains(Event.current.mousePosition))
        {
            ShowItemContextMenu(item, items, index, group);
            Event.current.Use();
        }

        // Double-click
        if (Event.current.type == EventType.MouseDown &&
            Event.current.clickCount == 2 &&
            itemRect.Contains(Event.current.mousePosition))
        {
            EditorGUIUtility.PingObject(item.asset);
            Selection.activeObject = item.asset;
            Event.current.Use();
        }
    }

    private void HandleItemDrag(Rect dragRect, FavoriteItem item)
    {
        Event evt = Event.current;
        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        switch (evt.type)
        {
            case EventType.MouseDown:
                if (dragRect.Contains(evt.mousePosition) && evt.button == 0)
                {
                    GUIUtility.hotControl = controlID;
                    draggedItem = item;
                    evt.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlID && draggedItem == item)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[] { item.asset };
                    DragAndDrop.StartDrag(item.asset.name);
                    GUIUtility.hotControl = 0;
                    evt.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                }
                break;
        }
    }

    private string FindItemLocation(Object asset)
    {
        // Check ungrouped
        if (favoritesData.ungroupedItems.Any(i => i != null && i.asset == asset))
        {
            return "Ungrouped";
        }

        // Check groups
        foreach (var group in favoritesData.groups)
        {
            if (group.items.Any(i => i != null && i.asset == asset))
            {
                return group.name;
            }
        }

        return null;
    }

    private void HandleDragAndDrop()
    {
        Event evt = Event.current;

        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            // Check if we're dragging valid objects from project
            bool hasValidObjects = DragAndDrop.objectReferences != null &&
                                   DragAndDrop.objectReferences.Length > 0;

            // Only process external drags (from project), not internal drags
            // Check both draggedItem and the target flags to ensure we're not in an internal drag
            bool isExternalDrag = draggedItem == null && draggedToGroup == null && !dragToUngrouped;

            if (hasValidObjects && isExternalDrag)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                // Always accept the drag on DragUpdated to show visual feedback
                if (evt.type == EventType.DragUpdated)
                {
                    evt.Use();
                }

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    List<string> alreadyFavorited = new List<string>();
                    int addedCount = 0;
                    int foldersProcessed = 0;

                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject != null)
                        {
                            string assetPath = AssetDatabase.GetAssetPath(draggedObject);

                            // Check if it's a folder
                            if (AssetDatabase.IsValidFolder(assetPath))
                            {
                                // Create a nested group hierarchy from the folder
                                string folderName = System.IO.Path.GetFileName(assetPath);

                                // Check if group with this name already exists
                                var existingGroup = favoritesData.groups.FirstOrDefault(g => g.name == folderName);

                                if (existingGroup == null)
                                {
                                    var newGroup = CreateGroupFromFolder(assetPath, 0);

                                    if (newGroup != null && (newGroup.items.Count > 0 || newGroup.childGroups.Count > 0))
                                    {
                                        favoritesData.groups.Add(newGroup);
                                        foldersProcessed++;

                                        // Count all assets recursively
                                        int totalAssets = CountAssetsInGroup(newGroup);
                                        addedCount += totalAssets;
                                    }
                                }
                                else
                                {
                                    alreadyFavorited.Add($"• Group '{folderName}' already exists");
                                }
                            }
                            else
                            {
                                // Regular asset
                                string location = FindItemLocation(draggedObject);

                                if (location != null)
                                {
                                    // Item already exists
                                    alreadyFavorited.Add($"• {draggedObject.name} (in {location})");
                                }
                                else
                                {
                                    // Add to ungrouped
                                    favoritesData.ungroupedItems.Add(new FavoriteItem
                                    {
                                        asset = draggedObject,
                                        note = ""
                                    });
                                    addedCount++;
                                }
                            }
                        }
                    }

                    // Show notification if there were duplicates
                    if (alreadyFavorited.Count > 0)
                    {
                        string message = "The following items are already favorited:\n\n";
                        message += string.Join("\n", alreadyFavorited);

                        if (addedCount > 0)
                        {
                            if (foldersProcessed > 0)
                            {
                                message += $"\n\n✓ Created {foldersProcessed} group(s) with {addedCount} asset(s).";
                            }
                            else
                            {
                                message += $"\n\n✓ Added {addedCount} new item(s) to favorites.";
                            }
                        }

                        EditorUtility.DisplayDialog(
                            "Duplicate Favorites",
                            message,
                            "OK");
                    }
                    else if (foldersProcessed > 0)
                    {
                        // Show success message for folders
                        ShowNotification(new GUIContent($"Created {foldersProcessed} group(s) from folder(s)"));
                    }

                    if (addedCount > 0)
                    {
                        EditorUtility.SetDirty(favoritesData);
                        AssetDatabase.SaveAssets();
                    }

                    Repaint();
                }

                evt.Use();
            }
        }

        // Clean up drag state when drag ends or is rejected
        if (evt.type == EventType.DragExited || evt.type == EventType.DragPerform)
        {
            draggedItem = null;
            draggedToGroup = null;
            dragToUngrouped = false;
        }
    }

    private FavoriteGroup CreateGroupFromFolder(string folderPath, int indentLevel = 0)
    {
        string folderName = System.IO.Path.GetFileName(folderPath);

        var group = new FavoriteGroup
        {
            name = folderName,
            color = GetRandomGroupColor(),
            isExpanded = true,
            items = new List<FavoriteItem>(),
            childGroups = new List<FavoriteGroup>(),
            indentLevel = indentLevel
        };

        // Get all items directly in this folder (not subfolders)
        string[] allPaths = AssetDatabase.FindAssets("", new[] { folderPath });

        foreach (string guid in allPaths)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // Check if this path is directly in the current folder (not in subfolders)
            string parentFolder = System.IO.Path.GetDirectoryName(assetPath).Replace("\\", "/");
            if (parentFolder != folderPath)
                continue;

            // If it's a folder, process it recursively as a child group
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                var childGroup = CreateGroupFromFolder(assetPath, indentLevel + 1);
                if (childGroup != null && (childGroup.items.Count > 0 || childGroup.childGroups.Count > 0))
                {
                    group.childGroups.Add(childGroup);
                }
            }
            else
            {
                // It's an asset, add it to this group
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (asset != null)
                {
                    group.items.Add(new FavoriteItem
                    {
                        asset = asset,
                        note = ""
                    });
                }
            }
        }

        return group;
    }

    private Color GetRandomGroupColor()
    {
        Color[] colors = new Color[]
        {
            new Color(0.3f, 0.5f, 0.8f, 0.3f), // Blue
            new Color(0.5f, 0.7f, 0.4f, 0.3f), // Green
            new Color(0.9f, 0.6f, 0.3f, 0.3f), // Orange
            new Color(0.7f, 0.4f, 0.7f, 0.3f), // Purple
            new Color(0.8f, 0.3f, 0.4f, 0.3f), // Red
            new Color(0.4f, 0.7f, 0.7f, 0.3f), // Cyan
            new Color(0.9f, 0.8f, 0.3f, 0.3f), // Yellow
        };

        return colors[UnityEngine.Random.Range(0, colors.Length)];
    }

    private int CountAssetsInGroup(FavoriteGroup group)
    {
        int count = group.items.Count;
        foreach (var childGroup in group.childGroups)
        {
            count += CountAssetsInGroup(childGroup);
        }
        return count;
    }

    private void DrawSearchResults()
    {
        EditorGUILayout.LabelField($"Search Results for \"{searchQuery}\"",
            EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        var results = new List<(FavoriteItem item, List<FavoriteItem> list,
            int index, FavoriteGroup group)>();

        // Search ungrouped
        for (int i = 0; i < favoritesData.ungroupedItems.Count; i++)
        {
            var item = favoritesData.ungroupedItems[i];
            if (item != null && item.asset != null &&
                item.asset.name.ToLower().Contains(searchQuery.ToLower()))
            {
                results.Add((item, favoritesData.ungroupedItems, i, null));
            }
        }

        // Search groups
        foreach (var group in favoritesData.groups)
        {
            for (int i = 0; i < group.items.Count; i++)
            {
                var item = group.items[i];
                if (item != null && item.asset != null &&
                    item.asset.name.ToLower().Contains(searchQuery.ToLower()))
                {
                    results.Add((item, group.items, i, group));
                }
            }
        }

        if (results.Count == 0)
        {
            EditorGUILayout.HelpBox("No results found", MessageType.Info);
        }
        else
        {
            foreach (var result in results)
            {
                DrawFavoriteItem(result.item, result.list, result.index,
                    result.group);
            }
        }
    }

    private void DrawEmptyState()
    {
        EditorGUILayout.Space(40);

        GUIStyle centeredStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16
        };

        EditorGUILayout.LabelField("⭐ Favorites", centeredStyle);
        EditorGUILayout.Space(10);

        GUIStyle dragHereStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("📁 Drag files here", dragHereStyle);
    }

    private void DrawFooter()
    {
        Rect footerRect = new Rect(0, position.height - 18,
            position.width, 18);

        GUILayout.BeginArea(footerRect);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        int totalItems = favoritesData.ungroupedItems.Count +
            favoritesData.groups.Sum(g => g.items.Count);
        EditorGUILayout.LabelField(
            $"📁 {favoritesData.groups.Count} groups | " +
            $"⭐ {totalItems} items ({favoritesData.ungroupedItems.Count} ungrouped)",
            EditorStyles.miniLabel);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Clear All", EditorStyles.toolbarButton))
        {
            if (EditorUtility.DisplayDialog("Clear All Favorites",
                "Remove all favorites and groups?", "Yes", "Cancel"))
            {
                favoritesData.ungroupedItems.Clear();
                foreach (var group in favoritesData.groups)
                {
                    group.items.Clear();
                }
                EditorUtility.SetDirty(favoritesData);
                AssetDatabase.SaveAssets();
            }
        }

        EditorGUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void ShowItemContextMenu(FavoriteItem item, List<FavoriteItem> items,
        int index, FavoriteGroup group)
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Select"), false, () =>
        {
            Selection.activeObject = item.asset;
        });

        menu.AddItem(new GUIContent("Ping"), false, () =>
        {
            EditorGUIUtility.PingObject(item.asset);
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Add/Edit Note..."), false, () =>
        {
            string assetPath = AssetDatabase.GetAssetPath(item.asset);
            NoteEditorWindow.ShowWindow(item, assetPath, assetNotesData, this);
        });

        string itemAssetPath = AssetDatabase.GetAssetPath(item.asset);
        if (assetNotesData != null && assetNotesData.HasNote(itemAssetPath))
        {
            menu.AddItem(new GUIContent("Clear Note"), false, () =>
            {
                assetNotesData.RemoveNote(itemAssetPath);
                EditorUtility.SetDirty(assetNotesData);
                AssetDatabase.SaveAssets();
                Repaint();
            });
        }

        menu.AddSeparator("");

        // Move to ungrouped
        if (group != null)
        {
            menu.AddItem(new GUIContent("Move to/Ungrouped"), false, () =>
            {
                items.RemoveAt(index);
                favoritesData.ungroupedItems.Add(item);
                EditorUtility.SetDirty(favoritesData);
                AssetDatabase.SaveAssets();
            });
        }

        // Move to group submenu
        foreach (var targetGroup in favoritesData.groups)
        {
            if (targetGroup != group)
            {
                menu.AddItem(new GUIContent($"Move to/{targetGroup.name}"),
                    false, () =>
                    {
                        items.RemoveAt(index);
                        targetGroup.items.Add(item);
                        EditorUtility.SetDirty(favoritesData);
                        AssetDatabase.SaveAssets();
                    });
            }
        }

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Duplicate Item"), false, () =>
        {
            var duplicate = new FavoriteItem
            {
                asset = item.asset
                // Note: Notes are now stored in shared AssetNotesData and will be automatically shared
            };
            items.Insert(index + 1, duplicate);
            EditorUtility.SetDirty(favoritesData);
            AssetDatabase.SaveAssets();
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Remove"), false, () =>
        {
            items.RemoveAt(index);
            EditorUtility.SetDirty(favoritesData);
            AssetDatabase.SaveAssets();
        });

        menu.ShowAsContext();
    }

    private void ShowGroupContextMenu(FavoriteGroup group, int groupIndex)
    {
        GenericMenu menu = new GenericMenu();

        menu.AddItem(new GUIContent("Rename"), false, () =>
        {
            GroupRenameWindow.ShowWindow(group, favoritesData);
        });

        menu.AddItem(new GUIContent("Change Color"), false, () =>
        {
            ColorPickerWindow.ShowWindow(group, favoritesData);
        });

        menu.AddItem(new GUIContent("Edit Note"), false, () =>
        {
            GroupNoteEditorWindow.ShowWindow(group, favoritesData);
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Sort by Name"), false, () =>
        {
            group.items = group.items
                .Where(i => i != null && i.asset != null)
                .OrderBy(i => i.asset.name)
                .ToList();
            EditorUtility.SetDirty(favoritesData);
            AssetDatabase.SaveAssets();
        });

        menu.AddItem(new GUIContent("Sort by Type"), false, () =>
        {
            group.items = group.items
                .Where(i => i != null && i.asset != null)
                .OrderBy(i => i.asset.GetType().Name)
                .ToList();
            EditorUtility.SetDirty(favoritesData);
            AssetDatabase.SaveAssets();
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Clear Items"), false, () =>
        {
            if (EditorUtility.DisplayDialog("Clear Group",
                $"Remove all items from '{group.name}'?", "Yes", "Cancel"))
            {
                group.items.Clear();
                EditorUtility.SetDirty(favoritesData);
                AssetDatabase.SaveAssets();
            }
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Delete Group"), false, () =>
        {
            // Count total items including nested groups
            int totalItems = CountAssetsInGroup(group);

            if (EditorUtility.DisplayDialog("Delete Group",
                $"Delete '{group.name}' and all {totalItems} item(s) (including nested groups)?",
                "Delete", "Cancel"))
            {
                // Remove group (all contents will be deleted)
                favoritesData.groups.RemoveAt(groupIndex);

                EditorUtility.SetDirty(favoritesData);
                AssetDatabase.SaveAssets();
            }
        });

        menu.ShowAsContext();
    }
}

// Helper window for adding groups
public class AddGroupWindow : EditorWindow
{
    private FavoritesData data;
    private string groupName = "";
    private Color groupColor = new Color(0.3f, 0.5f, 0.8f, 0.3f);

    public static void ShowWindow(FavoritesData data)
    {
        var window = GetWindow<AddGroupWindow>(true, "Add New Group", true);
        window.minSize = new Vector2(300, 120);
        window.maxSize = new Vector2(300, 120);
        window.data = data;
        window.groupColor = new Color(
            Random.Range(0.2f, 0.8f),
            Random.Range(0.2f, 0.8f),
            Random.Range(0.2f, 0.8f),
            0.3f);
        window.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Group Name:", EditorStyles.boldLabel);
        GUI.SetNextControlName("NameField");
        groupName = EditorGUILayout.TextField(groupName);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Group Color:", EditorStyles.boldLabel);
        groupColor = EditorGUILayout.ColorField(groupColor);

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
        {
            Close();
        }

        EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(groupName));
        if (GUILayout.Button("Create", GUILayout.Width(80)))
        {
            var newGroup = new FavoriteGroup
            {
                name = groupName.Trim(),
                color = groupColor,
                isExpanded = true,
                items = new List<FavoriteItem>()
            };

            data.groups.Add(newGroup);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Close();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return &&
            !string.IsNullOrWhiteSpace(groupName))
        {
            var newGroup = new FavoriteGroup
            {
                name = groupName.Trim(),
                color = groupColor,
                isExpanded = true,
                items = new List<FavoriteItem>()
            };

            data.groups.Add(newGroup);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Close();
            Event.current.Use();
        }

        EditorGUI.FocusTextInControl("NameField");
    }
}

// Helper window for renaming groups
public class GroupRenameWindow : EditorWindow
{
    private FavoriteGroup group;
    private FavoritesData data;
    private string newName;

    public static void ShowWindow(FavoriteGroup group, FavoritesData data)
    {
        var window = GetWindow<GroupRenameWindow>(true, "Rename Group", true);
        window.minSize = new Vector2(300, 80);
        window.maxSize = new Vector2(300, 80);
        window.group = group;
        window.data = data;
        window.newName = group.name;
        window.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Group Name:", EditorStyles.boldLabel);
        GUI.SetNextControlName("NameField");
        newName = EditorGUILayout.TextField(newName);

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
        {
            Close();
        }

        EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(newName));
        if (GUILayout.Button("Rename", GUILayout.Width(80)))
        {
            group.name = newName.Trim();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Close();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Return &&
            !string.IsNullOrWhiteSpace(newName))
        {
            group.name = newName.Trim();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Close();
            Event.current.Use();
        }

        EditorGUI.FocusTextInControl("NameField");
    }
}

// Helper window for color picking
public class ColorPickerWindow : EditorWindow
{
    private FavoriteGroup group;
    private FavoritesData data;
    private Color newColor;

    public static void ShowWindow(FavoriteGroup group, FavoritesData data)
    {
        var window = GetWindow<ColorPickerWindow>(true, "Change Color", true);
        window.minSize = new Vector2(300, 100);
        window.maxSize = new Vector2(300, 100);
        window.group = group;
        window.data = data;
        window.newColor = group.color;
        window.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Group Color:", EditorStyles.boldLabel);
        newColor = EditorGUILayout.ColorField(newColor);

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Cancel", GUILayout.Width(80)))
        {
            Close();
        }

        if (GUILayout.Button("Apply", GUILayout.Width(80)))
        {
            group.color = newColor;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}

// Helper window for adding notes to groups
public class GroupNoteEditorWindow : EditorWindow
{
    private FavoriteGroup group;
    private FavoritesData data;
    private string noteText;
    private Vector2 scrollPos;

    public static void ShowWindow(FavoriteGroup group, FavoritesData data)
    {
        var window = GetWindow<GroupNoteEditorWindow>(true, "Edit Group Note", true);
        window.minSize = new Vector2(400, 200);
        window.group = group;
        window.data = data;
        window.noteText = group.note ?? "";
        window.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"Note for group: {group.name}",
            EditorStyles.boldLabel);

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
            group.note = noteText;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }
}

// Helper window for adding notes
public class NoteEditorWindow : EditorWindow
{
    private FavoriteItem item;
    private string assetPath;
    private AssetNotesData notesData;
    private string noteText;
    private Vector2 scrollPos;
    private FavoritesWindow parentWindow;

    public static void ShowWindow(FavoriteItem item, string assetPath, AssetNotesData data, FavoritesWindow parent)
    {
        var window = GetWindow<NoteEditorWindow>(true, "Edit Note", true);
        window.minSize = new Vector2(400, 200);
        window.item = item;
        window.assetPath = assetPath;
        window.notesData = data;
        window.noteText = data != null ? data.GetNote(assetPath) : "";
        window.parentWindow = parent;
        window.ShowUtility();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"Note for: {item.asset.name}",
            EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"Path: {assetPath}", EditorStyles.miniLabel);

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
                notesData.SetNote(assetPath, noteText);
                EditorUtility.SetDirty(notesData);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Favorites] Saved note for asset: {item.asset.name}");

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
