using UnityEditor;
using UnityEngine;
using System;
using System.IO;

public class SettingsWindow : EditorWindow
{
    private SceneOrganizerWindow organizerWindow;
    private bool enableBackup;
    private string backupDirectory;
    private string[] backupFiles;
    private Vector2 scrollPosition;
    private int selectedBackupIndex = -1;

    public static void ShowWindow(SceneOrganizerWindow window)
    {
        SettingsWindow settingsWindow = GetWindow<SettingsWindow>("Settings", true);
        settingsWindow.organizerWindow = window;
        settingsWindow.minSize = new Vector2(400, 300);
        settingsWindow.maxSize = new Vector2(400, 600);
    }

    private void OnEnable()
    {
        enableBackup = EditorPrefs.GetBool(SceneOrganizerConstants.PREF_ENABLE_BACKUP, false);
        backupDirectory = EditorPrefs.GetString(SceneOrganizerConstants.PREF_BACKUP_DIR, Application.dataPath);
        LoadBackupFiles();
    }

    private void OnGUI()
    {
        GUILayout.Label("Scene Organizer Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        DrawBackupSettings();
        EditorGUILayout.Space();
        DrawBackupList();

        GUILayout.FlexibleSpace();
        DrawButtons();
    }

    private void DrawBackupSettings()
    {
        EditorGUILayout.LabelField("Backup Settings", EditorStyles.boldLabel);

        enableBackup = EditorGUILayout.Toggle("Enable Automatic Backup", enableBackup);

        if (enableBackup)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Backup Directory:");
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Backup Directory", backupDirectory, "");
                if (!string.IsNullOrEmpty(path))
                {
                    backupDirectory = path;
                    LoadBackupFiles();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(backupDirectory, MessageType.None);
            EditorGUILayout.HelpBox(
                $"Backups are created automatically when groups are modified. " +
                $"Maximum {SceneOrganizerConstants.MAX_BACKUP_COPIES} backups are kept.",
                MessageType.Info);
        }
    }

    private void DrawBackupList()
    {
        EditorGUILayout.LabelField("Available Backups", EditorStyles.boldLabel);

        if (backupFiles == null || backupFiles.Length == 0)
        {
            EditorGUILayout.HelpBox("No backups available.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

        for (int i = backupFiles.Length - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal("box");

            bool wasSelected = selectedBackupIndex == i;
            bool isSelected = GUILayout.Toggle(wasSelected, "", GUILayout.Width(20));

            if (isSelected && !wasSelected)
            {
                selectedBackupIndex = i;
            }

            string fileName = Path.GetFileName(backupFiles[i]);
            GUILayout.Label(fileName, GUILayout.ExpandWidth(true));

            FileInfo fileInfo = new FileInfo(backupFiles[i]);
            GUILayout.Label(fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                EditorStyles.miniLabel, GUILayout.Width(120));

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        if (selectedBackupIndex >= 0 && selectedBackupIndex < backupFiles.Length)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Restore Selected Backup", GUILayout.Width(180)))
            {
                RestoreFromBackup(backupFiles[selectedBackupIndex]);
            }

            if (GUILayout.Button("Delete", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Delete Backup",
                    "Are you sure you want to delete this backup?", "Yes", "No"))
                {
                    File.Delete(backupFiles[selectedBackupIndex]);
                    LoadBackupFiles();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create Backup Now"))
        {
            CreateManualBackup();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Save", GUILayout.Width(100)))
        {
            SaveSettings();
            Close();
        }

        if (GUILayout.Button("Cancel", GUILayout.Width(100)))
        {
            Close();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void LoadBackupFiles()
    {
        try
        {
            if (string.IsNullOrEmpty(backupDirectory) || !Directory.Exists(backupDirectory))
            {
                backupFiles = new string[0];
                selectedBackupIndex = -1;
                return;
            }

            backupFiles = Directory.GetFiles(backupDirectory, "SceneGroupData_Backup_*.asset");
            Array.Sort(backupFiles);

            if (selectedBackupIndex >= backupFiles.Length)
            {
                selectedBackupIndex = backupFiles.Length - 1;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading backup files: {ex.Message}");
            backupFiles = new string[0];
            selectedBackupIndex = -1;
        }
    }

    private void CreateManualBackup()
    {
        string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            SceneOrganizerConstants.ASSET_PATH));

        if (!File.Exists(sourcePath))
        {
            EditorUtility.DisplayDialog("Error", "No SceneGroupData found to backup.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(backupDirectory))
        {
            backupDirectory = Application.dataPath;
        }

        if (!Directory.Exists(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupFileName = $"SceneGroupData_Backup_{timestamp}.asset";
        string backupPath = Path.Combine(backupDirectory, backupFileName);

        try
        {
            File.Copy(sourcePath, backupPath, true);
            LoadBackupFiles();
            EditorUtility.DisplayDialog("Success", "Backup created successfully!", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to create backup: {ex.Message}", "OK");
        }
    }

    private void SaveSettings()
    {
        EditorPrefs.SetBool(SceneOrganizerConstants.PREF_ENABLE_BACKUP, enableBackup);
        EditorPrefs.SetString(SceneOrganizerConstants.PREF_BACKUP_DIR, backupDirectory);

        if (organizerWindow != null)
        {
            organizerWindow.UpdateBackupSettings(enableBackup, backupDirectory);
        }
    }

    private void RestoreFromBackup(string backupPath)
    {
        if (!EditorUtility.DisplayDialog("Restore Backup",
            "This will overwrite your current scene groups. Are you sure?", "Yes", "No"))
        {
            return;
        }

        try
        {
            string targetPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                SceneOrganizerConstants.ASSET_PATH));

            File.Copy(backupPath, targetPath, true);
            AssetDatabase.Refresh();

            if (organizerWindow != null)
            {
                organizerWindow.LoadGroups();
            }

            EditorUtility.DisplayDialog("Success", "Backup restored successfully!", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to restore backup: {ex.Message}", "OK");
        }
    }
}