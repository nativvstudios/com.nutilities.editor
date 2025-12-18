using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "AssetNotesData", menuName = "nUtils/Asset Notes Data")]
public class AssetNotesData : ScriptableObject
{
    [SerializeField]
    private List<AssetNote> assetNotes = new List<AssetNote>();

    [System.Serializable]
    public class AssetNote
    {
        public string assetPath;
        public string note;
    }

    /// <summary>
    /// Get the note for a specific asset path
    /// </summary>
    public string GetNote(string assetPath)
    {
        var note = assetNotes.FirstOrDefault(n => n.assetPath == assetPath);
        return note != null ? note.note : string.Empty;
    }

    /// <summary>
    /// Set or update the note for an asset path
    /// </summary>
    public void SetNote(string assetPath, string noteText)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            UnityEngine.Debug.LogWarning("[AssetNotesData] Cannot set note - asset path is null or empty");
            return;
        }

        UnityEngine.Debug.Log($"[AssetNotesData] SetNote called for: {assetPath} (note length: {(noteText?.Length ?? 0)})");

        var existingNote = assetNotes.FirstOrDefault(n => n.assetPath == assetPath);

        if (existingNote != null)
        {
            if (string.IsNullOrWhiteSpace(noteText))
            {
                // Remove note if text is empty
                assetNotes.Remove(existingNote);
            }
            else
            {
                // Update existing note
                existingNote.note = noteText;
            }
        }
        else if (!string.IsNullOrWhiteSpace(noteText))
        {
            // Add new note
            assetNotes.Add(new AssetNote
            {
                assetPath = assetPath,
                note = noteText
            });
        }

        // Mark as dirty for saving
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif

        UnityEngine.Debug.Log($"[AssetNotesData] Note saved. Total notes: {assetNotes.Count}");
    }

    /// <summary>
    /// Check if an asset has a note
    /// </summary>
    public bool HasNote(string assetPath)
    {
        bool result = assetNotes.Any(n => n.assetPath == assetPath && !string.IsNullOrWhiteSpace(n.note));
        if (result)
        {
            UnityEngine.Debug.Log($"[AssetNotesData] HasNote({assetPath}): true");
        }
        return result;
    }

    /// <summary>
    /// Remove a note for an asset
    /// </summary>
    public void RemoveNote(string assetPath)
    {
        var note = assetNotes.FirstOrDefault(n => n.assetPath == assetPath);
        if (note != null)
        {
            assetNotes.Remove(note);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    /// <summary>
    /// Get all asset paths that have notes
    /// </summary>
    public List<string> GetAllAssetPathsWithNotes()
    {
        return assetNotes.Where(n => !string.IsNullOrWhiteSpace(n.note))
                        .Select(n => n.assetPath)
                        .ToList();
    }

    /// <summary>
    /// Clear all notes
    /// </summary>
    public void ClearAllNotes()
    {
        assetNotes.Clear();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Get total count of notes
    /// </summary>
    public int GetNoteCount()
    {
        return assetNotes.Count(n => !string.IsNullOrWhiteSpace(n.note));
    }
}
