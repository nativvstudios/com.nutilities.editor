using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SceneGroupData", menuName = "ScriptableObjects/SceneGroupData", order = 1)]
public class SceneGroupData : ScriptableObject
{
    public List<SceneGroup> sceneGroups = new List<SceneGroup>();
    public List<string> recentScenes = new List<string>();
    public const int MAX_RECENT_SCENES = 10;

    [System.Serializable]
    public class SceneGroup
    {
        public string groupName;
        public List<string> scenes = new List<string>();
        public Color groupColor = Color.white;
        public bool isCollapsed = false;
    }

    public void AddToRecent(string scenePath)
    {
        recentScenes.Remove(scenePath); // Remove if exists
        recentScenes.Insert(0, scenePath); // Add to front

        if (recentScenes.Count > MAX_RECENT_SCENES)
        {
            recentScenes.RemoveAt(recentScenes.Count - 1);
        }
    }
}