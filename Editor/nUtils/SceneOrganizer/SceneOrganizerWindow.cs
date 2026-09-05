using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class SceneOrganizerWindow : EditorWindow
{
    // ------------------------------------------------------------------ model

    /// <summary>
    /// Scene paths with their display strings precomputed, so no row has to derive them
    /// while drawing.
    /// </summary>
    private struct SceneEntry
    {
        public string Path;
        public string Name;
        public string Directory;
        public string LowerName;
        public string LowerPath;
    }

    private SceneGroupData sceneGroupData;
    private AssetNotesData assetNotesData;

    private readonly List<SceneEntry> allScenes = new List<SceneEntry>();
    private readonly Dictionary<string, int> sceneIndexByPath = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<string> knownScenePaths = new HashSet<string>(StringComparer.Ordinal);

    // Indices into allScenes. Recomputed only when the query or the scene list changes.
    private readonly List<int> filteredScenes = new List<int>();
    private readonly List<int> recentScenes = new List<int>();
    private string filterCacheKey;

    // Settings
    private string searchQuery = "";
    private bool showRecentScenes = true;
    private bool showScenePath = false;
    private bool enableBackup;
    private string backupDirectory;
    private float leftPaneWidth = 300f;
    private SceneSortMode sortMode = SceneSortMode.Name;

    private enum SceneSortMode { Name, Path, RecentlyUsed }

    private const float RowHeight = 22f;
    private const float DragThreshold = 6f;

    // ------------------------------------------------------------------- view

    private TwoPaneSplitView split;
    private VisualElement leftPane;
    private VisualElement rightPane;
    private ListView sceneList;
    private ListView recentList;
    private Foldout recentFoldout;
    private Label sceneCountLabel;
    private VisualElement sceneEmpty;
    private ScrollView groupsScroll;
    private VisualElement groupsEmpty;
    private ToolbarMenu sortMenu;

    private IVisualElementScheduledItem pendingSave;
    private const long SaveDebounceMs = 700;

    private Vector2 dragStart;
    private bool dragCandidate;
    private string renamingGroup;

    private static GUIContent sceneIconContent;
    private const string DragKey = "nUtils.SceneOrganizer.Scenes";

    [MenuItem("Window/nUtilities/Scene Organizer")]
    public static void ShowWindow()
    {
        var window = GetWindow<SceneOrganizerWindow>("Scene Organizer");
        window.minSize = new Vector2(560, 320);
    }

    private void OnEnable()
    {
        // Groups and settings first: LoadScenes sorts by them and prunes against them.
        LoadGroups();
        LoadSettings();
        LoadAssetNotes();
        LoadScenes();

        EditorApplication.projectChanged += OnProjectChanged;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        pendingSave?.Pause();

        if (leftPane != null && leftPane.resolvedStyle.width > 50f)
            leftPaneWidth = leftPane.resolvedStyle.width;

        SaveSettings();
        SaveGroups();
    }

    private void OnProjectChanged()
    {
        // Saving our own asset raises this too. Rebuilding the group cards here would
        // destroy the list you are dragging, so only react when scenes really changed.
        if (!SceneSetChanged())
            return;

        LoadScenes();
        RefreshAll();
    }

    private bool SceneSetChanged()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene");

        if (guids.Length != knownScenePaths.Count)
            return true;

        foreach (string guid in guids)
        {
            if (!knownScenePaths.Contains(AssetDatabase.GUIDToAssetPath(guid)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Marks the asset dirty now and schedules the actual write. AssetDatabase.SaveAssets
    /// puts up Unity's save/import progress dialog, so calling it on every reorder step
    /// flashed a popup on each drag.
    /// </summary>
    private void MarkGroupsDirty()
    {
        if (sceneGroupData == null)
            return;

        EditorUtility.SetDirty(sceneGroupData);

        if (rootVisualElement == null)
        {
            SaveGroups();
            return;
        }

        if (pendingSave == null)
            pendingSave = rootVisualElement.schedule.Execute(SaveGroups);

        pendingSave.ExecuteLater(SaveDebounceMs);
    }

    // ------------------------------------------------------------- UI assembly

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.AddToClassList("so-root");

        var sheet = LoadStyleSheet();
        if (sheet != null)
            root.styleSheets.Add(sheet);

        root.Add(BuildToolbar());

        split = new TwoPaneSplitView(0, leftPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
        root.Add(split);

        leftPane = BuildScenePane();
        rightPane = BuildGroupPane();
        split.Add(leftPane);
        split.Add(rightPane);

        root.RegisterCallback<KeyDownEvent>(OnKeyDown);

        RefreshAll();
    }

    private StyleSheet LoadStyleSheet()
    {
        // Resolved next to this script so the package can be moved or renamed freely.
        var script = MonoScript.FromScriptableObject(this);
        string scriptPath = script != null ? AssetDatabase.GetAssetPath(script) : null;

        if (!string.IsNullOrEmpty(scriptPath))
        {
            string dir = Path.GetDirectoryName(scriptPath);
            if (!string.IsNullOrEmpty(dir))
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                    dir.Replace('\\', '/') + "/SceneOrganizerStyles.uss");

                if (sheet != null)
                    return sheet;
            }
        }

        Debug.LogWarning("[Scene Organizer] SceneOrganizerStyles.uss not found next to SceneOrganizerWindow.cs; the window will render unstyled.");
        return null;
    }

    private Toolbar BuildToolbar()
    {
        var toolbar = new Toolbar();

        var create = new ToolbarButton(() => CreateNewSceneWindow.ShowWindow(this)) { text = "Create Scene" };
        toolbar.Add(create);

        var refresh = new ToolbarButton(() => { LoadScenes(); RefreshAll(); }) { text = "Refresh" };
        toolbar.Add(refresh);

        var search = new ToolbarSearchField();
        search.AddToClassList("so-search");
        search.tooltip = "Filter scenes by name or path";
        search.RegisterValueChangedCallback(evt =>
        {
            searchQuery = evt.newValue ?? "";
            InvalidateFilter();
            RefreshSceneList();
        });
        toolbar.Add(search);

        var spacer = new VisualElement();
        spacer.AddToClassList("so-toolbar-spacer");
        toolbar.Add(spacer);

        sortMenu = new ToolbarMenu { text = "Sort: " + sortMode };
        foreach (SceneSortMode mode in Enum.GetValues(typeof(SceneSortMode)))
        {
            var captured = mode;
            sortMenu.menu.AppendAction(
                ObjectNames.NicifyVariableName(mode.ToString()),
                _ => SetSortMode(captured),
                _ => sortMode == captured ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }
        toolbar.Add(sortMenu);

        var pathToggle = new ToolbarToggle { text = "Path", value = showScenePath, tooltip = "Show each scene's folder" };
        pathToggle.RegisterValueChangedCallback(evt =>
        {
            showScenePath = evt.newValue;
            SaveSettings();
            RefreshSceneList();
        });
        toolbar.Add(pathToggle);

        var options = new ToolbarMenu { text = "⋮" };
        options.menu.AppendAction("Show Recent Scenes",
            _ => { showRecentScenes = !showRecentScenes; SaveSettings(); RefreshSceneList(); },
            _ => showRecentScenes ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        options.menu.AppendSeparator();
        options.menu.AppendAction("Settings…", _ => SettingsWindow.ShowWindow(this));
        toolbar.Add(options);

        return toolbar;
    }

    private VisualElement BuildScenePane()
    {
        var pane = new VisualElement();
        pane.AddToClassList("so-pane");

        var header = new VisualElement();
        header.AddToClassList("so-pane__header");

        var title = new Label("Scenes");
        title.AddToClassList("so-pane__title");
        header.Add(title);

        sceneCountLabel = new Label();
        sceneCountLabel.AddToClassList("so-pane__count");
        header.Add(sceneCountLabel);

        pane.Add(header);

        var body = new VisualElement();
        body.AddToClassList("so-pane__body");

        recentFoldout = new Foldout { text = "Recent", value = true };
        recentFoldout.AddToClassList("so-recent");
        recentList = MakeSceneListView(recentScenes, true);
        recentFoldout.Add(recentList);
        body.Add(recentFoldout);

        sceneList = MakeSceneListView(filteredScenes, false);
        sceneList.style.flexGrow = 1;
        body.Add(sceneList);

        sceneEmpty = BuildEmptyState("No scenes match", "Try a different search term.", null, null);
        body.Add(sceneEmpty);

        pane.Add(body);
        return pane;
    }

    private ListView MakeSceneListView(List<int> source, bool isRecent)
    {
        var list = new ListView
        {
            itemsSource = source,
            fixedItemHeight = RowHeight,
            selectionType = isRecent ? SelectionType.Single : SelectionType.Multiple,
            virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            showBorder = false,
            makeItem = () => MakeSceneRow(isRecent),
            reorderable = false,
        };

        list.bindItem = (element, index) => BindSceneRow(element, source, index, isRecent);

        // Double-click (or Enter on the list) opens.
        list.itemsChosen += chosen =>
        {
            foreach (var item in chosen)
            {
                if (item is int sceneIndex && sceneIndex >= 0 && sceneIndex < allScenes.Count)
                {
                    OpenScene(allScenes[sceneIndex].Path);
                    break;
                }
            }
        };

        return list;
    }

    private VisualElement MakeSceneRow(bool isRecent)
    {
        var row = new VisualElement();
        row.AddToClassList("so-row");
        if (isRecent)
            row.AddToClassList("so-row--recent");

        var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
        icon.AddToClassList("so-row__icon");
        row.Add(icon);

        var name = new Label { name = "name" };
        name.AddToClassList("so-row__name");
        row.Add(name);

        var note = new Label { name = "note", text = "\U0001F4DD" };
        note.AddToClassList("so-row__note");
        row.Add(note);

        var spacer = new VisualElement();
        spacer.AddToClassList("so-row__spacer");
        row.Add(spacer);

        var path = new Label { name = "path" };
        path.AddToClassList("so-row__path");
        row.Add(path);

        // Drag out to a group. A plain click must not start one, hence the threshold.
        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            dragStart = evt.position;
            dragCandidate = true;
        });

        row.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragCandidate || (evt.pressedButtons & 1) == 0) return;
            if (Vector2.Distance(evt.position, dragStart) < DragThreshold) return;

            dragCandidate = false;
            StartSceneDrag();
        });

        row.RegisterCallback<PointerUpEvent>(_ => dragCandidate = false);

        row.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            if (!(row.userData is int sceneIndex) || sceneIndex < 0 || sceneIndex >= allScenes.Count)
                return;

            BuildSceneContextMenu(evt.menu, allScenes[sceneIndex].Path);
        }));

        return row;
    }

    private void BindSceneRow(VisualElement element, List<int> source, int index, bool isRecent)
    {
        if (index < 0 || index >= source.Count)
            return;

        int sceneIndex = source[index];
        if (sceneIndex < 0 || sceneIndex >= allScenes.Count)
            return;

        SceneEntry entry = allScenes[sceneIndex];
        element.userData = sceneIndex;

        var icon = element.Q<Image>("icon");
        if (sceneIconContent != null)
            icon.image = sceneIconContent.image;

        element.Q<Label>("name").text = entry.Name;

        var note = element.Q<Label>("note");
        bool hasNote = assetNotesData != null && assetNotesData.HasNote(entry.Path);
        note.style.display = hasNote ? DisplayStyle.Flex : DisplayStyle.None;
        note.tooltip = hasNote ? assetNotesData.GetNote(entry.Path) : null;

        var path = element.Q<Label>("path");
        path.style.display = showScenePath ? DisplayStyle.Flex : DisplayStyle.None;
        path.text = showScenePath ? entry.Directory : null;

        element.tooltip = entry.Path;
    }

    private VisualElement BuildGroupPane()
    {
        var pane = new VisualElement();
        pane.AddToClassList("so-pane");

        var header = new VisualElement();
        header.AddToClassList("so-pane__header");

        var title = new Label("Groups");
        title.AddToClassList("so-pane__title");
        header.Add(title);

        var count = new Label();
        count.AddToClassList("so-pane__count");
        header.Add(count);

        var newGroup = new Button(PromptNewGroup) { text = "New Group" };
        newGroup.AddToClassList("so-btn");
        header.Add(newGroup);

        pane.Add(header);

        var body = new VisualElement();
        body.AddToClassList("so-pane__body");

        groupsScroll = new ScrollView(ScrollViewMode.Vertical);
        groupsScroll.AddToClassList("so-groups");
        body.Add(groupsScroll);

        groupsEmpty = BuildEmptyState(
            "No groups yet",
            "Group scenes you open together, then load the whole set in one click.",
            "Create First Group",
            PromptNewGroup);
        body.Add(groupsEmpty);

        pane.Add(body);
        return pane;
    }

    private static VisualElement BuildEmptyState(string title, string hint, string actionText, Action action)
    {
        var empty = new VisualElement();
        empty.AddToClassList("so-empty");

        var titleLabel = new Label(title);
        titleLabel.AddToClassList("so-empty__title");
        empty.Add(titleLabel);

        var hintLabel = new Label(hint);
        hintLabel.AddToClassList("so-empty__hint");
        empty.Add(hintLabel);

        if (!string.IsNullOrEmpty(actionText) && action != null)
        {
            var button = new Button(action) { text = actionText };
            button.AddToClassList("so-empty__action");
            empty.Add(button);
        }

        return empty;
    }

    // -------------------------------------------------------------- refreshing

    private void RefreshAll()
    {
        RefreshSceneList();
        RefreshGroups();
    }

    private void RefreshSceneList()
    {
        if (sceneList == null)
            return;

        EnsureSceneIcon();
        RebuildFilter();
        RebuildRecents();

        bool hasRecents = showRecentScenes && recentScenes.Count > 0;
        recentFoldout.style.display = hasRecents ? DisplayStyle.Flex : DisplayStyle.None;
        if (hasRecents)
        {
            recentList.style.height = recentScenes.Count * RowHeight + 4f;
            recentList.RefreshItems();
        }

        bool any = filteredScenes.Count > 0;
        sceneList.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
        sceneEmpty.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;

        sceneCountLabel.text = string.IsNullOrEmpty(searchQuery)
            ? allScenes.Count.ToString()
            : filteredScenes.Count + " of " + allScenes.Count;

        sceneList.RefreshItems();
    }

    private void RefreshGroups()
    {
        if (groupsScroll == null)
            return;

        groupsScroll.Clear();

        bool any = sceneGroupData != null && sceneGroupData.sceneGroups.Count > 0;
        groupsScroll.style.display = any ? DisplayStyle.Flex : DisplayStyle.None;
        groupsEmpty.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;

        if (!any)
            return;

        for (int i = 0; i < sceneGroupData.sceneGroups.Count; i++)
            groupsScroll.Add(BuildGroupCard(sceneGroupData.sceneGroups[i], i));
    }

    private VisualElement BuildGroupCard(SceneGroupData.SceneGroup group, int groupIndex)
    {
        var card = new VisualElement();
        card.AddToClassList("so-group");

        var stripe = new VisualElement();
        stripe.AddToClassList("so-group__stripe");
        stripe.style.backgroundColor = group.groupColor;
        card.Add(stripe);

        var main = new VisualElement();
        main.AddToClassList("so-group__main");
        card.Add(main);

        var header = new VisualElement();
        header.AddToClassList("so-group__header");
        main.Add(header);

        // Assigned further down; the foldout callback closes over it.
        VisualElement body = null;

        var foldout = new Foldout { value = !group.isCollapsed };
        foldout.AddToClassList("so-group__foldout");
        foldout.RegisterValueChangedCallback(evt =>
        {
            if (evt.target != foldout)
                return;

            group.isCollapsed = !evt.newValue;
            EditorUtility.SetDirty(sceneGroupData);

            // The body is a sibling of the header, not a child of the foldout, so its
            // visibility has to be driven explicitly - otherwise the arrow turns and
            // nothing collapses.
            if (body != null)
                body.style.display = group.isCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
        });
        header.Add(foldout);

        var swatch = new VisualElement { tooltip = "Change group colour" };
        swatch.AddToClassList("so-swatch");
        swatch.style.backgroundColor = group.groupColor;
        swatch.AddManipulator(new Clickable(() => ColorPickerWindow.ShowWindow(group, this)));
        header.Add(swatch);

        if (group.groupName == renamingGroup)
        {
            var field = new TextField { value = group.groupName };
            field.AddToClassList("so-group__rename");
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    RenameGroup(group.groupName, field.value);
                    renamingGroup = null;
                    RefreshGroups();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    renamingGroup = null;
                    RefreshGroups();
                    evt.StopPropagation();
                }
            });
            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (renamingGroup == null) return;
                RenameGroup(group.groupName, field.value);
                renamingGroup = null;
                RefreshGroups();
            });
            header.Add(field);
            field.schedule.Execute(() => { field.Focus(); field.SelectAll(); }).StartingIn(0);
        }
        else
        {
            var name = new Label(group.groupName) { tooltip = "Click to collapse or expand" };
            name.AddToClassList("so-group__name");
            name.AddManipulator(new Clickable(() => foldout.value = !foldout.value));
            header.Add(name);

            var count = new Label(group.scenes.Count.ToString());
            count.AddToClassList("so-group__count");
            header.Add(count);
        }

        var spacer = new VisualElement();
        spacer.AddToClassList("so-group__spacer");
        header.Add(spacer);

        var addSelected = new Button(() => AddSelectedScenesToGroup(group))
        {
            text = "+",
            tooltip = "Add the scenes selected on the left to this group",
        };
        addSelected.AddToClassList("so-btn");
        addSelected.AddToClassList("so-btn--icon");
        header.Add(addSelected);

        if (group.scenes.Count > 0)
        {
            var loadAll = new Button(() => LoadAllScenesInGroup(group))
            {
                text = "Load All",
                tooltip = "Open every scene in this group together (first single, rest additive)",
            };
            loadAll.AddToClassList("so-btn");
            header.Add(loadAll);
        }

        var menu = new Button { text = "⋮", tooltip = "Group actions" };
        menu.AddToClassList("so-btn");
        menu.AddToClassList("so-btn--icon");
        menu.clicked += () => ShowGroupMenu(group, groupIndex, menu.worldBound);
        header.Add(menu);

        body = new VisualElement();
        body.AddToClassList("so-group__body");

        if (group.scenes.Count == 0)
        {
            var empty = new Label("Drop scenes here, or select some and press +");
            empty.AddToClassList("so-group__empty");
            body.Add(empty);
        }
        else
        {
            body.Add(BuildGroupSceneList(group));
        }

        body.style.display = group.isCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
        main.Add(body);

        RegisterGroupDropTarget(card, group);
        return card;
    }

    /// <summary>
    /// Bound straight to the group's own list, so dragging a row to reorder rewrites the
    /// saved order. Scene order matters here: it decides which scene loads first.
    /// </summary>
    private ListView BuildGroupSceneList(SceneGroupData.SceneGroup group)
    {
        var list = new ListView
        {
            itemsSource = group.scenes,
            fixedItemHeight = RowHeight,
            selectionType = SelectionType.None,
            virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            showBorder = false,
            reorderable = true,
            reorderMode = ListViewReorderMode.Animated,
        };

        list.AddToClassList("so-group__scenes");
        list.style.height = group.scenes.Count * RowHeight + 2f;

        list.makeItem = () =>
        {
            var row = new VisualElement();
            row.AddToClassList("so-row");

            var icon = new Image { name = "icon", scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList("so-row__icon");
            row.Add(icon);

            var name = new Label { name = "name" };
            name.AddToClassList("so-row__name");
            row.Add(name);

            var spacer = new VisualElement();
            spacer.AddToClassList("so-row__spacer");
            row.Add(spacer);

            var open = new Button { name = "open", text = "Open", tooltip = "Open this scene (closes the others)" };
            open.AddToClassList("so-btn");
            row.Add(open);

            var remove = new Button { name = "remove", text = "×", tooltip = "Remove from group" };
            remove.AddToClassList("so-btn");
            remove.AddToClassList("so-btn--icon");
            row.Add(remove);

            return row;
        };

        list.bindItem = (element, index) =>
        {
            if (index < 0 || index >= group.scenes.Count)
                return;

            string path = group.scenes[index];
            element.tooltip = path;

            if (sceneIconContent != null)
                element.Q<Image>("icon").image = sceneIconContent.image;

            element.Q<Label>("name").text = Path.GetFileNameWithoutExtension(path);

            var open = element.Q<Button>("open");
            open.clickable = new Clickable(() => OpenScene(path));

            var remove = element.Q<Button>("remove");
            remove.clickable = new Clickable(() =>
            {
                group.scenes.Remove(path);
                MarkGroupsDirty();
                RefreshGroups();
            });
        };

        list.itemIndexChanged += (_, __) => MarkGroupsDirty();

        ReorderAnimationSpeed.Attach(list);

        return list;
    }

    /// <summary>
    /// Unity animates the reorder gap with a ValueAnimation whose duration is hardcoded
    /// to 500 ms inside ListViewDraggerAnimated.Animate, with nothing public to tune. The
    /// animation object itself is reachable though, so this shortens it while a drag is
    /// running. Every lookup is reflective and guarded: if a future Unity moves these
    /// internals, reordering simply keeps Unity's own timing instead of breaking.
    /// </summary>
    private static class ReorderAnimationSpeed
    {
        private const int TargetDurationMs = 120;
        private const long TickIntervalMs = 16;

        private static readonly System.Reflection.PropertyInfo ActiveItems;
        private static readonly System.Reflection.PropertyInfo Animator;
        private static readonly System.Reflection.PropertyInfo DurationMs;
        private static readonly bool Available;

        static ReorderAnimationSpeed()
        {
            try
            {
                const System.Reflection.BindingFlags flags =
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;

                var assembly = typeof(ListView).Assembly;

                ActiveItems = assembly
                    .GetType("UnityEngine.UIElements.BaseVerticalCollectionView")
                    ?.GetProperty("activeItems", flags);

                Animator = assembly
                    .GetType("UnityEngine.UIElements.ReusableCollectionItem")
                    ?.GetProperty("animator", flags);

                DurationMs = Animator?.PropertyType.GetProperty("durationMs");

                Available = ActiveItems != null && Animator != null && DurationMs != null && DurationMs.CanWrite;
            }
            catch
            {
                Available = false;
            }
        }

        public static void Attach(ListView list)
        {
            if (!Available)
                return;

            IVisualElementScheduledItem ticker = null;

            // Only runs between pointer down and pointer up on this list, so there is no
            // idle cost when nobody is dragging.
            list.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (ticker == null)
                    ticker = list.schedule.Execute(() => Retune(list)).Every(TickIntervalMs);
                else
                    ticker.Resume();
            });

            list.RegisterCallback<PointerUpEvent>(_ => ticker?.Pause());
            list.RegisterCallback<PointerCaptureOutEvent>(_ => ticker?.Pause());
            list.RegisterCallback<DetachFromPanelEvent>(_ => ticker?.Pause());
        }

        private static void Retune(ListView list)
        {
            try
            {
                if (!(ActiveItems.GetValue(list) is System.Collections.IEnumerable items))
                    return;

                foreach (var item in items)
                {
                    object animation = Animator.GetValue(item);
                    if (animation == null)
                        continue;

                    // Animations are pooled and reset to Unity's duration on reuse, so
                    // this has to keep re-applying rather than run once.
                    if ((int)DurationMs.GetValue(animation) > TargetDurationMs)
                        DurationMs.SetValue(animation, TargetDurationMs);
                }
            }
            catch
            {
                // Internals moved; leave Unity's timing alone.
            }
        }
    }

    // -------------------------------------------------------------- drag & drop

    private void StartSceneDrag()
    {
        var paths = GetSelectedScenePaths();
        if (paths.Count == 0)
            return;

        var objects = new UnityEngine.Object[paths.Count];
        for (int i = 0; i < paths.Count; i++)
            objects[i] = AssetDatabase.LoadAssetAtPath<SceneAsset>(paths[i]);

        DragAndDrop.PrepareStartDrag();
        DragAndDrop.paths = paths.ToArray();
        DragAndDrop.objectReferences = objects;
        DragAndDrop.SetGenericData(DragKey, paths);
        DragAndDrop.StartDrag(paths.Count == 1
            ? Path.GetFileNameWithoutExtension(paths[0])
            : paths.Count + " scenes");
    }

    private void RegisterGroupDropTarget(VisualElement card, SceneGroupData.SceneGroup group)
    {
        card.RegisterCallback<DragUpdatedEvent>(evt =>
        {
            if (GetDraggedScenePaths().Count == 0)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            card.AddToClassList("so-group--drop");
            evt.StopPropagation();
        });

        card.RegisterCallback<DragLeaveEvent>(_ => card.RemoveFromClassList("so-group--drop"));
        card.RegisterCallback<DragExitedEvent>(_ => card.RemoveFromClassList("so-group--drop"));

        card.RegisterCallback<DragPerformEvent>(evt =>
        {
            var paths = GetDraggedScenePaths();
            card.RemoveFromClassList("so-group--drop");

            if (paths.Count == 0)
                return;

            DragAndDrop.AcceptDrag();
            AddScenesToGroup(group, paths);
            evt.StopPropagation();
        });
    }

    private static List<string> GetDraggedScenePaths()
    {
        var result = new List<string>();

        if (DragAndDrop.GetGenericData(DragKey) is List<string> carried)
        {
            result.AddRange(carried);
            return result;
        }

        // Also accept scenes dragged in from the Project window.
        var paths = DragAndDrop.paths;
        if (paths != null)
        {
            foreach (var path in paths)
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    result.Add(path);
        }

        return result;
    }

    private List<string> GetSelectedScenePaths()
    {
        var paths = new List<string>();

        if (sceneList == null)
            return paths;

        foreach (var item in sceneList.selectedItems)
        {
            if (item is int sceneIndex && sceneIndex >= 0 && sceneIndex < allScenes.Count)
                paths.Add(allScenes[sceneIndex].Path);
        }

        return paths;
    }

    private void AddSelectedScenesToGroup(SceneGroupData.SceneGroup group)
    {
        var paths = GetSelectedScenePaths();

        if (paths.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing Selected",
                "Select one or more scenes in the left pane first.", "OK");
            return;
        }

        AddScenesToGroup(group, paths);
    }

    private void AddScenesToGroup(SceneGroupData.SceneGroup group, List<string> paths)
    {
        int added = 0;

        foreach (var path in paths)
        {
            if (group.scenes.Contains(path))
                continue;

            group.scenes.Add(path);
            added++;
        }

        if (added == 0)
            return;

        MarkGroupsDirty();
        RefreshGroups();
    }

    // ------------------------------------------------------------------- menus

    private void BuildSceneContextMenu(DropdownMenu menu, string scenePath)
    {
        menu.AppendAction("Open", _ => OpenScene(scenePath));
        menu.AppendAction("Open Additive", _ => EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive));
        menu.AppendSeparator();

        var selected = GetSelectedScenePaths();
        if (sceneGroupData != null && sceneGroupData.sceneGroups.Count > 0)
        {
            string label = selected.Count > 1 ? $"Add {selected.Count} Selected to Group/" : "Add to Group/";
            foreach (var group in sceneGroupData.sceneGroups)
            {
                var captured = group;
                menu.AppendAction(label + group.groupName,
                    _ => AddScenesToGroup(captured, selected.Count > 1 ? selected : new List<string> { scenePath }));
            }
            menu.AppendSeparator();
        }

        menu.AppendAction("Edit Note…", _ => SceneNoteEditorWindow.ShowWindow(scenePath, assetNotesData, this));
        menu.AppendAction("Rename…", _ => PromptRenameScene(scenePath));
        menu.AppendSeparator();
        menu.AppendAction("Show in Project", _ =>
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)));
        menu.AppendAction("Copy Path", _ => EditorGUIUtility.systemCopyBuffer = scenePath);
    }

    private void ShowGroupMenu(SceneGroupData.SceneGroup group, int groupIndex, Rect anchor)
    {
        var menu = new GenericMenu();

        if (group.scenes.Count > 0)
        {
            menu.AddItem(new GUIContent("Load All Scenes"), false, () => LoadAllScenesInGroup(group));
            menu.AddSeparator("");
        }

        menu.AddItem(new GUIContent("Rename"), false, () =>
        {
            renamingGroup = group.groupName;
            RefreshGroups();
        });
        menu.AddItem(new GUIContent("Change Colour…"), false, () => ColorPickerWindow.ShowWindow(group, this));
        menu.AddSeparator("");

        if (groupIndex > 0)
            menu.AddItem(new GUIContent("Move Up"), false, () => MoveGroup(groupIndex, -1));
        else
            menu.AddDisabledItem(new GUIContent("Move Up"));

        if (groupIndex < sceneGroupData.sceneGroups.Count - 1)
            menu.AddItem(new GUIContent("Move Down"), false, () => MoveGroup(groupIndex, 1));
        else
            menu.AddDisabledItem(new GUIContent("Move Down"));

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Delete Group"), false, () =>
        {
            if (EditorUtility.DisplayDialog("Delete Group",
                $"Are you sure you want to delete '{group.groupName}'?", "Delete", "Cancel"))
            {
                RemoveGroup(group);
            }
        });

        menu.DropDown(anchor);
    }

    private void MoveGroup(int index, int direction)
    {
        int target = index + direction;
        if (target < 0 || target >= sceneGroupData.sceneGroups.Count)
            return;

        var group = sceneGroupData.sceneGroups[index];
        sceneGroupData.sceneGroups.RemoveAt(index);
        sceneGroupData.sceneGroups.Insert(target, group);

        MarkGroupsDirty();
        RefreshGroups();
    }

    private void PromptNewGroup()
    {
        TextPromptWindow.Show("New Group", "Group name", "", name =>
        {
            CreateNewGroup(name);
            RefreshGroups();
        });
    }

    private void PromptRenameScene(string scenePath)
    {
        TextPromptWindow.Show("Rename Scene", "Scene name",
            Path.GetFileNameWithoutExtension(scenePath), newName =>
            {
                RenameScene(scenePath, newName);
                RefreshAll();
            });
    }

    // --------------------------------------------------------------- keyboard

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (sceneGroupData == null)
            return;

        switch (evt.keyCode)
        {
            case KeyCode.F2:
                var selected = GetSelectedScenePaths();
                if (selected.Count == 1)
                {
                    PromptRenameScene(selected[0]);
                    evt.StopPropagation();
                }
                break;

            case KeyCode.Escape:
                sceneList?.ClearSelection();
                evt.StopPropagation();
                break;
        }
    }

    // ------------------------------------------------------------ scene loading

    public void LoadScenes()
    {
        allScenes.Clear();
        knownScenePaths.Clear();

        string[] sceneGUIDs = AssetDatabase.FindAssets("t:Scene");
        foreach (string guid in sceneGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            string name = Path.GetFileNameWithoutExtension(path);
            allScenes.Add(new SceneEntry
            {
                Path = path,
                Name = name,
                Directory = Path.GetDirectoryName(path),
                LowerName = name.ToLowerInvariant(),
                LowerPath = path.ToLowerInvariant(),
            });
            knownScenePaths.Add(path);
        }

        SortScenes();
        PruneMissingScenes();
        InvalidateFilter();
    }

    /// <summary>
    /// Drops references to scenes that no longer exist, at load time rather than as a
    /// File.Exists per row per repaint.
    /// </summary>
    private void PruneMissingScenes()
    {
        if (sceneGroupData == null)
            return;

        int removed = sceneGroupData.recentScenes.RemoveAll(s => !knownScenePaths.Contains(s));

        foreach (var group in sceneGroupData.sceneGroups)
            removed += group.scenes.RemoveAll(s => !knownScenePaths.Contains(s));

        if (removed > 0)
            EditorUtility.SetDirty(sceneGroupData);
    }

    private void SetSortMode(SceneSortMode mode)
    {
        sortMode = mode;
        if (sortMenu != null)
            sortMenu.text = "Sort: " + mode;

        SortScenes();
        SaveSettings();
        RefreshSceneList();
    }

    private void SortScenes()
    {
        switch (sortMode)
        {
            case SceneSortMode.Name:
                allScenes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                break;

            case SceneSortMode.Path:
                allScenes.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
                break;

            case SceneSortMode.RecentlyUsed:
                // recentScenes[0] is the most recent; anything never opened sorts last.
                allScenes.Sort((a, b) =>
                {
                    int ra = RecentRank(a.Path);
                    int rb = RecentRank(b.Path);
                    return ra != rb
                        ? ra.CompareTo(rb)
                        : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });
                break;
        }

        sceneIndexByPath.Clear();
        for (int i = 0; i < allScenes.Count; i++)
            sceneIndexByPath[allScenes[i].Path] = i;

        InvalidateFilter();
    }

    private int RecentRank(string path)
    {
        if (sceneGroupData == null)
            return int.MaxValue;

        int index = sceneGroupData.recentScenes.IndexOf(path);
        return index < 0 ? int.MaxValue : index;
    }

    private void InvalidateFilter()
    {
        filterCacheKey = null;
    }

    private void RebuildFilter()
    {
        string key = searchQuery ?? string.Empty;
        if (filterCacheKey == key)
            return;

        filterCacheKey = key;
        filteredScenes.Clear();

        if (string.IsNullOrEmpty(searchQuery))
        {
            for (int i = 0; i < allScenes.Count; i++)
                filteredScenes.Add(i);

            return;
        }

        string query = searchQuery.ToLowerInvariant();

        for (int i = 0; i < allScenes.Count; i++)
        {
            SceneEntry entry = allScenes[i];

            if (entry.LowerName.IndexOf(query, StringComparison.Ordinal) >= 0 ||
                entry.LowerPath.IndexOf(query, StringComparison.Ordinal) >= 0)
                filteredScenes.Add(i);
        }
    }

    private void RebuildRecents()
    {
        recentScenes.Clear();

        if (sceneGroupData == null)
            return;

        for (int i = 0; i < sceneGroupData.recentScenes.Count && recentScenes.Count < 5; i++)
        {
            if (sceneIndexByPath.TryGetValue(sceneGroupData.recentScenes[i], out int index))
                recentScenes.Add(index);
        }
    }

    private static void EnsureSceneIcon()
    {
        if (sceneIconContent == null || sceneIconContent.image == null)
            sceneIconContent = EditorGUIUtility.IconContent("SceneAsset Icon");
    }

    // ------------------------------------------------------------ data operations

    private void CreateNewGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            return;

        if (sceneGroupData.sceneGroups.Exists(g => g.groupName == groupName))
        {
            EditorUtility.DisplayDialog("Duplicate Name", "A group with this name already exists.", "OK");
            return;
        }

        sceneGroupData.sceneGroups.Add(new SceneGroupData.SceneGroup
        {
            groupName = groupName,
            groupColor = GetRandomPastelColor(),
        });

        MarkGroupsDirty();
    }

    private static Color GetRandomPastelColor()
    {
        Color[] colors =
        {
            new Color(0.3f, 0.6f, 1f),
            new Color(1f, 0.4f, 0.4f),
            new Color(0.4f, 0.9f, 0.4f),
            new Color(1f, 0.8f, 0.2f),
            new Color(0.9f, 0.4f, 0.9f),
            new Color(0.4f, 0.9f, 0.9f),
        };

        return colors[UnityEngine.Random.Range(0, colors.Length)];
    }

    private void RenameGroup(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || oldName == newName)
            return;

        var group = sceneGroupData.sceneGroups.Find(g => g.groupName == oldName);
        if (group == null)
            return;

        group.groupName = newName;
        MarkGroupsDirty();
    }

    private void RenameScene(string oldScenePath, string newSceneName)
    {
        if (string.IsNullOrWhiteSpace(newSceneName))
            return;

        string error = AssetDatabase.RenameAsset(oldScenePath, newSceneName);

        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Rename Failed", error, "OK");
            return;
        }

        AssetDatabase.SaveAssets();
        LoadScenes();
    }

    private void RemoveGroup(SceneGroupData.SceneGroup group)
    {
        sceneGroupData.sceneGroups.Remove(group);
        MarkGroupsDirty();
        RefreshGroups();
    }

    private void OpenScene(string scenePath)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EditorSceneManager.OpenScene(scenePath);
        sceneGroupData.AddToRecent(scenePath);
        MarkGroupsDirty();

        if (sortMode == SceneSortMode.RecentlyUsed)
            SortScenes();

        RefreshSceneList();
    }

    private void LoadAllScenesInGroup(SceneGroupData.SceneGroup group)
    {
        if (group.scenes.Count == 0)
        {
            EditorUtility.DisplayDialog("No Scenes", "This group has no scenes to load.", "OK");
            return;
        }

        if (!File.Exists(group.scenes[0]))
        {
            EditorUtility.DisplayDialog("Scene Not Found",
                $"The first scene in group '{group.groupName}' could not be found:\n{group.scenes[0]}\n\nPlease refresh the scene list.",
                "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        try
        {
            EditorSceneManager.OpenScene(group.scenes[0]);
            int loadedCount = 1;

            for (int i = 1; i < group.scenes.Count; i++)
            {
                if (File.Exists(group.scenes[i]))
                {
                    EditorSceneManager.OpenScene(group.scenes[i], OpenSceneMode.Additive);
                    loadedCount++;
                }
                else
                {
                    Debug.LogWarning($"[Scene Organizer] Scene not found (skipped): {group.scenes[i]}");
                }
            }

            if (loadedCount < group.scenes.Count)
            {
                EditorUtility.DisplayDialog("Scenes Loaded with Warnings",
                    $"Loaded {loadedCount} out of {group.scenes.Count} scenes.\n\nSome scenes could not be found. Check the Console for details.",
                    "OK");
            }
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Error Loading Scenes",
                $"An error occurred while loading scenes:\n{e.Message}", "OK");
            Debug.LogError($"Error loading scenes from group '{group.groupName}': {e}");
        }
    }

    // ------------------------------------------------------------- settings/IO

    private void LoadSettings()
    {
        enableBackup = EditorPrefs.GetBool(SceneOrganizerConstants.PREF_ENABLE_BACKUP, false);
        backupDirectory = EditorPrefs.GetString(SceneOrganizerConstants.PREF_BACKUP_DIR, Application.dataPath);
        showRecentScenes = EditorPrefs.GetBool(SceneOrganizerConstants.PREF_SHOW_RECENT, true);
        showScenePath = EditorPrefs.GetBool("SceneOrganizer_ShowPath", false);
        sortMode = (SceneSortMode)EditorPrefs.GetInt("SceneOrganizer_SortMode", 0);
        leftPaneWidth = EditorPrefs.GetFloat("SceneOrganizer_LeftPaneWidth", 300f);
    }

    private void SaveSettings()
    {
        EditorPrefs.SetBool(SceneOrganizerConstants.PREF_SHOW_RECENT, showRecentScenes);
        EditorPrefs.SetBool("SceneOrganizer_ShowPath", showScenePath);
        EditorPrefs.SetInt("SceneOrganizer_SortMode", (int)sortMode);
        EditorPrefs.SetFloat("SceneOrganizer_LeftPaneWidth", leftPaneWidth);
    }

    private void LoadAssetNotes()
    {
        const string assetPath = "Assets/Editor/AssetNotesData.asset";
        assetNotesData = AssetDatabase.LoadAssetAtPath<AssetNotesData>(assetPath);

        if (assetNotesData != null)
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");

        assetNotesData = CreateInstance<AssetNotesData>();
        AssetDatabase.CreateAsset(assetNotesData, assetPath);
        AssetDatabase.SaveAssets();
    }

    public void LoadGroups()
    {
        try
        {
            EnsureAssetFolder();
            sceneGroupData = AssetDatabase.LoadAssetAtPath<SceneGroupData>(SceneOrganizerConstants.ASSET_PATH);

            if (sceneGroupData == null)
                CreateNewSceneGroupData();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load groups: {ex.Message}");
            CreateNewSceneGroupData();
        }

        RefreshGroups();
    }

    private static void EnsureAssetFolder()
    {
        string directory = Path.GetDirectoryName(SceneOrganizerConstants.ASSET_PATH);

        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            AssetDatabase.CreateFolder("Assets", "Editor");
    }

    private void CreateNewSceneGroupData()
    {
        try
        {
            EnsureAssetFolder();
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
        pendingSave?.Pause();

        if (sceneGroupData == null)
            return;

        EditorUtility.SetDirty(sceneGroupData);
        AssetDatabase.SaveAssets();

        if (enableBackup && !string.IsNullOrEmpty(backupDirectory))
            BackupGroups();
    }

    private void BackupGroups()
    {
        try
        {
            if (!Directory.Exists(backupDirectory))
                Directory.CreateDirectory(backupDirectory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = Path.Combine(backupDirectory, $"SceneGroupData_Backup_{timestamp}.asset");
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
            if (!Directory.Exists(backupDirectory))
                return;

            string[] backupFiles = Directory.GetFiles(backupDirectory, "SceneGroupData_Backup_*.asset");

            if (backupFiles.Length <= SceneOrganizerConstants.MAX_BACKUP_COPIES)
                return;

            Array.Sort(backupFiles);

            for (int i = 0; i < backupFiles.Length - SceneOrganizerConstants.MAX_BACKUP_COPIES; i++)
                File.Delete(backupFiles[i]);
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

    // ------------------------------------------------------------ helper windows

    /// <summary>Small modal used for naming a new group and renaming scenes.</summary>
    public class TextPromptWindow : EditorWindow
    {
        private string value;
        private string label;
        private Action<string> onAccept;

        public static void Show(string title, string label, string initial, Action<string> onAccept)
        {
            var window = CreateInstance<TextPromptWindow>();
            window.titleContent = new GUIContent(title);
            window.label = label;
            window.value = initial;
            window.onAccept = onAccept;
            window.minSize = new Vector2(320, 96);
            window.maxSize = new Vector2(320, 96);
            window.ShowModalUtility();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;

            var field = new TextField(label) { value = value };
            field.RegisterValueChangedCallback(evt => value = evt.newValue);
            root.Add(field);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 10;

            var cancel = new Button(Close) { text = "Cancel" };
            cancel.style.minWidth = 70;
            buttons.Add(cancel);

            var accept = new Button(Accept) { text = "OK" };
            accept.style.minWidth = 70;
            buttons.Add(accept);

            root.Add(buttons);

            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Accept();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.StopPropagation();
                }
            });

            field.schedule.Execute(() => { field.Focus(); field.SelectAll(); }).StartingIn(0);
        }

        private void Accept()
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var callback = onAccept;
            string result = value;
            Close();
            callback?.Invoke(result);
        }
    }

    public class ColorPickerWindow : EditorWindow
    {
        private SceneGroupData.SceneGroup group;
        private SceneOrganizerWindow parentWindow;
        private Color selected;

        private static readonly Color[] presetColors =
        {
            new Color(0.3f, 0.6f, 1f), new Color(1f, 0.4f, 0.4f), new Color(0.4f, 0.9f, 0.4f),
            new Color(1f, 0.8f, 0.2f), new Color(0.9f, 0.4f, 0.9f), new Color(0.4f, 0.9f, 0.9f),
            new Color(1f, 0.6f, 0.2f), new Color(0.7f, 0.5f, 1f), new Color(1f, 0.5f, 0.7f),
            new Color(0.5f, 0.8f, 0.5f), new Color(0.6f, 0.6f, 0.6f), new Color(0.5f, 0.7f, 0.9f),
        };

        public static void ShowWindow(SceneGroupData.SceneGroup group, SceneOrganizerWindow parent)
        {
            var window = CreateInstance<ColorPickerWindow>();
            window.titleContent = new GUIContent("Group Colour");
            window.group = group;
            window.parentWindow = parent;
            window.selected = group.groupColor;
            window.minSize = new Vector2(240, 260);
            window.maxSize = new Vector2(240, 260);
            window.ShowUtility();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var sheet = parentWindow != null ? parentWindow.LoadStyleSheet() : null;
            if (sheet != null)
                root.styleSheets.Add(sheet);

            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            var grid = new VisualElement();
            grid.AddToClassList("so-swatch-grid");

            var swatches = new List<VisualElement>();

            foreach (var color in presetColors)
            {
                var captured = color;
                var swatch = new VisualElement();
                swatch.AddToClassList("so-swatch-grid__item");
                swatch.style.backgroundColor = captured;

                if (Approximately(captured, selected))
                    swatch.AddToClassList("so-swatch-grid__item--selected");

                swatch.AddManipulator(new Clickable(() =>
                {
                    selected = captured;
                    foreach (var other in swatches)
                        other.EnableInClassList("so-swatch-grid__item--selected", other == swatch);
                }));

                swatches.Add(swatch);
                grid.Add(swatch);
            }

            root.Add(grid);

            var custom = new UnityEditor.UIElements.ColorField("Custom") { value = selected, showAlpha = false };
            custom.style.marginTop = 8;
            custom.RegisterValueChangedCallback(evt =>
            {
                selected = evt.newValue;
                foreach (var other in swatches)
                    other.RemoveFromClassList("so-swatch-grid__item--selected");
            });
            root.Add(custom);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 10;

            var cancel = new Button(Close) { text = "Cancel" };
            cancel.style.minWidth = 70;
            buttons.Add(cancel);

            var apply = new Button(() =>
            {
                group.groupColor = selected;

                if (parentWindow != null)
                {
                    parentWindow.MarkGroupsDirty();
                    parentWindow.RefreshGroups();
                }

                Close();
            })
            { text = "Apply" };
            apply.style.minWidth = 70;
            buttons.Add(apply);

            root.Add(buttons);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b);
        }
    }

    public class SceneNoteEditorWindow : EditorWindow
    {
        private string scenePath;
        private AssetNotesData notesData;
        private SceneOrganizerWindow parentWindow;
        private string noteText;

        public static void ShowWindow(string scenePath, AssetNotesData data, SceneOrganizerWindow parent)
        {
            var window = CreateInstance<SceneNoteEditorWindow>();
            window.titleContent = new GUIContent("Scene Note");
            window.scenePath = scenePath;
            window.notesData = data;
            window.parentWindow = parent;
            window.noteText = data != null ? data.GetNote(scenePath) : "";
            window.minSize = new Vector2(400, 220);
            window.ShowUtility();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            var title = new Label(Path.GetFileNameWithoutExtension(scenePath));
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            var path = new Label(scenePath);
            path.style.fontSize = 10;
            path.style.opacity = 0.6f;
            path.style.marginBottom = 6;
            root.Add(path);

            var field = new TextField { multiline = true, value = noteText };
            field.style.flexGrow = 1;
            field.style.whiteSpace = WhiteSpace.Normal;
            field.RegisterValueChangedCallback(evt => noteText = evt.newValue);
            root.Add(field);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 8;

            var clear = new Button(() => { noteText = ""; field.value = ""; }) { text = "Clear" };
            clear.style.minWidth = 70;
            buttons.Add(clear);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            buttons.Add(spacer);

            var cancel = new Button(Close) { text = "Cancel" };
            cancel.style.minWidth = 70;
            buttons.Add(cancel);

            var save = new Button(() =>
            {
                if (notesData != null)
                {
                    notesData.SetNote(scenePath, noteText);
                    EditorUtility.SetDirty(notesData);
                    AssetDatabase.SaveAssets();
                    parentWindow?.RefreshSceneList();
                }

                Close();
            })
            { text = "Save" };
            save.style.minWidth = 70;
            buttons.Add(save);

            root.Add(buttons);
        }
    }
}
