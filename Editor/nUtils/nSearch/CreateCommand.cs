using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class CreateCommand : ISearchCommand
{
    public string Prefix => "create:";
    public string Description => "Create new assets";

    private string[] cachedMenuPaths;

    public void GetResults(string query, List<SearchResult> results)
    {
        if (cachedMenuPaths == null)
            cachedMenuPaths = FetchCreateMenuPaths();

        query = query.ToLower();

        foreach (string menuPath in cachedMenuPaths)
        {
            string displayName = GetDisplayName(menuPath);

            if (string.IsNullOrEmpty(query) || displayName.ToLower().Contains(query) || menuPath.ToLower().Contains(query))
            {
                string path = menuPath;
                results.Add(new SearchResult
                {
                    Name = displayName,
                    Path = menuPath,
                    Icon = EditorGUIUtility.IconContent("d_CreateAddNew").image as Texture2D,
                    OnSelect = () => EditorApplication.ExecuteMenuItem(path)
                });
            }

            if (results.Count >= 50)
                break;
        }
    }

    private static string GetDisplayName(string menuPath)
    {
        int lastSlash = menuPath.LastIndexOf('/');
        return lastSlash >= 0 ? menuPath.Substring(lastSlash + 1) : menuPath;
    }

    private static string[] FetchCreateMenuPaths()
    {
        try
        {
            var unsupportedType = typeof(EditorWindow).Assembly.GetType("UnityEditor.Unsupported");
            if (unsupportedType != null)
            {
                var method = unsupportedType.GetMethod("GetSubmenus", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    var paths = method.Invoke(null, new object[] { "Assets/Create" }) as string[];
                    if (paths != null)
                        return paths;
                }
            }
        }
        catch
        {
            // Reflection failed, fall through to empty
        }

        return Array.Empty<string>();
    }
}
