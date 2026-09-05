using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AssetNotesData", menuName = "nUtils/Asset Notes Data")]
public class AssetNotesData : ScriptableObject
{
    [SerializeField]
    private List<AssetNote> assetNotes = new List<AssetNote>();

    /// <summary>
    /// Path -> note lookup rebuilt from the serialized list on demand. The note windows
    /// ask about every visible row on every repaint, and a linear scan per row made that
    /// cost grow with the number of notes stored.
    /// </summary>
    [NonSerialized] private Dictionary<string, AssetNote> noteIndex;

    [System.Serializable]
    public class AssetNote
    {
        public string assetPath;
        public string note;
    }

    private Dictionary<string, AssetNote> Index
    {
        get
        {
            if (noteIndex == null)
                RebuildIndex();

            return noteIndex;
        }
    }

    private void OnEnable()
    {
        // Deserialization replaces the list wholesale, so drop any stale index.
        noteIndex = null;
    }

    private void OnValidate()
    {
        noteIndex = null;
    }

    private void RebuildIndex()
    {
        noteIndex = new Dictionary<string, AssetNote>(assetNotes.Count, StringComparer.Ordinal);

        for (int i = 0; i < assetNotes.Count; i++)
        {
            var entry = assetNotes[i];
            if (entry != null && !string.IsNullOrEmpty(entry.assetPath))
                noteIndex[entry.assetPath] = entry;
        }
    }

    /// <summary>
    /// Get the note for a specific asset path
    /// </summary>
    public string GetNote(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return string.Empty;

        return Index.TryGetValue(assetPath, out var entry) && entry.note != null
            ? entry.note
            : string.Empty;
    }

    /// <summary>
    /// Set or update the note for an asset path
    /// </summary>
    public void SetNote(string assetPath, string noteText)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning("[AssetNotesData] Cannot set note - asset path is null or empty");
            return;
        }

        bool blank = string.IsNullOrWhiteSpace(noteText);

        if (Index.TryGetValue(assetPath, out var existing))
        {
            if (blank)
            {
                assetNotes.Remove(existing);
                noteIndex.Remove(assetPath);
            }
            else
            {
                existing.note = noteText;
            }
        }
        else if (!blank)
        {
            var entry = new AssetNote { assetPath = assetPath, note = noteText };
            assetNotes.Add(entry);
            noteIndex[assetPath] = entry;
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Check if an asset has a note
    /// </summary>
    public bool HasNote(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        return Index.TryGetValue(assetPath, out var entry) && !string.IsNullOrWhiteSpace(entry.note);
    }

    /// <summary>
    /// Remove a note for an asset
    /// </summary>
    public void RemoveNote(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;

        if (!Index.TryGetValue(assetPath, out var entry))
            return;

        assetNotes.Remove(entry);
        noteIndex.Remove(assetPath);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Get all asset paths that have notes
    /// </summary>
    public List<string> GetAllAssetPathsWithNotes()
    {
        var paths = new List<string>(assetNotes.Count);

        for (int i = 0; i < assetNotes.Count; i++)
        {
            var entry = assetNotes[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.note))
                paths.Add(entry.assetPath);
        }

        return paths;
    }

    /// <summary>
    /// Clear all notes
    /// </summary>
    public void ClearAllNotes()
    {
        assetNotes.Clear();
        noteIndex = null;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Get total count of notes
    /// </summary>
    public int GetNoteCount()
    {
        int count = 0;

        for (int i = 0; i < assetNotes.Count; i++)
        {
            var entry = assetNotes[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.note))
                count++;
        }

        return count;
    }
}
