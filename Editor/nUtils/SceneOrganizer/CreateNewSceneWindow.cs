using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SceneTemplate;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Makes a new scene: pick a template (the project's own scene templates, with their
/// thumbnails, or a basic or empty scene), name it, pick a folder (the ones that already
/// hold scenes are a click away), optionally put it in a group, and open it on its own or
/// alongside the scenes already open. The path it will write is shown as you type, with
/// anything wrong said right there. Enter creates, Esc cancels. Remembers the last choices.
/// </summary>
public class CreateNewSceneWindow : EditorWindow
{
    private const string PrefTemplate = "SceneOrganizer_NewSceneTemplate";
    private const string PrefAdditive = "SceneOrganizer_NewSceneAdditive";
    private const string PrefGroup = "SceneOrganizer_NewSceneGroup";
    private const string NoGroup = "None";

    /// <summary>One choice of starting point: a scene template asset, or one of Unity's basic setups.</summary>
    private sealed class Template
    {
        public string Id;
        public string Title;
        public string Description;
        public Texture Thumbnail;
        public SceneTemplateAsset Asset;
        public NewSceneSetup Setup;
    }

    private SceneOrganizerWindow organizerWindow;
    private readonly List<Template> templates = new List<Template>();
    private Template template;
    private readonly List<VisualElement> templateCards = new List<VisualElement>();

    private TextField nameField;
    private TextField folderField;
    private PopupField<string> groupField;
    private Toggle additiveToggle;
    private Label result;
    private Button createButton;

    public static void ShowWindow(SceneOrganizerWindow organizerWindow)
    {
        var window = GetWindow<CreateNewSceneWindow>(true, "Create Scene", true);
        window.organizerWindow = organizerWindow;
        window.minSize = new Vector2(520, 330);
        window.maxSize = new Vector2(760, 330);

        // Centred over the editor rather than in the screen's corner.
        Rect main = EditorGUIUtility.GetMainWindowPosition();
        window.position = new Rect(main.center.x - 280f, main.center.y - 165f, 560f, 330f);
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.AddToClassList("so-root");
        root.AddToClassList("so-dialog");
        var sheet = SceneOrganizerWindow.LoadStyleSheetNextTo(this);
        if (sheet != null)
            root.styleSheets.Add(sheet);

        FindTemplates();

        // ------------------------------------------------------------ templates
        root.Add(SectionTitle("Template"));
        var cards = new VisualElement();
        cards.AddToClassList("so-templates");
        foreach (var option in templates)
            cards.Add(TemplateCard(option));
        root.Add(cards);

        // ---------------------------------------------------------- name, folder
        string folder = StartingFolder();

        nameField = new TextField("Name") { value = UniqueName(folder, "New Scene") };
        nameField.AddToClassList("so-form-field");
        nameField.RegisterValueChangedCallback(_ => Validate());
        root.Add(nameField);

        var folderRow = new VisualElement();
        folderRow.AddToClassList("so-form-row");
        folderField = new TextField("Folder") { value = folder };
        folderField.AddToClassList("so-form-field");
        folderField.AddToClassList("so-form-row__grow");
        folderField.RegisterValueChangedCallback(_ => Validate());
        folderRow.Add(folderField);

        var recentFolders = new Button { text = "▾", tooltip = "Folders that already hold scenes" };
        recentFolders.AddToClassList("so-btn");
        recentFolders.AddToClassList("so-btn--icon");
        recentFolders.clicked += () => ShowFolderMenu(recentFolders.worldBound);
        folderRow.Add(recentFolders);

        var browse = new Button(BrowseFolder) { text = "Browse…" };
        browse.AddToClassList("so-btn");
        folderRow.Add(browse);
        root.Add(folderRow);

        // ------------------------------------------------------- group and open
        var data = AssetDatabase.LoadAssetAtPath<SceneGroupData>(SceneOrganizerConstants.ASSET_PATH);
        var groups = new List<string> { NoGroup };
        if (data != null)
            groups.AddRange(data.sceneGroups.Select(g => g.groupName));
        string lastGroup = EditorPrefs.GetString(PrefGroup, NoGroup);
        groupField = new PopupField<string>("Add to Group", groups, groups.Contains(lastGroup) ? lastGroup : NoGroup);
        groupField.AddToClassList("so-form-field");
        groupField.SetEnabled(groups.Count > 1);
        groupField.tooltip = groups.Count > 1 ? "Put the new scene straight into a group" : "Make a group in the Scene Organizer first";
        root.Add(groupField);

        additiveToggle = new Toggle("Keep Open Scenes")
        {
            value = EditorPrefs.GetBool(PrefAdditive, false),
            tooltip = "Open the new scene alongside the scenes already open, instead of in their place",
        };
        additiveToggle.AddToClassList("so-form-field");
        root.Add(additiveToggle);

        // --------------------------------------------------- result and buttons
        result = new Label();
        result.AddToClassList("so-result");
        root.Add(result);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        root.Add(spacer);

        var buttons = new VisualElement();
        buttons.AddToClassList("so-dialog__buttons");
        var cancel = new Button(Close) { text = "Cancel" };
        cancel.AddToClassList("so-dialog__button");
        buttons.Add(cancel);
        createButton = new Button(CreateScene) { text = "Create" };
        createButton.AddToClassList("so-dialog__button");
        createButton.AddToClassList("so-dialog__button--primary");
        buttons.Add(createButton);
        root.Add(buttons);

        // Enter creates and Esc cancels from anywhere in the window.
        root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                if (createButton.enabledSelf)
                    CreateScene();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                Close();
                evt.StopPropagation();
            }
        }, TrickleDown.TrickleDown);

        string lastTemplate = EditorPrefs.GetString(PrefTemplate, "");
        Select(templates.FirstOrDefault(t => t.Id == lastTemplate) ?? templates[0]);
        Validate();

        // Ready to type a name straight away.
        nameField.schedule.Execute(() => { nameField.Focus(); nameField.SelectAll(); }).StartingIn(0);
    }

    private static Label SectionTitle(string text)
    {
        var label = new Label(text);
        label.AddToClassList("so-section-title");
        return label;
    }

    // ------------------------------------------------------------------ templates

    /// <summary>The project's scene templates (pinned ones first), then Unity's basic and empty scenes.</summary>
    private void FindTemplates()
    {
        templates.Clear();
        var assets = new List<SceneTemplateAsset>();
        foreach (string guid in AssetDatabase.FindAssets("t:SceneTemplateAsset"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneTemplateAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.templateScene != null)
                assets.Add(asset);
        }

        foreach (var asset in assets.OrderByDescending(a => a.addToDefaults).ThenBy(a => a.templateName))
        {
            templates.Add(new Template
            {
                Id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset)),
                Title = string.IsNullOrEmpty(asset.templateName) ? asset.name : asset.templateName,
                Description = asset.description,
                Thumbnail = asset.preview,
                Asset = asset,
            });
        }

        templates.Add(new Template
        {
            Id = "basic",
            Title = "Basic",
            Description = "A Main Camera and a Directional Light.",
            Thumbnail = EditorGUIUtility.IconContent("SceneAsset Icon").image,
            Setup = NewSceneSetup.DefaultGameObjects,
        });
        templates.Add(new Template
        {
            Id = "empty",
            Title = "Empty",
            Description = "Nothing at all.",
            Thumbnail = EditorGUIUtility.IconContent("SceneAsset Icon").image,
            Setup = NewSceneSetup.EmptyScene,
        });
    }

    private VisualElement TemplateCard(Template option)
    {
        var card = new VisualElement { tooltip = option.Description, userData = option };
        card.AddToClassList("so-template");
        if (option.Asset == null)
            card.AddToClassList("so-template--builtin");

        var thumb = new Image { image = option.Thumbnail, scaleMode = option.Asset != null ? ScaleMode.ScaleAndCrop : ScaleMode.ScaleToFit };
        thumb.AddToClassList("so-template__thumb");
        card.Add(thumb);

        var title = new Label(option.Title);
        title.AddToClassList("so-template__title");
        card.Add(title);

        card.AddManipulator(new Clickable(() => Select(option)));
        // A double-click picks it and creates straight away.
        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.clickCount == 2 && createButton != null && createButton.enabledSelf)
            {
                Select(option);
                CreateScene();
            }
        });

        templateCards.Add(card);
        return card;
    }

    private void Select(Template option)
    {
        template = option;
        foreach (var card in templateCards)
            card.EnableInClassList("so-template--selected", card.userData == option);
    }

    // --------------------------------------------------------------------- folders

    /// <summary>
    /// The folder used last time, else the open scene's (where this project's own scenes
    /// live: the folder with the most scenes is often an imported pack's demos), else the
    /// one holding the most scenes, else Assets/Scenes.
    /// </summary>
    private static string StartingFolder()
    {
        string last = EditorPrefs.GetString(SceneOrganizerConstants.PREF_NEW_SCENE_FOLDER, "");
        if (!string.IsNullOrEmpty(last) && AssetDatabase.IsValidFolder(last))
            return last;

        string active = SceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(active) && active.StartsWith("Assets/", StringComparison.Ordinal))
            return Path.GetDirectoryName(active).Replace('\\', '/');

        var busiest = SceneFolders().FirstOrDefault();
        return busiest ?? "Assets/Scenes";
    }

    /// <summary>Folders inside Assets that hold scenes, those with the most first.</summary>
    private static IEnumerable<string> SceneFolders() =>
        SceneOrganizerScenes.All
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal))
            .Select(p => Path.GetDirectoryName(p).Replace('\\', '/'))
            .GroupBy(f => f)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key);

    private void ShowFolderMenu(Rect anchor)
    {
        var menu = new GenericMenu();
        foreach (string folder in SceneFolders().Take(12))
        {
            string captured = folder;
            menu.AddItem(new GUIContent(folder.Replace("/", "∕")), folder == folderField.value, () => folderField.value = captured);
        }

        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("No scenes in Assets yet"));
        menu.DropDown(anchor);
    }

    private void BrowseFolder()
    {
        string path = EditorUtility.OpenFolderPanel("Choose a Folder", folderField.value, "");
        if (string.IsNullOrEmpty(path))
            return;

        string assets = Application.dataPath.Replace('\\', '/');
        path = path.Replace('\\', '/');
        if (path == assets || path.StartsWith(assets + "/"))
            folderField.value = "Assets" + path.Substring(assets.Length);
        else
            ShowNotification(new GUIContent("Pick a folder inside Assets"), 2);
    }

    /// <summary><paramref name="baseName"/>, or "baseName 1", "baseName 2"... whichever isn't taken in <paramref name="folder"/>.</summary>
    private static string UniqueName(string folder, string baseName)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(folder.TrimEnd('/') + "/" + baseName + ".unity");
        return Path.GetFileNameWithoutExtension(path);
    }

    // ------------------------------------------------------------------ checking

    private string Folder => (folderField.value ?? "").Replace('\\', '/').Trim().TrimEnd('/');
    private string SceneName => (nameField.value ?? "").Trim();
    private string TargetPath => Folder + "/" + SceneName + ".unity";

    /// <summary>Shows the path that will be written, or what's in the way, and enables Create to match.</summary>
    private void Validate()
    {
        if (result == null || createButton == null)
            return;

        string problem = null;
        if (SceneName.Length == 0)
            problem = "Give the scene a name.";
        else if (SceneName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            problem = "A name can't contain / \\ : * ? \" < > |";
        else if (!(Folder == "Assets" || Folder.StartsWith("Assets/", StringComparison.Ordinal)))
            problem = "The folder has to be inside Assets.";

        bool exists = problem == null && File.Exists(TargetPath);
        result.EnableInClassList("so-result--error", problem != null);
        result.EnableInClassList("so-result--warn", exists);

        if (problem != null)
            result.text = problem;
        else if (exists)
            result.text = TargetPath + " already exists. Creating replaces it.";
        else
            result.text = TargetPath + (AssetDatabase.IsValidFolder(Folder) ? "" : "   (the folder will be made)");

        createButton.SetEnabled(problem == null);
        createButton.text = exists ? "Replace" : "Create";
    }

    // ------------------------------------------------------------------- creating

    private void CreateScene()
    {
        Validate();
        if (!createButton.enabledSelf || template == null)
            return;

        string path = TargetPath;
        bool additive = additiveToggle.value;

        // The new scene takes the place of the open ones: offer to save their changes first.
        if (!additive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder(Folder);

        // Replacing: the old file goes first, so a template can write its copy in its place.
        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);

        Scene scene;
        try
        {
            if (template.Asset != null)
            {
                // The template's own setup: its scene, cloned dependencies and any pipeline it runs.
                scene = SceneTemplateService.Instantiate(template.Asset, additive, path).scene;
            }
            else
            {
                scene = EditorSceneManager.NewScene(template.Setup, additive ? NewSceneMode.Additive : NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, path);
            }
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Couldn't Create the Scene", e.Message, "OK");
            return;
        }

        if (!scene.IsValid())
        {
            ShowNotification(new GUIContent("The scene wasn't created"), 2);
            return;
        }

        string group = groupField.value;
        if (group != NoGroup)
            AddToGroup(group, path);

        EditorPrefs.SetString(SceneOrganizerConstants.PREF_NEW_SCENE_FOLDER, Folder);
        EditorPrefs.SetString(PrefTemplate, template.Id);
        EditorPrefs.SetBool(PrefAdditive, additive);
        EditorPrefs.SetString(PrefGroup, group);

        SceneOrganizerUserState.instance.AddRecent(path);
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
        Close();
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private void AddToGroup(string groupName, string path)
    {
        var data = AssetDatabase.LoadAssetAtPath<SceneGroupData>(SceneOrganizerConstants.ASSET_PATH);
        var group = data != null ? data.sceneGroups.Find(g => g.groupName == groupName) : null;
        if (group == null || group.scenes.Contains(path))
            return;

        group.scenes.Add(path);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
        organizerWindow?.LoadGroups();
    }
}
