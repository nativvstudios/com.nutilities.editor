using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using UnityEngine.SceneManagement;

public class CreateNewSceneWindow : EditorWindow
{
    private string sceneName = "NewScene";
    private int selectedTemplateIndex = 0;
    private string savePath = "Assets/Scenes";
    private SceneOrganizerWindow organizerWindow;
    private bool addToGroup;
    private string selectedGroup = "";

    private readonly string[] sceneTemplates = { "Default (with Main Camera & Light)", "Empty Scene" };

    public static void ShowWindow(SceneOrganizerWindow organizerWindow)
    {
        CreateNewSceneWindow window = GetWindow<CreateNewSceneWindow>("Create New Scene");
        window.organizerWindow = organizerWindow;
        window.minSize = new Vector2(450, 250);
        window.maxSize = new Vector2(450, 250);
    }

    private void OnGUI()
    {
        GUILayout.Label("Create New Scene", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Scene Name
        EditorGUILayout.LabelField("Scene Name:");
        sceneName = EditorGUILayout.TextField(sceneName);
        EditorGUILayout.Space();

        // Template Selection
        EditorGUILayout.LabelField("Template:");
        selectedTemplateIndex = EditorGUILayout.Popup(selectedTemplateIndex, sceneTemplates);
        EditorGUILayout.Space();

        // Save Path
        EditorGUILayout.LabelField("Save Path:");
        EditorGUILayout.BeginHorizontal();
        savePath = EditorGUILayout.TextField(savePath);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string path = EditorUtility.SaveFolderPanel("Select Folder", savePath, "");
            if (!string.IsNullOrEmpty(path))
            {
                // Convert to relative path
                if (path.StartsWith(Application.dataPath))
                {
                    savePath = "Assets" + path.Substring(Application.dataPath.Length);
                }
                else
                {
                    EditorUtility.DisplayDialog("Invalid Path",
                        "Please select a folder inside the Assets directory.", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // Add to Group Option
        addToGroup = EditorGUILayout.Toggle("Add to Group", addToGroup);

        if (addToGroup && organizerWindow != null)
        {
            var sceneGroupData = AssetDatabase.LoadAssetAtPath<SceneGroupData>(
                SceneOrganizerConstants.ASSET_PATH);

            if (sceneGroupData != null && sceneGroupData.sceneGroups.Count > 0)
            {
                string[] groupNames = sceneGroupData.sceneGroups
                    .ConvertAll(g => g.groupName).ToArray();

                int selectedIndex = System.Array.IndexOf(groupNames, selectedGroup);
                if (selectedIndex < 0) selectedIndex = 0;

                selectedIndex = EditorGUILayout.Popup("Group:", selectedIndex, groupNames);
                selectedGroup = groupNames[selectedIndex];
            }
            else
            {
                EditorGUILayout.HelpBox("No groups available. Create a group first.", MessageType.Warning);
                addToGroup = false;
            }
        }

        EditorGUILayout.Space();
        GUILayout.FlexibleSpace();

        // Buttons
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = !string.IsNullOrWhiteSpace(sceneName);
        if (GUILayout.Button("Create Scene", GUILayout.Height(30)))
        {
            CreateScene();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Cancel", GUILayout.Height(30)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void CreateScene()
    {
        // Ensure save path directory exists
        if (!AssetDatabase.IsValidFolder(savePath))
        {
            // Try to create the folder structure
            string[] folders = savePath.Split('/');
            string currentPath = folders[0]; // Should be "Assets"

            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = newPath;
            }
        }

        string fullPath = Path.Combine(savePath, sceneName + ".unity");

        // Check if file exists
        if (File.Exists(fullPath))
        {
            if (!EditorUtility.DisplayDialog("Overwrite Scene",
                "A scene with this name already exists. Do you want to overwrite it?", "Yes", "No"))
            {
                return;
            }
        }

        // Create the scene
        NewSceneSetup setup = selectedTemplateIndex == 0
            ? NewSceneSetup.DefaultGameObjects
            : NewSceneSetup.EmptyScene;

        Scene newScene = EditorSceneManager.NewScene(setup, NewSceneMode.Single);
        EditorSceneManager.SaveScene(newScene, fullPath);
        AssetDatabase.Refresh();

        // Add to group if requested
        if (addToGroup && !string.IsNullOrEmpty(selectedGroup))
        {
            var sceneGroupData = AssetDatabase.LoadAssetAtPath<SceneGroupData>(
                SceneOrganizerConstants.ASSET_PATH);

            if (sceneGroupData != null)
            {
                var group = sceneGroupData.sceneGroups.Find(g => g.groupName == selectedGroup);
                if (group != null && !group.scenes.Contains(fullPath))
                {
                    group.scenes.Add(fullPath);
                    EditorUtility.SetDirty(sceneGroupData);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        // Refresh organizer window
        organizerWindow?.LoadScenes();

        Close();
    }
}