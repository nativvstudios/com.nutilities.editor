using UnityEditor;
using UnityEngine;
using System;
using System.IO;

public class SettingsWindow : EditorWindow
{
    private SceneOrganizerWindow organizerWindow;
    private bool enableBackup;
    private string backupDirectory;
    private string[] backupFiles = new string[0];
    // Shown beside each backup; read once per listing rather than on every repaint.
    private string[] backupDates = new string[0];
    private Vector2 scrollPosition;
    private int selectedBackupIndex = -1;

    public static void ShowWindow(SceneOrganizerWindow window)
    {
        SettingsWindow settingsWindow = GetWindow<SettingsWindow>(true, "Scene Organizer Settings", true);
        settingsWindow.organizerWindow = window;
        settingsWindow.minSize = new Vector2(420, 320);
        settingsWindow.maxSize = new Vector2(600, 640);
    }

    private void OnEnable()
    {
        enableBackup = EditorPrefs.GetBool(SceneOrganizerConstants.PREF_ENABLE_BACKUP, false);
        backupDirectory = EditorPrefs.GetString(SceneOrganizerConstants.PREF_BACKUP_DIR, SceneOrganizerConstants.DefaultBackupDirectory);
        LoadBackupFiles();
    }

    private void OnGUI()
    {
        GUILayout.Label("Backups", EditorStyles.boldLabel);

        DrawBackupSettings();
        EditorGUILayout.Space();
        DrawBackupList();

        GUILayout.FlexibleSpace();
        DrawButtons();
    }

    private void DrawBackupSettings()
    {
        enableBackup = EditorGUILayout.ToggleLeft(
            new GUIContent("Back up groups automatically", "Keep a copy of your scene groups each time they change."),
            enableBackup);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Folder", GUILayout.Width(50));
        EditorGUILayout.SelectableLabel(backupDirectory, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        if (GUILayout.Button("Browse…", GUILayout.Width(70)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Backup Folder", backupDirectory, "");
            if (!string.IsNullOrEmpty(path))
            {
                backupDirectory = path;
                LoadBackupFiles();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (SceneOrganizerConstants.IsInsideAssets(backupDirectory))
        {
            EditorGUILayout.HelpBox(
                "This folder is inside Assets, so Unity imports every backup as a new asset, which slows the editor. A folder outside Assets is better.",
                MessageType.Warning);
            if (GUILayout.Button("Use the project's UserSettings folder"))
            {
                backupDirectory = SceneOrganizerConstants.DefaultBackupDirectory;
                LoadBackupFiles();
            }
        }
        else if (enableBackup)
        {
            EditorGUILayout.HelpBox(
                $"A copy is kept each time your groups change; the newest {SceneOrganizerConstants.MAX_BACKUP_COPIES} are kept.",
                MessageType.None);
        }
    }

    private void DrawBackupList()
    {
        EditorGUILayout.LabelField("Saved Backups", EditorStyles.boldLabel);

        if (backupFiles.Length == 0)
        {
            EditorGUILayout.HelpBox("No backups in this folder yet.", MessageType.None);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

        // Newest first; a click anywhere on a row picks it.
        for (int i = backupFiles.Length - 1; i >= 0; i--)
        {
            bool selected = selectedBackupIndex == i;
            var rect = EditorGUILayout.BeginHorizontal(selected ? "SelectionRect" : "box");
            GUILayout.Label(Path.GetFileName(backupFiles[i]), GUILayout.ExpandWidth(true));
            GUILayout.Label(backupDates[i], EditorStyles.miniLabel, GUILayout.Width(110));
            EditorGUILayout.EndHorizontal();

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                selectedBackupIndex = i;
                Event.current.Use();
            }
        }

        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(selectedBackupIndex < 0 || selectedBackupIndex >= backupFiles.Length))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Restore Selected", GUILayout.Width(130)))
                RestoreFromBackup(backupFiles[selectedBackupIndex]);

            if (GUILayout.Button("Delete", GUILayout.Width(70)))
            {
                if (EditorUtility.DisplayDialog("Delete Backup",
                    $"Delete {Path.GetFileName(backupFiles[selectedBackupIndex])}?", "Delete", "Cancel"))
                {
                    File.Delete(backupFiles[selectedBackupIndex]);
                    selectedBackupIndex = -1;
                    LoadBackupFiles();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Back Up Now", GUILayout.Width(110)))
        {
            CreateManualBackup();
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Cancel", GUILayout.Width(90)))
        {
            Close();
        }

        if (GUILayout.Button("Save", GUILayout.Width(90)))
        {
            SaveSettings();
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
                backupDates = new string[0];
                selectedBackupIndex = -1;
                return;
            }

            backupFiles = Directory.GetFiles(backupDirectory, "SceneGroupData_Backup_*.asset");
            Array.Sort(backupFiles);
            backupDates = new string[backupFiles.Length];
            for (int i = 0; i < backupFiles.Length; i++)
                backupDates[i] = File.GetLastWriteTime(backupFiles[i]).ToString("yyyy-MM-dd HH:mm");

            if (selectedBackupIndex >= backupFiles.Length)
            {
                selectedBackupIndex = backupFiles.Length - 1;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading backup files: {ex.Message}");
            backupFiles = new string[0];
            backupDates = new string[0];
            selectedBackupIndex = -1;
        }
    }

    private void CreateManualBackup()
    {
        string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            SceneOrganizerConstants.ASSET_PATH));

        if (!File.Exists(sourcePath))
        {
            EditorUtility.DisplayDialog("Nothing to Back Up", "There are no scene groups saved yet.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(backupDirectory))
        {
            backupDirectory = SceneOrganizerConstants.DefaultBackupDirectory;
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
            ShowNotification(new GUIContent("Backed up"), 1.5);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Backup Failed", $"Couldn't create the backup: {ex.Message}", "OK");
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
            "This replaces your current scene groups with the backup. Continue?", "Restore", "Cancel"))
        {
            return;
        }

        try
        {
            string targetPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                SceneOrganizerConstants.ASSET_PATH));

            File.Copy(backupPath, targetPath, true);
            // Only this one file changed: reimport it rather than refreshing the whole project.
            AssetDatabase.ImportAsset(SceneOrganizerConstants.ASSET_PATH, ImportAssetOptions.ForceUpdate);

            if (organizerWindow != null)
            {
                organizerWindow.LoadGroups();
            }

            ShowNotification(new GUIContent("Restored"), 1.5);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Restore Failed", $"Couldn't restore the backup: {ex.Message}", "OK");
        }
    }
}
