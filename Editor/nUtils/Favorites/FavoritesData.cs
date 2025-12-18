using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FavoriteItem
{
    public Object asset;
    public string note = "";
}

[System.Serializable]
public class FavoriteGroup
{
    public string name = "New Group";
    public Color color = new Color(0.3f, 0.5f, 0.8f, 0.3f);
    public bool isExpanded = true;
    public string note = "";
    public List<FavoriteItem> items = new List<FavoriteItem>();
    public List<FavoriteGroup> childGroups = new List<FavoriteGroup>();
    public int indentLevel = 0; // For rendering hierarchy depth
}

[CreateAssetMenu(fileName = "FavoritesData", menuName = "nUtils/Favorites Data")]
public class FavoritesData : ScriptableObject
{
    public List<FavoriteGroup> groups = new List<FavoriteGroup>();
    public List<FavoriteItem> ungroupedItems = new List<FavoriteItem>();
}
