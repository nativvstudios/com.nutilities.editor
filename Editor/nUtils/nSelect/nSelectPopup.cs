using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace nUtils
{
    /// <summary>
    /// The list of what's under the cursor, front to back: a picture of each (its sprite or tile), its name and where it
    /// sits, its sorting layer or cell, and its components' icons. Search by name or "t:Type", filter by kind, hover to
    /// outline it in the Scene view. Click picks it (closing the list); Ctrl or Shift-click adds to the selection and keeps
    /// the list open; double-click picks and frames it; Alt-click picks the tile or sprite asset. Arrows and Enter work
    /// too, and Escape closes.
    /// </summary>
    public class nSelectPopup : EditorWindow
    {
        const float Width = 380f, RowHeight = 38f, MaxRows = 11;

        static readonly (PickKind? kind, string label)[] Tabs =
        {
            (null, "All"), (PickKind.Sprite, "Sprites"), (PickKind.Tile, "Tiles"), (PickKind.Collider, "Colliders"),
            (PickKind.UI, "UI"), (PickKind.Other, "Other"),
        };

        List<PickEntry> _all = new List<PickEntry>();
        readonly List<PickEntry> _shown = new List<PickEntry>();
        PickKind? _filter;
        string _query = "";
        ListView _list;
        TextField _search;
        VisualElement _tabs;
        Label _empty;

        public static void Open(List<PickEntry> entries, Vector2 screenPosition)
        {
            foreach (var old in Resources.FindObjectsOfTypeAll<nSelectPopup>()) old.Close();
            var popup = CreateInstance<nSelectPopup>();
            popup._all = entries;
            float header = (nSelectSettings.ShowSearch ? 30f : 0f) + (nSelectSettings.ShowFilters ? 28f : 0f) + 8f;
            float height = header + Mathf.Min(entries.Count, MaxRows) * RowHeight + 6f;
            popup.ShowAsDropDown(new Rect(screenPosition, Vector2.one), new Vector2(Width, height));
        }

        void OnDestroy()
        {
            nSelect.Hovered = null;
            SceneView.RepaintAll();
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            var style = AssetDatabase.FindAssets("nSelectStyles t:StyleSheet").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StyleSheet>).FirstOrDefault();
            if (style != null) root.styleSheets.Add(style);
            root.AddToClassList("pick");

            if (nSelectSettings.ShowSearch)
            {
                _search = new TextField { name = "search" };
                _search.AddToClassList("pick__search");
                _search.textEdition.placeholder = "Search, or t:Type";
                _search.RegisterValueChangedCallback(e => { _query = e.newValue ?? ""; Refresh(); });
                // Down from the search goes into the list; Enter picks the first match.
                _search.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.DownArrow && _shown.Count > 0) { _list.Focus(); _list.SetSelection(0); e.StopPropagation(); }
                    else if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && _shown.Count > 0) { Pick(_shown[0], EventModifiers.None, 1); e.StopPropagation(); }
                }, TrickleDown.TrickleDown);
                root.Add(_search);
            }

            if (nSelectSettings.ShowFilters)
            {
                _tabs = new VisualElement();
                _tabs.AddToClassList("pick__tabs");
                root.Add(_tabs);
            }

            _list = new ListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                makeItem = MakeRow,
                bindItem = BindRow,
                itemsSource = _shown,
            };
            _list.AddToClassList("pick__list");
            _list.selectionChanged += _ => { if (_list.selectedItem is PickEntry entry) Hover(entry); };
            _list.itemsChosen += chosen => { if (chosen.FirstOrDefault() is PickEntry entry) Pick(entry, EventModifiers.None, 1); };
            root.Add(_list);

            _empty = new Label("Nothing matches.");
            _empty.AddToClassList("pick__empty");
            root.Add(_empty);

            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) { Close(); e.StopPropagation(); return; }
                // Typing while the list has focus searches.
                if (_search != null && e.character != '\0' && !char.IsControl(e.character) && _search.focusController?.focusedElement != _search)
                {
                    _search.Focus();
                    _search.value += e.character;
                    _search.SelectRange(_search.value.Length, _search.value.Length);
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerLeaveEvent>(_ => Hover(null));

            Refresh();
            if (_search != null) _search.schedule.Execute(() => _search.Focus());
            else _list.schedule.Execute(() => { _list.Focus(); if (_shown.Count > 0) _list.SetSelection(0); });
        }

        // ------------------------------------------------------------------ filtering

        void Refresh()
        {
            _shown.Clear();
            _shown.AddRange(_all.Where(e => (_filter == null || e.Kind == _filter) && Matches(e)));
            _list?.RefreshItems();
            if (_empty != null) _empty.style.display = _shown.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            BuildTabs();
        }

        bool Matches(PickEntry entry)
        {
            foreach (var word in _query.Split(' ').Where(w => w.Length > 0))
            {
                if (word.StartsWith("t:", System.StringComparison.OrdinalIgnoreCase))
                {
                    string type = word.Substring(2);
                    if (type.Length > 0 && !entry.Types.Any(t => t.IndexOf(type, System.StringComparison.OrdinalIgnoreCase) >= 0)) return false;
                }
                else if (entry.Name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) < 0
                         && (entry.Path ?? "").IndexOf(word, System.StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            return true;
        }

        void BuildTabs()
        {
            if (_tabs == null) return;
            _tabs.Clear();
            foreach (var (kind, label) in Tabs)
            {
                int count = kind == null ? _all.Count : _all.Count(e => e.Kind == kind);
                if (kind != null && count == 0) continue; // only the kinds that are here
                var tab = new Button(() => { _filter = _filter == kind ? null : kind; Refresh(); }) { text = $"{label} {count}" };
                tab.AddToClassList("pick__tab");
                if (_filter == kind) tab.AddToClassList("pick__tab--on");
                _tabs.Add(tab);
            }
        }

        // ------------------------------------------------------------------ rows

        sealed class Row
        {
            public PickEntry Entry;
            public Image Picture;
            public Label Name, Path, Detail;
            public VisualElement Icons;
        }

        VisualElement MakeRow()
        {
            var element = new VisualElement();
            element.AddToClassList("pick__row");
            var row = new Row();
            element.userData = row;

            row.Picture = new Image { scaleMode = ScaleMode.ScaleToFit };
            row.Picture.AddToClassList("pick__picture");
            element.Add(row.Picture);

            var text = new VisualElement();
            text.AddToClassList("pick__text");
            row.Name = new Label();
            row.Name.AddToClassList("pick__name");
            row.Path = new Label();
            row.Path.AddToClassList("pick__path");
            text.Add(row.Name);
            text.Add(row.Path);
            element.Add(text);

            var right = new VisualElement();
            right.AddToClassList("pick__right");
            row.Detail = new Label();
            row.Detail.AddToClassList("pick__detail");
            row.Icons = new VisualElement();
            row.Icons.AddToClassList("pick__icons");
            right.Add(row.Detail);
            right.Add(row.Icons);
            element.Add(right);

            element.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (row.Entry == null) return;
                int i = _shown.IndexOf(row.Entry);
                if (i >= 0) _list.SetSelectionWithoutNotify(new[] { i });
                Hover(row.Entry);
            });
            element.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.button != 0 || row.Entry == null) return;
                Pick(row.Entry, e.modifiers, e.clickCount);
                e.StopPropagation();
            });
            return element;
        }

        void BindRow(VisualElement element, int index)
        {
            if (!(element.userData is Row row) || index < 0 || index >= _shown.Count) return;
            var entry = _shown[index];
            row.Entry = entry;
            if (entry.Sprite != null) { row.Picture.sprite = entry.Sprite; row.Picture.image = null; }
            else { row.Picture.sprite = null; row.Picture.image = entry.Icon; }
            row.Picture.EnableInClassList("pick__picture--icon", entry.Sprite == null);
            row.Name.text = entry.Name;
            row.Path.text = nSelectSettings.ShowPaths ? entry.Path : "";
            row.Path.style.display = nSelectSettings.ShowPaths && !string.IsNullOrEmpty(entry.Path) ? DisplayStyle.Flex : DisplayStyle.None;
            row.Detail.text = entry.Detail ?? "";
            row.Detail.EnableInClassList("pick__detail--tile", entry.Kind == PickKind.Tile);
            row.Detail.EnableInClassList("pick__detail--collider", entry.Kind == PickKind.Collider);
            row.Icons.Clear();
            foreach (var icon in entry.Components.Take(6))
            {
                var image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("pick__icon");
                row.Icons.Add(image);
            }
            bool selected = entry.GameObject != null && UnityEditor.Selection.Contains(entry.GameObject);
            element.EnableInClassList("pick__row--selected", selected);
            element.tooltip = entry.IsTile ? $"{entry.Path} in the {entry.Name} layer at {entry.Detail}" : entry.GameObject != null ? entry.GameObject.name : "";
        }

        // ------------------------------------------------------------------ picking

        void Hover(PickEntry entry)
        {
            if (nSelect.Hovered == entry) return;
            nSelect.Hovered = entry;
            SceneView.RepaintAll();
        }

        void Pick(PickEntry entry, EventModifiers modifiers, int clicks)
        {
            if (entry == null || entry.GameObject == null) return;
            bool alt = (modifiers & EventModifiers.Alt) != 0;
            bool toggle = (modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
            bool add = (modifiers & EventModifiers.Shift) != 0;

            if (alt && entry.Asset != null)
            {
                UnityEditor.Selection.activeObject = entry.Asset; // the tile or sprite itself
                EditorGUIUtility.PingObject(entry.Asset);
                Close();
                return;
            }
            var target = entry.Target;
            if (toggle || add)
            {
                var objects = UnityEditor.Selection.objects.ToList();
                if (toggle && objects.Contains(target)) objects.Remove(target);
                else if (!objects.Contains(target)) objects.Add(target);
                UnityEditor.Selection.objects = objects.ToArray();
                _list.RefreshItems(); // the ticks follow; the list stays open for more
                return;
            }
            UnityEditor.Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
            if (clicks >= 2 && SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Close();
        }
    }
}
