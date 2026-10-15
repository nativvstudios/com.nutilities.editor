using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Keeps Scene Organizer's list of the project's scenes current without searching the
/// whole AssetDatabase. A full scan (AssetDatabase.FindAssets("t:Scene")) costs hundreds
/// of milliseconds in a large project, so it runs once per editor session; after that the
/// list is patched from the import/delete/move lists Unity hands every AssetPostprocessor,
/// and it is cached in SessionState so recompiles do not rescan either.
/// </summary>
internal static class SceneOrganizerScenes
{
    private const string CacheKey = "nUtils.SceneOrganizer.ScenePaths";

    private static List<string> paths;

    /// <summary>
    /// Raised after scenes were added, deleted or moved: (added, deleted, movedTo, movedFrom).
    /// Re-imports of scenes already known (saving a scene) do not raise it.
    /// </summary>
    public static event Action<IList<string>, IList<string>, IList<string>, IList<string>> Changed;

    /// <summary>Every scene path in the project (Assets and Packages).</summary>
    public static IReadOnlyList<string> All
    {
        get
        {
            Ensure();
            return paths;
        }
    }

    // An empty list is never trusted as "the project has no scenes": it is what a scan returns
    // while the AssetDatabase is still waking up after a script reload.
    private static bool Cached => (paths != null && paths.Count > 0) || !string.IsNullOrEmpty(SessionState.GetString(CacheKey, null));

    private static void Ensure()
    {
        if (paths != null && paths.Count > 0)
            return;

        string cached = SessionState.GetString(CacheKey, null);
        if (!string.IsNullOrEmpty(cached))
        {
            paths = new List<string>(cached.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            return;
        }

        Rescan();
    }

    /// <summary>Lists the scenes from scratch (the Refresh button, and once per session).</summary>
    public static void Rescan()
    {
        // GetAllAssetPaths filtered by extension is several times faster than a typed
        // FindAssets query and finds the same scenes.
        var found = new List<string>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
            if (IsScene(path))
                found.Add(path);

        // Mid-reload GetAllAssetPaths can come back empty: ask the slower query instead.
        if (found.Count == 0)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsScene(path) && !found.Contains(path))
                    found.Add(path);
            }
        }

        paths = found;
        if (found.Count > 0)
            Store();
    }

    private static void Store() => SessionState.SetString(CacheKey, string.Join("\n", paths));

    public static bool IsScene(string path) =>
        !string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

    private sealed class Watcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var added = new List<string>();
            var removed = new List<string>();
            var to = new List<string>();
            var from = new List<string>();

            for (int i = 0; i < moved.Length; i++)
            {
                if (IsScene(moved[i]) || IsScene(movedFrom[i]))
                {
                    to.Add(moved[i]);
                    from.Add(movedFrom[i]);
                }
            }

            foreach (string path in deleted)
                if (IsScene(path))
                    removed.Add(path);

            // Nothing cached yet means nobody has asked for the list this session: no work.
            bool cached = Cached;
            if (cached)
                Ensure();

            foreach (string path in imported)
                if (IsScene(path) && !to.Contains(path) && (!cached || !paths.Contains(path)))
                    added.Add(path);

            if (added.Count == 0 && removed.Count == 0 && to.Count == 0)
                return;

            if (cached)
            {
                foreach (string path in removed) paths.Remove(path);
                foreach (string path in from) paths.Remove(path);
                foreach (string path in to) if (IsScene(path) && !paths.Contains(path)) paths.Add(path);
                foreach (string path in added) if (!paths.Contains(path)) paths.Add(path);
                Store();
            }

            // A scene renamed or moved in the Project window keeps its place in groups and
            // recents. Saved after the import finishes, not from inside it.
            if (to.Count > 0)
                EditorApplication.delayCall += () => FollowMoves(to, from);

            Changed?.Invoke(added, removed, to, from);
        }
    }

    private static void FollowMoves(List<string> to, List<string> from)
    {
        SceneOrganizerUserState.instance.Rename(from, to);

        var data = AssetDatabase.LoadAssetAtPath<SceneGroupData>(SceneOrganizerConstants.ASSET_PATH);
        if (data == null)
            return;

        bool changed = false;
        foreach (var group in data.sceneGroups)
        {
            for (int i = 0; i < group.scenes.Count; i++)
            {
                int moved = from.IndexOf(group.scenes[i]);
                if (moved < 0)
                    continue;

                group.scenes[i] = to[moved];
                changed = true;
            }
        }

        if (!changed)
            return;

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);
    }
}

/// <summary>
/// Per-user Scene Organizer state, kept in UserSettings (not an imported asset and not
/// shared through version control): recently opened scenes. Opening a scene used to
/// rewrite the shared SceneGroupData asset, which reimported it and showed up as a change
/// for everyone on the team.
/// </summary>
[FilePath("UserSettings/nUtilities/SceneOrganizer.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class SceneOrganizerUserState : ScriptableSingleton<SceneOrganizerUserState>
{
    [SerializeField] private List<string> recentScenes = new List<string>();
    [SerializeField] private bool migratedRecents;

    /// <summary>Raised when the recent scenes change.</summary>
    public static event Action RecentsChanged;

    public IReadOnlyList<string> RecentScenes
    {
        get
        {
            MigrateOnce();
            return recentScenes;
        }
    }

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        // Every scene opened counts, not only those opened from the organizer.
        EditorSceneManager.sceneOpened += (scene, mode) =>
        {
            if (!string.IsNullOrEmpty(scene.path))
                instance.AddRecent(scene.path);
        };
    }

    public void AddRecent(string path)
    {
        MigrateOnce();
        if (recentScenes.Count > 0 && recentScenes[0] == path)
            return;

        recentScenes.Remove(path);
        recentScenes.Insert(0, path);
        if (recentScenes.Count > SceneGroupData.MAX_RECENT_SCENES)
            recentScenes.RemoveRange(SceneGroupData.MAX_RECENT_SCENES, recentScenes.Count - SceneGroupData.MAX_RECENT_SCENES);

        Save(true);
        RecentsChanged?.Invoke();
    }

    /// <summary>Drops recents that are not in <paramref name="known"/>.</summary>
    public void Prune(ICollection<string> known)
    {
        MigrateOnce();
        if (recentScenes.RemoveAll(path => !known.Contains(path)) > 0)
            Save(true);
    }

    public void Rename(IList<string> from, IList<string> to)
    {
        MigrateOnce();
        bool changed = false;
        for (int i = 0; i < recentScenes.Count; i++)
        {
            int moved = from.IndexOf(recentScenes[i]);
            if (moved < 0)
                continue;

            recentScenes[i] = to[moved];
            changed = true;
        }

        if (!changed)
            return;

        Save(true);
        RecentsChanged?.Invoke();
    }

    /// <summary>Brings over the recents older versions kept in the shared SceneGroupData asset.</summary>
    private void MigrateOnce()
    {
        if (migratedRecents)
            return;

        migratedRecents = true;
        var data = AssetDatabase.LoadAssetAtPath<SceneGroupData>(SceneOrganizerConstants.ASSET_PATH);
        if (data != null)
        {
            foreach (string path in data.recentScenes)
                if (!recentScenes.Contains(path))
                    recentScenes.Add(path);
        }

        Save(true);
    }
}
