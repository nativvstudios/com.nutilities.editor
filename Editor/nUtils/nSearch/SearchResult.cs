using System;
using UnityEditor;
using UnityEngine;

public class SearchResult
{
    public string Name;
    public string Path;
    public Texture2D Icon;
    public UnityEngine.Object Target;
    public Action OnSelect;

    /// <summary>
    /// Asset results carry a path instead of a loaded object. Loading is deferred to
    /// the one result the user actually pings or opens, so a keystroke that produces a
    /// hundred hits doesn't pull a hundred assets off disk.
    /// </summary>
    public string AssetPath;

    /// <summary>
    /// Verb shown next to the Enter key in the footer ("Open", "Copy", "Create").
    /// Null means the default, "Open".
    /// </summary>
    public string ActionLabel;

    public UnityEngine.Object ResolveTarget()
    {
        if (Target == null && !string.IsNullOrEmpty(AssetPath))
            Target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath);

        return Target;
    }
}
