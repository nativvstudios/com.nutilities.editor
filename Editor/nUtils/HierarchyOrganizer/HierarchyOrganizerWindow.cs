using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace nUtils.HierarchyOrganizer
{
    public class HierarchyOrganizerWindow : EditorWindow
    {
        private Vector2 scrollPosition;
        private string searchQuery = "";
        private HierarchySeparator.SeparatorColor filterColor = (HierarchySeparator.SeparatorColor)(-1);
        private List<HierarchySeparator> cachedSeparators;
        private double lastRefreshTime;
        private const double REFRESH_INTERVAL = 0.5;

        [MenuItem("Window/nUtilities/Hierarchy Organizer Manager")]
        public static void ShowWindow()
        {
            HierarchyOrganizerWindow window = GetWindow<HierarchyOrganizerWindow>("Hierarchy Organizer");
            window.minSize = new Vector2(300, 400);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSeparatorsList();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnHierarchyChanged()
        {
            // Throttle refreshes
            if (EditorApplication.timeSinceStartup - lastRefreshTime > REFRESH_INTERVAL)
            {
                RefreshSeparatorsList();
                Repaint();
            }
        }

        private void RefreshSeparatorsList()
        {
            cachedSeparators = FindObjectsOfType<HierarchySeparator>()
                .OrderBy(s => s.transform.GetSiblingIndex())
                .ToList();
            lastRefreshTime = EditorApplication.timeSinceStartup;
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawStats();
            EditorGUILayout.Space(5);
            DrawSeparatorsList();
            EditorGUILayout.Space(5);
            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search field
            GUILayout.Label("Search:", GUILayout.Width(50));
            string newSearch = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (newSearch != searchQuery)
            {
                searchQuery = newSearch;
                Repaint();
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            // Filter toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Filter:", GUILayout.Width(50));

            string[] filterOptions = new string[]
            {
                "All Colors",
                "Blue",
                "Green",
                "Orange",
                "Purple",
                "Red",
                "Cyan",
                "Yellow",
                "Custom"
            };

            int currentFilter = (int)filterColor + 1;
            int newFilter = EditorGUILayout.Popup(currentFilter, filterOptions, EditorStyles.toolbarPopup);
            if (newFilter != currentFilter)
            {
                filterColor = (HierarchySeparator.SeparatorColor)(newFilter - 1);
                Repaint();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshSeparatorsList();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStats()
        {
            if (cachedSeparators == null || cachedSeparators.Count == 0)
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total Separators: {cachedSeparators.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Count by color
            var colorCounts = cachedSeparators
                .GroupBy(s => s.ColorPreset)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());

            EditorGUILayout.BeginHorizontal();
            foreach (var kvp in colorCounts)
            {
                Color color = GetColorForPreset(kvp.Key);
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = color;
                GUILayout.Label($"{kvp.Key}: {kvp.Value}", EditorStyles.miniButton, GUILayout.Width(80));
                GUI.backgroundColor = oldBg;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawSeparatorsList()
        {
            if (cachedSeparators == null || cachedSeparators.Count == 0)
            {
                DrawEmptyState();
                return;
            }

            // Filter separators
            var filteredSeparators = cachedSeparators.Where(s =>
            {
                if (s == null) return false;

                // Filter by search
                if (!string.IsNullOrEmpty(searchQuery) &&
                    !s.gameObject.name.ToLower().Contains(searchQuery.ToLower()))
                    return false;

                // Filter by color
                if ((int)filterColor >= 0 && s.ColorPreset != filterColor)
                    return false;

                return true;
            }).ToList();

            if (filteredSeparators.Count == 0)
            {
                EditorGUILayout.HelpBox("No separators match the current filter.", MessageType.Info);
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var separator in filteredSeparators)
            {
                if (separator == null)
                    continue;

                DrawSeparatorItem(separator);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSeparatorItem(HierarchySeparator separator)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Color indicator
            Color separatorColor = separator.GetColor();
            Rect colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
            EditorGUI.DrawRect(colorRect, separatorColor);

            // Name and details
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(separator.gameObject.name, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            // Color preset label
            GUILayout.Label(separator.ColorPreset.ToString(), EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();

            // Hierarchy path
            string path = GetGameObjectPath(separator.gameObject);
            EditorGUILayout.LabelField(path, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            // Action buttons
            EditorGUILayout.BeginVertical(GUILayout.Width(60));

            if (GUILayout.Button("Select", EditorStyles.miniButton))
            {
                Selection.activeGameObject = separator.gameObject;
                EditorGUIUtility.PingObject(separator.gameObject);
            }

            if (GUILayout.Button("Focus", EditorStyles.miniButton))
            {
                Selection.activeGameObject = separator.gameObject;
                SceneView.FrameLastActiveSceneView();
            }

            EditorGUILayout.EndVertical();

            // Delete button
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(22), GUILayout.Height(38)))
            {
                ShowDeleteOptionsDialog(separator);
            }
            GUI.backgroundColor = Color.white;

            // Context menu
            if (Event.current.type == EventType.ContextClick)
            {
                Rect itemRect = GUILayoutUtility.GetLastRect();
                if (itemRect.Contains(Event.current.mousePosition))
                {
                    ShowSeparatorContextMenu(separator);
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ShowDeleteOptionsDialog(HierarchySeparator separator)
        {
            int choice = EditorUtility.DisplayDialogComplex("Delete Separator",
                $"What would you like to do with '{separator.gameObject.name}'?",
                "Delete GameObject",
                "Cancel",
                "Remove Component Only");

            switch (choice)
            {
                case 0: // Delete GameObject
                    Undo.DestroyObjectImmediate(separator.gameObject);
                    RefreshSeparatorsList();
                    break;
                case 1: // Cancel
                    break;
                case 2: // Remove Component Only
                    Undo.DestroyObjectImmediate(separator);
                    RefreshSeparatorsList();
                    break;
            }
        }

        private void ShowSeparatorContextMenu(HierarchySeparator separator)
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Select"), false, () =>
            {
                Selection.activeGameObject = separator.gameObject;
            });

            menu.AddItem(new GUIContent("Focus in Scene"), false, () =>
            {
                Selection.activeGameObject = separator.gameObject;
                SceneView.FrameLastActiveSceneView();
            });

            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                Selection.activeGameObject = separator.gameObject;
                EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                EditorApplication.delayCall += () =>
                {
                    var window = EditorWindow.focusedWindow;
                    if (window != null)
                    {
                        Event e = new Event();
                        e.type = EventType.ValidateCommand;
                        e.commandName = "Rename";
                        window.SendEvent(e);
                        e.type = EventType.ExecuteCommand;
                        window.SendEvent(e);
                    }
                };
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                GameObject duplicate = Instantiate(separator.gameObject);
                duplicate.name = separator.gameObject.name;
                duplicate.transform.SetParent(separator.transform.parent);
                duplicate.transform.SetSiblingIndex(separator.transform.GetSiblingIndex() + 1);
                Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate Separator");
                RefreshSeparatorsList();
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                ShowDeleteOptionsDialog(separator);
            });

            menu.ShowAsContext();
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("No Hierarchy Separators Found", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Create your first separator to organize your scene!", EditorStyles.wordWrappedMiniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Separator", GUILayout.Width(150), GUILayout.Height(30)))
            {
                CreateSeparatorWindow.ShowWindow();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Create New Separator", EditorStyles.toolbarButton))
            {
                ShowCreateSeparatorMenu();
            }

            GUILayout.FlexibleSpace();

            if (cachedSeparators != null && cachedSeparators.Count > 0)
            {
                if (GUILayout.Button("Batch Operations", EditorStyles.toolbarButton))
                {
                    ShowBatchOperationsMenu();
                }
            }

            if (GUILayout.Button("Help", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                ShowHelp();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ShowCreateSeparatorMenu()
        {
            CreateSeparatorWindow.ShowWindow();
        }

        private void ShowBatchOperationsMenu()
        {
            GenericMenu menu = new GenericMenu();

            menu.AddItem(new GUIContent("Select All Separators"), false, () =>
            {
                Selection.objects = cachedSeparators.Select(s => s.gameObject).ToArray();
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Delete All Separators"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Delete All Separators",
                    $"Are you sure you want to delete all {cachedSeparators.Count} separators?",
                    "Delete All", "Cancel"))
                {
                    foreach (var separator in cachedSeparators)
                    {
                        if (separator != null)
                            Undo.DestroyObjectImmediate(separator.gameObject);
                    }
                    RefreshSeparatorsList();
                }
            });

            menu.AddItem(new GUIContent("Delete Filtered Separators"), false, () =>
            {
                var filtered = cachedSeparators.Where(s =>
                {
                    if (s == null) return false;
                    if (!string.IsNullOrEmpty(searchQuery) &&
                        !s.gameObject.name.ToLower().Contains(searchQuery.ToLower()))
                        return false;
                    if ((int)filterColor >= 0 && s.ColorPreset != filterColor)
                        return false;
                    return true;
                }).ToList();

                if (EditorUtility.DisplayDialog("Delete Filtered Separators",
                    $"Are you sure you want to delete {filtered.Count} separators?",
                    "Delete", "Cancel"))
                {
                    foreach (var separator in filtered)
                    {
                        if (separator != null)
                            Undo.DestroyObjectImmediate(separator.gameObject);
                    }
                    RefreshSeparatorsList();
                }
            });

            menu.ShowAsContext();
        }

        private void ShowHelp()
        {
            EditorUtility.DisplayDialog("Hierarchy Organizer Help",
                "Hierarchy Organizer helps you organize your scene with colored separators.\n\n" +
                "Features:\n" +
                "• Create colored separators from GameObject menu\n" +
                "• Use Quick Presets for common categories\n" +
                "• Filter and search separators\n" +
                "• Batch operations on multiple separators\n" +
                "• Right-click items for more options\n\n" +
                "Tip: Use consistent naming like '--- SECTION ---' for better organization!",
                "Got it!");
        }

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private Color GetColorForPreset(HierarchySeparator.SeparatorColor preset)
        {
            switch (preset)
            {
                case HierarchySeparator.SeparatorColor.Blue:
                    return new Color(0.3f, 0.5f, 0.8f, 1f);
                case HierarchySeparator.SeparatorColor.Green:
                    return new Color(0.5f, 0.7f, 0.4f, 1f);
                case HierarchySeparator.SeparatorColor.Orange:
                    return new Color(0.9f, 0.6f, 0.3f, 1f);
                case HierarchySeparator.SeparatorColor.Purple:
                    return new Color(0.7f, 0.4f, 0.7f, 1f);
                case HierarchySeparator.SeparatorColor.Red:
                    return new Color(0.8f, 0.3f, 0.4f, 1f);
                case HierarchySeparator.SeparatorColor.Cyan:
                    return new Color(0.4f, 0.7f, 0.7f, 1f);
                case HierarchySeparator.SeparatorColor.Yellow:
                    return new Color(0.9f, 0.8f, 0.3f, 1f);
                default:
                    return Color.gray;
            }
        }
    }
}
