using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace nUtils
{
    /// <summary>What a pick list entry is, for its filter tab.</summary>
    public enum PickKind { Sprite, Tile, Collider, UI, Other }

    /// <summary>One thing under the cursor: an object, or one tile of a tilemap.</summary>
    public sealed class PickEntry
    {
        public GameObject GameObject;
        public PickKind Kind;
        public string Name, Path, Detail;
        /// <summary>A picture of it: the sprite it draws, or the tile's.</summary>
        public Sprite Sprite;
        /// <summary>Its icon when there's no picture.</summary>
        public Texture Icon;
        public List<Texture> Components = new List<Texture>();
        /// <summary>Its component type names, for "t:" searches.</summary>
        public List<string> Types = new List<string>();
        /// <summary>The tile's tilemap and cell (tile entries).</summary>
        public Tilemap Tilemap;
        public Vector3Int Cell;
        /// <summary>Where it is, for the highlight: four corners in the world.</summary>
        public Vector3[] Outline;

        public bool IsTile => Tilemap != null;
        /// <summary>What picking it selects: a tile selects its tilemap.</summary>
        public GameObject Target => GameObject;
        /// <summary>The asset behind it (the tile, or the sprite): Alt-click picks that instead.</summary>
        public Object Asset
        {
            get
            {
                var tile = IsTile ? Tilemap.GetTile(Cell) : null;
                return tile != null ? tile : (Object)Sprite;
            }
        }
    }

    /// <summary>
    /// Everything under a point of the Scene view, front to back: what Unity's own picking finds there (asked again
    /// and again, ignoring what it already found), each tilemap's tile at that cell as its own entry, then the objects
    /// that draw nothing but have a 2D collider there (markers, areas, doors).
    /// </summary>
    public static class nSelectCandidates
    {
        const int MaxObjects = 64;

        public static List<PickEntry> At(Vector2 guiPosition)
        {
            var entries = new List<PickEntry>();
            var hidden = nSelectSettings.HiddenIcons;
            Vector3 world = HandleUtility.GUIPointToWorldRay(guiPosition).origin;
            world.z = 0f;

            var picked = new List<GameObject>();
            for (int i = 0; i < MaxObjects; i++)
            {
                var go = HandleUtility.PickGameObject(guiPosition, false, picked.ToArray());
                if (go == null || picked.Contains(go)) break;
                picked.Add(go);
                var tilemap = go.GetComponent<Tilemap>();
                if (tilemap != null && go.GetComponent<TilemapRenderer>() != null)
                {
                    if (!nSelectSettings.IncludeTiles) { entries.Add(Entry(go, hidden)); continue; }
                    var cell = tilemap.WorldToCell(world);
                    if (tilemap.HasTile(cell)) entries.Add(TileEntry(tilemap, cell, hidden));
                    continue;
                }
                entries.Add(Entry(go, hidden));
            }

            if (nSelectSettings.IncludeColliders)
            {
                Physics2D.SyncTransforms(); // objects moved in the editor since physics last looked
                foreach (var collider in Physics2D.OverlapPointAll(world, Physics2D.AllLayers, float.NegativeInfinity, float.PositiveInfinity))
                {
                    var go = collider.gameObject;
                    if (picked.Contains(go) || collider is TilemapCollider2D || collider is CompositeCollider2D) continue;
                    if (!go.scene.IsValid() || (go.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                    if (SceneVisibilityManager.instance.IsPickingDisabled(go)) continue;
                    picked.Add(go);
                    var entry = Entry(go, hidden);
                    entry.Kind = PickKind.Collider;
                    entry.Detail = collider.isTrigger ? "Trigger" : "Collider";
                    entry.Outline = Corners(collider.bounds);
                    entries.Add(entry);
                }
            }
            return entries;
        }

        static PickEntry Entry(GameObject go, HashSet<string> hidden)
        {
            var entry = new PickEntry { GameObject = go, Name = go.name, Path = PathOf(go.transform) };
            var sprite = go.GetComponent<SpriteRenderer>();
            var renderer = go.GetComponent<Renderer>();
            if (sprite != null)
            {
                entry.Kind = PickKind.Sprite;
                entry.Sprite = sprite.sprite;
                entry.Detail = $"{sprite.sortingLayerName} {sprite.sortingOrder}";
                entry.Outline = Corners(sprite.bounds);
            }
            else if (go.transform is RectTransform)
            {
                entry.Kind = PickKind.UI;
                entry.Sprite = UiSprite(go);
                var canvas = go.GetComponentInParent<Canvas>();
                entry.Detail = canvas != null ? canvas.rootCanvas.name : "UI";
                var corners = new Vector3[4];
                ((RectTransform)go.transform).GetWorldCorners(corners);
                entry.Outline = corners;
            }
            else
            {
                entry.Kind = PickKind.Other;
                if (renderer != null)
                {
                    entry.Detail = renderer.sortingLayerName != "Default" || renderer.sortingOrder != 0 ? $"{renderer.sortingLayerName} {renderer.sortingOrder}" : renderer.GetType().Name;
                    entry.Outline = Corners(renderer.bounds);
                }
                else entry.Outline = Corners(new Bounds(go.transform.position, Vector3.one * 0.5f));
                // Nothing drawn: say what it is by its main component ("Grid Area", "Grid Area Spawner").
                if (renderer == null)
                {
                    var main = go.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null);
                    if (main != null) entry.Detail = ObjectNames.NicifyVariableName(main.GetType().Name);
                }
            }
            entry.Icon = AssetPreview.GetMiniThumbnail(go);
            AddComponents(entry, go, hidden);
            return entry;
        }

        /// <summary>
        /// A UI image's sprite. nUtilities doesn't depend on uGUI (a project may not have it), so the Image is found by
        /// its "sprite" property rather than by type.
        /// </summary>
        static Sprite UiSprite(GameObject go)
        {
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null || component is SpriteRenderer) continue;
                var property = component.GetType().GetProperty("sprite");
                if (property != null && property.PropertyType == typeof(Sprite) && property.GetValue(component) is Sprite sprite) return sprite;
            }
            return null;
        }

        static PickEntry TileEntry(Tilemap tilemap, Vector3Int cell, HashSet<string> hidden)
        {
            var tile = tilemap.GetTile(cell);
            var entry = new PickEntry
            {
                GameObject = tilemap.gameObject, Kind = PickKind.Tile, Tilemap = tilemap, Cell = cell,
                // The layer leads (that's what tells tiles apart); the tile, tidied up, goes under it.
                Name = tilemap.name,
                Path = (tilemap.transform.parent != null ? tilemap.transform.parent.name + " · " : "") + TileName(tile),
                Sprite = tilemap.GetSprite(cell),
                Detail = $"({cell.x}, {cell.y})",
                Icon = AssetPreview.GetMiniThumbnail(tilemap.gameObject),
            };
            var centre = tilemap.GetCellCenterWorld(cell);
            var half = Vector3.Scale(tilemap.layoutGrid.cellSize, tilemap.transform.lossyScale) / 2f;
            entry.Outline = new[]
            {
                centre + new Vector3(-half.x, -half.y), centre + new Vector3(-half.x, half.y),
                centre + new Vector3(half.x, half.y), centre + new Vector3(half.x, -half.y),
            };
            AddComponents(entry, tilemap.gameObject, hidden);
            return entry;
        }

        /// <summary>"Tileset-Terrain-new grass.Tile.1129" as "Tileset-Terrain-new grass #1129".</summary>
        static string TileName(TileBase tile)
        {
            if (tile == null) return "Tile";
            int at = tile.name.LastIndexOf(".Tile.", System.StringComparison.Ordinal);
            return at > 0 ? tile.name.Substring(0, at) + " #" + tile.name.Substring(at + 6) : tile.name;
        }

        static void AddComponents(PickEntry entry, GameObject go, HashSet<string> hidden)
        {
            var seen = new HashSet<System.Type>();
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue; // a missing script
                var type = component.GetType();
                entry.Types.Add(type.Name);
                if (!seen.Add(type) || hidden.Contains(type.Name)) continue;
                var icon = AssetPreview.GetMiniThumbnail(component);
                // Scripts share one generic icon: once is enough.
                if (icon != null && !entry.Components.Contains(icon)) entry.Components.Add(icon);
            }
        }

        /// <summary>Its parents, nearest last ("World / Ground"); a tile's includes its own tilemap.</summary>
        static string PathOf(Transform t, bool includeSelf = false)
        {
            var parts = new List<string>();
            for (var p = includeSelf ? t : t.parent; p != null; p = p.parent) parts.Insert(0, p.name);
            if (parts.Count > 3) parts = new List<string> { "…" }.Concat(parts.Skip(parts.Count - 3)).ToList();
            return string.Join(" / ", parts);
        }

        static Vector3[] Corners(Bounds b) => new[]
        {
            new Vector3(b.min.x, b.min.y, b.center.z), new Vector3(b.min.x, b.max.y, b.center.z),
            new Vector3(b.max.x, b.max.y, b.center.z), new Vector3(b.max.x, b.min.y, b.center.z),
        };
    }
}
