using UnityEditor;
using UnityEngine;

namespace nUtils
{
    /// <summary>
    /// nSelect: pick anything under the cursor in the Scene view, even what's hidden behind other things.
    /// Watches the Scene view for the pick click (a right-click by default: pressed and let go without moving, so a
    /// right-drag still pans) and opens the list of what's under the cursor (<see cref="nSelectPopup"/>). Right-clicking
    /// where there's nothing leaves the click to Unity, so its own Scene view menu still opens. Also draws the
    /// highlight round the entry hovered in the list.
    /// </summary>
    [InitializeOnLoad]
    public static class nSelect
    {
        static Vector2 _downAt;
        static bool _armed, _swallowContextClick;

        /// <summary>The entry hovered in the open list: outlined in the Scene view.</summary>
        public static PickEntry Hovered { get; set; }

        static nSelect() => SceneView.duringSceneGui += OnSceneGUI;

        static bool TriggerHeld(Event e) => nSelectSettings.Trigger switch
        {
            PickTrigger.ShiftRightClick => e.shift && !e.alt && !e.control,
            PickTrigger.AltRightClick => e.alt && !e.shift && !e.control,
            _ => !e.shift && !e.alt && !e.control && !e.command,
        };

        static void OnSceneGUI(SceneView view)
        {
            var e = Event.current;
            if (e.type == EventType.Repaint && Hovered != null) DrawHighlight(Hovered);
            if (!nSelectSettings.Enabled) return;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 1:
                    // Not used: a right-drag still pans the view.
                    _armed = TriggerHeld(e);
                    _downAt = e.mousePosition;
                    break;
                case EventType.MouseDrag when e.button == 1 && _armed:
                    if ((e.mousePosition - _downAt).magnitude > nSelectSettings.DeadZone) _armed = false;
                    break;
                case EventType.MouseUp when e.button == 1 && _armed:
                    _armed = false;
                    if ((e.mousePosition - _downAt).magnitude > nSelectSettings.DeadZone) break;
                    var entries = nSelectCandidates.At(e.mousePosition);
                    if (entries.Count == 0) break; // nothing here: Unity's own menu opens
                    nSelectPopup.Open(entries, GUIUtility.GUIToScreenPoint(e.mousePosition));
                    _swallowContextClick = true;
                    e.Use();
                    break;
                case EventType.ContextClick when _swallowContextClick:
                    _swallowContextClick = false;
                    e.Use();
                    break;
            }
        }

        static void DrawHighlight(PickEntry entry)
        {
            if (!nSelectSettings.HoverHighlight || entry.Outline == null || entry.GameObject == null) return;
            var colour = nSelectSettings.HighlightColor;
            var fill = new Color(colour.r, colour.g, colour.b, colour.a * 0.18f);
            var zTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.DrawSolidRectangleWithOutline(entry.Outline, fill, colour);
            // A second, thicker edge so it reads over busy tiles.
            using (new Handles.DrawingScope(colour))
                for (int i = 0; i < 4; i++)
                    Handles.DrawLine(entry.Outline[i], entry.Outline[(i + 1) % 4], 2.5f);
            Handles.zTest = zTest;
        }
    }
}
