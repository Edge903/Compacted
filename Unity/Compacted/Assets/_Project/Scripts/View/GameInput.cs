using Compacted.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Compacted.View
{
    /// <summary>Keyboard and mouse controls (requirements §8.1). World clicks are ignored while the pointer is over UI.</summary>
    public sealed class GameInput : MonoBehaviour
    {
        public GameController game;
        public WorldView view;
        public GameUI ui;

        const float PanSpeed = 5f;   // tiles per second at zoom 1
        const float ZoomStep = 0.1f;
        float lastClickTime = -1;
        Cell lastClickTile;
        Cell? dragStart;

        // Press/release edges, sampled once per frame from each control's held state. This doesn't depend on
        // which input update an event arrived in, so it also works with injected input (automated tests).
        readonly System.Collections.Generic.Dictionary<UnityEngine.InputSystem.Controls.ButtonControl, bool> held =
            new System.Collections.Generic.Dictionary<UnityEngine.InputSystem.Controls.ButtonControl, bool>();
        readonly System.Collections.Generic.HashSet<UnityEngine.InputSystem.Controls.ButtonControl> pressed =
            new System.Collections.Generic.HashSet<UnityEngine.InputSystem.Controls.ButtonControl>();
        readonly System.Collections.Generic.HashSet<UnityEngine.InputSystem.Controls.ButtonControl> released =
            new System.Collections.Generic.HashSet<UnityEngine.InputSystem.Controls.ButtonControl>();

        void Sample(params UnityEngine.InputSystem.Controls.ButtonControl[] controls)
        {
            pressed.Clear(); released.Clear();
            foreach (var c in controls)
            {
                bool now = c.isPressed || c.wasPressedThisFrame;
                held.TryGetValue(c, out var was);
                if (now && !was) pressed.Add(c);
                if (!now && was) released.Add(c);
                held[c] = now && !c.wasReleasedThisFrame || c.isPressed;
            }
        }

        bool Down(UnityEngine.InputSystem.Controls.ButtonControl c) => pressed.Contains(c);
        bool Up(UnityEngine.InputSystem.Controls.ButtonControl c) => released.Contains(c);

        static readonly Key[] Digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            var watch = new System.Collections.Generic.List<UnityEngine.InputSystem.Controls.ButtonControl>
            {
                kb.escapeKey, kb.tabKey, kb.rKey, kb.qKey, kb.tKey, kb.cKey, mouse.leftButton, mouse.rightButton,
            };
            foreach (var d in Digits) watch.Add(kb[d]);
            Sample(watch.ToArray());

            if (Down(kb.escapeKey)) { ui.Escape(); return; }
            if (ui.IsTyping) return;
            if (Down(kb.tabKey) && !game.Paused) ui.TogglePanel(GameUI.Panel.Materials);
            if (game.Paused) { game.Hover = null; return; }

            // ---- keyboard ----
            var pan = Vector2.zero;
            if (kb.wKey.isPressed) pan.y += 1;
            if (kb.sKey.isPressed) pan.y -= 1;
            if (kb.aKey.isPressed) pan.x -= 1;
            if (kb.dKey.isPressed) pan.x += 1;
            // Pan faster when zoomed out, so the screen moves at about the same speed at any zoom.
            game.CameraOffset += pan * (PanSpeed * Time.unscaledDeltaTime / game.Zoom);

            if (Down(kb.rKey) && game.Slot >= 0) game.Rotate(kb.shiftKey.isPressed ? -1 : 1);
            if (Down(kb.qKey)) game.SetDestroyMode(!game.DestroyMode);
            if (Down(kb.tKey)) ui.TogglePanel(GameUI.Panel.Tech);
            if (Down(kb.cKey)) ui.TogglePanel(GameUI.Panel.Crafting);
            for (int i = 0; i < Digits.Length; i++)
            {
                if (!Down(kb[Digits[i]])) continue;
                if (game.Slot == i) game.Deselect();      // pressing a slot's key again deselects
                else { ui.CloseBrowser(); game.SelectSlot(i); }
            }

            // ---- mouse ----
            Vector2 screen = mouse.position.ReadValue();
            bool overUi = ui.IsPointerOverUI(screen);
            var tile = view.ScreenToTile(screen);
            var dim = game.World.Dims[game.CurrentDim];
            bool inBounds = dim.InBounds(tile.X, tile.Y);
            game.Hover = overUi ? (Cell?)null : tile;

            if (dragStart is Cell ds)
            {
                // Destroy-mode drag: clamp to the grid; confirm on release.
                var cx = Mathf.Clamp(tile.X, 0, dim.Size - 1);
                var cy = Mathf.Clamp(tile.Y, 0, dim.Size - 1);
                int x0 = Mathf.Min(ds.X, cx), x1 = Mathf.Max(ds.X, cx), y0 = Mathf.Min(ds.Y, cy), y1 = Mathf.Max(ds.Y, cy);
                game.DragRect = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                if (Up(mouse.leftButton) || !mouse.leftButton.isPressed)
                {
                    dragStart = null;
                    var r = game.DragRect.Value;
                    game.DragRect = null;
                    ui.ShowDestroyConfirm(game.World.PlanDestroy(game.CurrentDim, r.xMin, r.yMin, r.xMax - 1, r.yMax - 1));
                }
                return;
            }
            if (overUi) return;

            // Mouse wheel zooms between 0.5× and 1× of the fitted view (wheel up = closer).
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f) game.SetZoom(game.Zoom + Mathf.Sign(wheel) * ZoomStep);

            if (Down(mouse.rightButton))
            {
                if (game.Slot >= 0) game.Deselect();
                else if (game.DestroyMode) game.SetDestroyMode(false);
                return;
            }
            if (!Down(mouse.leftButton)) return;

            if (game.DestroyMode)
            {
                if (inBounds) { dragStart = tile; game.DragRect = new RectInt(tile.X, tile.Y, 1, 1); }
                return;
            }
            var type = game.SelectedType;
            if (type != null)
            {
                if (!inBounds) return;
                var res = game.World.Place(type, game.CurrentDim, tile.X, tile.Y, game.PreviewDir);
                if (!res.Ok) ui.Toast(res.Reason);
                return;
            }

            var e = inBounds ? game.World.At(game.CurrentDim, tile) : null;
            bool doubleClick = Time.unscaledTime - lastClickTime < 0.3f && tile == lastClickTile;
            lastClickTime = Time.unscaledTime;
            lastClickTile = tile;
            if (doubleClick && e is ControllerEntity ctrl) { ui.ClosePopup(); game.Enter(ctrl); return; }
            if (e != null) ui.OpenPopup(e); else ui.ClosePopup();
        }
    }
}
