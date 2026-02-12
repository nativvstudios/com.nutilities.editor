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

        // Cached styles
        private GUIStyle _headerStyle;
        private GUIStyle _pathStyle;
        private GUIStyle _statDotStyle;
        private bool _stylesReady;

        [MenuItem("Window/nUtilities/Hierarchy Organizer Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<HierarchyOrganizerWindow>("Hierarchy Organizer");
            window.minSize = new Vector2(340, 400);
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

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                margin = new RectOffset(0, 0, 0, 2),
            };

            _pathStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.5f, 0.5f, 0.5f)
                    : new Color(0.45f, 0.45f, 0.45f) },
            };

            _stylesReady = true;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawToolbar();
            DrawStats();
            GUILayout.Space(4);
            DrawSeparatorsList();
            GUILayout.Space(2);
            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search:", GUILayout.Width(48));
            string newSearch = EditorGUILayout.TextField(searchQuery,
                EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (newSearch != searchQuery)
            {
                searchQuery = newSearch;
                Repaint();
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(44)))
                {
                    searchQuery = "";
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Filter:", GUILayout.Width(48));

            string[] filterOptions = new string[]
            {
                "All Colors", "Blue", "Green", "Orange", "Purple",
                "Red", "Cyan", "Yellow", "Custom"
            };

            int currentFilter = (int)filterColor + 1;
            int newFilter = EditorGUILayout.Popup(currentFilter, filterOptions, EditorStyles.toolbarPopup);
            if (newFilter != currentFilter)
            {
                filterColor = (HierarchySeparator.SeparatorColor)(newFilter - 1);
                Repaint();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(54)))
            {
                RefreshSeparatorsList();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStats()
        {
            if (cachedSeparators == null || cachedSeparators.Count == 0)
                return;

            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6);

            // Total count
            GUILayout.Label($"{cachedSeparators.Count} separators", EditorStyles.miniLabel);
            GUILayout.Space(8);

            // Color breakdown as colored dots with counts
            var colorCounts = cachedSeparators
                .GroupBy(s => s.ColorPreset)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var kvp in colorCounts)
            {
                Color color = GetColorForPreset(kvp.Key);

                // Colored dot
                Rect dotRect = GUILayoutUtility.GetRect(10, 14, GUILayout.Width(10));
                Rect dot = new Rect(dotRect.x, dotRect.y + 2, 10, 10);

                // Dark border
                EditorGUI.DrawRect(new Rect(dot.x - 1, dot.y - 1, dot.width + 2, dot.height + 2),
                    new Color(0.1f, 0.1f, 0.1f));
                EditorGUI.DrawRect(dot, color);

                GUILayout.Label($"{kvp.Value}", EditorStyles.miniLabel, GUILayout.Width(16));
                GUILayout.Space(2);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Space(6);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);

            // Subtle separator line
            Rect lineRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, EditorGUIUtility.isProSkin
                ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.75f, 0.75f, 0.75f));
        }

        private void DrawSeparatorsList()
        {
            if (cachedSeparators == null || cachedSeparators.Count == 0)
            {
                DrawEmptyState();
                return;
            }

            var filteredSeparators = cachedSeparators.Where(s =>
            {
                if (s == null) return false;

                if (!string.IsNullOrEmpty(searchQuery) &&
                    !s.gameObject.name.ToLower().Contains(searchQuery.ToLower()))
                    return false;

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
                if (separator == null) continue;
                DrawSeparatorItem(separator);
                GUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSeparatorItem(HierarchySeparator separator)
        {
            Color sepColor = separator.GetColor();

            Rect cardRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Row 1: Name + Color dropdown + Custom color
            EditorGUILayout.BeginHorizontal();

            GUILayout.Space(6); // room for color bar

            // Editable name (delayed so undo is clean)
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.DelayedTextField(separator.gameObject.name,
                EditorStyles.textField);
            if (EditorGUI.EndChangeCheck() && !string.IsNullOrWhiteSpace(newName))
            {
                Undo.RecordObject(separator.gameObject, "Rename Separator");
                separator.gameObject.name = newName;
                EditorUtility.SetDirty(separator.gameObject);
                EditorApplication.RepaintHierarchyWindow();
            }

            // Color preset dropdown
            EditorGUI.BeginChangeCheck();
            var newPreset = (HierarchySeparator.SeparatorColor)EditorGUILayout.EnumPopup(
                separator.ColorPreset, GUILayout.Width(70));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(separator, "Change Separator Color");
                separator.ColorPreset = newPreset;
                EditorUtility.SetDirty(separator);
                EditorApplication.RepaintHierarchyWindow();
            }

            // Custom color picker (inline, only when Custom is selected)
            if (separator.ColorPreset == HierarchySeparator.SeparatorColor.Custom)
            {
                EditorGUI.BeginChangeCheck();
                Color newCustom = EditorGUILayout.ColorField(GUIContent.none,
                    separator.CustomColor, true, false, false, GUILayout.Width(40));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(separator, "Change Custom Color");
                    separator.CustomColor = newCustom;
                    EditorUtility.SetDirty(separator);
                    EditorApplication.RepaintHierarchyWindow();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Row 2: Preview bar
            GUILayout.Space(2);
            Rect previewRect = EditorGUILayout.GetControlRect(false, 18);
            previewRect.x += 6;
            previewRect.width -= 6;
            EditorGUI.DrawRect(previewRect, sepColor);

            GUIStyle previewLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 10,
            };
            previewLabel.normal.textColor = GetContrastColor(sepColor);
            GUI.Label(previewRect, separator.gameObject.name, previewLabel);

            // Row 3: Path + Action buttons
            GUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            GUILayout.Space(6);

            string path = GetGameObjectPath(separator.gameObject);
            EditorGUILayout.LabelField(path, _pathStyle);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(46)))
            {
                Selection.activeGameObject = separator.gameObject;
                EditorGUIUtility.PingObject(separator.gameObject);
            }

            if (GUILayout.Button("Focus", EditorStyles.miniButton, GUILayout.Width(42)))
            {
                Selection.activeGameObject = separator.gameObject;
                SceneView.FrameLastActiveSceneView();
            }

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                ShowDeleteOptionsDialog(separator);
            }
            GUI.backgroundColor = prevBg;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Draw color bar on the left edge of the card
            if (Event.current.type == EventType.Repaint)
            {
                Rect colorBar = new Rect(cardRect.x, cardRect.y, 4, cardRect.height);
                EditorGUI.DrawRect(colorBar, sepColor);
            }

            // Context menu
            if (Event.current.type == EventType.ContextClick &&
                cardRect.Contains(Event.current.mousePosition))
            {
                ShowSeparatorContextMenu(separator);
                Event.current.Use();
            }
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
                case 0:
                    Undo.DestroyObjectImmediate(separator.gameObject);
                    RefreshSeparatorsList();
                    break;
                case 1:
                    break;
                case 2:
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
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
            };
            GUILayout.Label("No Separators Found", titleStyle);

            GUILayout.Space(4);

            GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.5f, 0.5f, 0.5f)
                    : new Color(0.45f, 0.45f, 0.45f) },
            };
            GUILayout.Label("Create your first separator to organize your hierarchy.", hintStyle);

            GUILayout.Space(12);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Separator", GUILayout.Width(140), GUILayout.Height(28)))
            {
                CreateSeparatorWindow.ShowWindow();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Create New", EditorStyles.toolbarButton))
            {
                CreateSeparatorWindow.ShowWindow();
            }

            GUILayout.FlexibleSpace();

            if (cachedSeparators != null && cachedSeparators.Count > 0)
            {
                if (GUILayout.Button("Batch", EditorStyles.toolbarButton))
                {
                    ShowBatchOperationsMenu();
                }
            }

            if (GUILayout.Button("?", EditorStyles.toolbarButton, GUILayout.Width(24)))
            {
                ShowHelp();
            }

            EditorGUILayout.EndHorizontal();
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
                "\u2022 Create separators from the GameObject menu\n" +
                "\u2022 Edit names and colors inline in this window\n" +
                "\u2022 Quick Presets for common categories\n" +
                "\u2022 Filter and search separators\n" +
                "\u2022 Batch operations on multiple separators\n" +
                "\u2022 Right-click items for more options\n\n" +
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
                case HierarchySeparator.SeparatorColor.Blue:   return new Color(0.3f, 0.5f, 0.8f, 1f);
                case HierarchySeparator.SeparatorColor.Green:  return new Color(0.5f, 0.7f, 0.4f, 1f);
                case HierarchySeparator.SeparatorColor.Orange: return new Color(0.9f, 0.6f, 0.3f, 1f);
                case HierarchySeparator.SeparatorColor.Purple: return new Color(0.7f, 0.4f, 0.7f, 1f);
                case HierarchySeparator.SeparatorColor.Red:    return new Color(0.8f, 0.3f, 0.4f, 1f);
                case HierarchySeparator.SeparatorColor.Cyan:   return new Color(0.4f, 0.7f, 0.7f, 1f);
                case HierarchySeparator.SeparatorColor.Yellow: return new Color(0.9f, 0.8f, 0.3f, 1f);
                default: return Color.gray;
            }
        }

        private Color GetContrastColor(Color bg)
        {
            float luminance = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
            return luminance > 0.5f ? Color.black : Color.white;
        }
    }
}
