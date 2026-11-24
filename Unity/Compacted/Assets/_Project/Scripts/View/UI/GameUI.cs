using System;
using System.Collections.Generic;
using System.Linq;
using Compacted.Data;
using Compacted.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Compacted.View
{
    /// <summary>
    /// The whole UI Toolkit interface: HUD, toolbar, big windows (browser, materials, tech tree, crafting),
    /// info popups, port/output-side windows, destroy confirmation and the pause menu. Built in code.
    /// </summary>
    public sealed partial class GameUI : MonoBehaviour
    {
        public enum Panel { None, Materials, Tech, Crafting }

        public UIDocument document;
        public GameController game;
        public WorldView view;
        public StyleSheet style;

        VisualElement root, hud, bar, crumb, side, bigWindow, popup, sideWindow, confirm, pauseLayer;
        Button leaveButton;
        Label modeLabel, toastLabel;
        readonly List<VisualElement> slots = new List<VisualElement>();
        VisualElement destroySlot, descriptionBox;
        Label descriptionTitle, descriptionText;
        const string DescriptionPlaceholder = "Description placeholder";
        readonly Dictionary<Panel, Button> sideTabs = new Dictionary<Panel, Button>();
        float toastUntil;

        public Panel OpenPanel { get; private set; }
        public bool BrowserOpen { get; private set; }

        ContentDatabase Db => game.database;
        World W => game.World;

        // Start, not OnEnable: the UIDocument creates its root visual element in its own OnEnable.
        void Start()
        {
            root = document.rootVisualElement;
            root.Clear();
            if (style != null) root.styleSheets.Add(style);
            root.pickingMode = PickingMode.Ignore;
            Build();
            game.Changed += OnChanged;
            OnChanged();
        }

        void OnDestroy() { if (game != null) game.Changed -= OnChanged; }

        // ---------- helpers ----------
        static T Add<T>(VisualElement parent, T el, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) el.AddToClassList(c);
            parent?.Add(el);
            return el;
        }
        static VisualElement Div(VisualElement parent, params string[] classes) => Add(parent, new VisualElement(), classes);
        static Label Text(VisualElement parent, string text, params string[] classes) => Add(parent, new Label(text), classes);
        static Button Btn(VisualElement parent, string text, Action onClick, params string[] classes) => Add(parent, new Button(onClick) { text = text }, classes);
        static VisualElement Layer(VisualElement parent)
        {
            var l = Div(parent, "layer");
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        VisualElement Dot(VisualElement parent, Color c)
        {
            var d = Div(parent, "dot");
            d.style.backgroundColor = c;
            return d;
        }

        /// <summary>A row: muted label on the left, value on the right.</summary>
        VisualElement Row(VisualElement parent, string label, string value = null)
        {
            var r = Div(parent, "row");
            Text(r, label);
            if (value != null) Text(r, value, "bold");
            return r;
        }

        VisualElement ItemLabel(VisualElement parent, string id, string suffix = "")
        {
            var w = Div(parent, "with-dot");
            Dot(w, view.ItemColor(id));
            Text(w, NameOf(id) + suffix);
            return w;
        }

        public string NameOf(string id)
        {
            if (id == null) return "";
            if (game.Content.Items.TryGetValue(id, out var i) && !string.IsNullOrEmpty(i.Name)) return i.Name;
            if (game.Content.Fluids.TryGetValue(id, out var f)) return f.Name;
            if (game.Content.Buildings.TryGetValue(id, out var b)) return b.Name;
            return id;
        }

        public static string Ml(double ml) => ml >= 1000 ? (ml / 1000).ToString(ml >= 10000 ? "0" : "0.0") + " L" : Mathf.RoundToInt((float)ml) + " ml";

        public string RecipeLabel(string recipeId)
        {
            var r = game.Content.Recipes[recipeId];
            return string.Join(", ", r.Out.Keys.Concat(r.FluidOut.Keys).Select(NameOf));
        }

        // ---------- queries for input ----------
        public bool IsTyping => root.focusController?.focusedElement is TextField;

        public bool IsPointerOverUI(Vector2 screen)
        {
            if (root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            return root.panel.Pick(p) != null;
        }

        // ---------- build ----------
        void Build()
        {
            hud = Layer(root);

            var top = Div(hud, "top");
            leaveButton = Btn(top, "⟵ Leave", () => game.Leave());
            crumb = Div(top, "crumb");

            modeLabel = Text(hud, "", "mode");
            modeLabel.pickingMode = PickingMode.Ignore;

            side = Div(hud, "side");
            sideTabs[Panel.Materials] = Btn(side, "Materials  Tab", () => TogglePanel(Panel.Materials), "side-tab");
            sideTabs[Panel.Tech] = Btn(side, "Tech tree  T", () => TogglePanel(Panel.Tech), "side-tab");
            sideTabs[Panel.Crafting] = Btn(side, "Crafting  C", () => TogglePanel(Panel.Crafting), "side-tab");

            var hint = Text(hud, "WASD camera · wheel zoom · 1–9 slot · R rotate\nLMB place / inspect · RMB deselect\nQ destroy · Tab materials · T tech · C craft\nEsc close / pause · double-click a controller to enter", "hint");
            hint.pickingMode = PickingMode.Ignore;

            bar = Div(hud, "bar");
            bar.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 9; i++)
            {
                int index = i;
                var s = Div(bar, "slot");
                s.RegisterCallback<ClickEvent>(_ => SlotClicked(index));
                slots.Add(s);
            }
            Div(bar, "divider");
            destroySlot = Div(bar, "slot", "destroy-slot");
            Text(destroySlot, "Q", "slot-key");
            Text(destroySlot, "✕", "trash");
            destroySlot.RegisterCallback<ClickEvent>(_ => { CloseBrowser(); game.SetDestroyMode(!game.DestroyMode); });

            // Description of the selected toolbar block, just above the toolbar.
            descriptionBox = Add(hud, new VisualElement(), "box", "desc");
            descriptionBox.pickingMode = PickingMode.Ignore;
            descriptionTitle = Text(descriptionBox, "", "desc-title");
            descriptionText = Text(descriptionBox, "", "desc-text");
            descriptionBox.style.display = DisplayStyle.None;

            bigWindow = Add(root, new VisualElement(), "box", "big");
            bigWindow.style.display = DisplayStyle.None;

            popup = Add(root, new VisualElement(), "box", "popup");
            popup.style.display = DisplayStyle.None;
            sideWindow = Add(root, new VisualElement(), "box", "ports");
            sideWindow.style.display = DisplayStyle.None;
            confirm = Add(root, new VisualElement(), "box", "confirm");
            confirm.style.display = DisplayStyle.None;

            BuildPause();

            toastLabel = Text(root, "", "toast");
            toastLabel.pickingMode = PickingMode.Ignore;
        }

        // ---------- per-frame ----------
        void Update()
        {
            if (root == null) return;
            UpdateHud();
            UpdatePopup();
            UpdateBigWindow();
            if (Time.unscaledTime > toastUntil) toastLabel.RemoveFromClassList("show");
        }

        void OnChanged()
        {
            RebuildToolbar();
            UpdateDescription();
            RebuildCrumb();
            if (game.SelectedEntity == 0) ClosePopup();
            if (BrowserOpen) RenderBrowser();
        }

        void UpdateHud()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var type = game.Toolbar[i];
                var count = slots[i].Q<Label>(className: "slot-count");
                if (count != null && type != null) count.text = PlaceCount(type);
            }
            var def = game.SelectedDef;
            modeLabel.EnableInClassList("destroy", game.DestroyMode);
            modeLabel.text = game.DestroyMode ? "Destroy mode · click or drag to select"
                : game.Slot >= 0 && def == null ? "Empty slot · click it again to choose a block"
                : def != null ? $"Placing {def.Name}{(def.Rotates ? " · facing " + game.PreviewDir : "")}{(def.Placement == Placement.Wall ? " · must touch a wall" : "")}"
                : "";
        }

        public string PlaceCount(string type) =>
            game.Content.Buildings[type].Free || W.FreeBuild ? "∞" : W.InvCount(type).ToString();

        public void Toast(string msg)
        {
            toastLabel.text = msg;
            toastLabel.AddToClassList("show");
            toastUntil = Time.unscaledTime + 2f;
        }

        // ---------- toolbar ----------
        void RebuildToolbar()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                s.Clear();
                s.EnableInClassList("sel", game.Slot == i);
                Text(s, (i + 1).ToString(), "slot-key").pickingMode = PickingMode.Ignore;
                var type = game.Toolbar[i];
                if (type != null)
                {
                    s.Add(new BlockIcon(Db.Building(type), 34));
                    Text(s, PlaceCount(type), "slot-count").pickingMode = PickingMode.Ignore;
                    s.tooltip = NameOf(type);
                }
                else s.tooltip = "Empty slot";
            }
            destroySlot.EnableInClassList("sel", game.DestroyMode);
        }

        /// <summary>Show the selected toolbar block's description above the toolbar; hide it when nothing is selected.</summary>
        void UpdateDescription()
        {
            var type = game.SelectedType;
            var def = type != null ? Db.Building(type) : null;
            if (def == null) { descriptionBox.style.display = DisplayStyle.None; return; }
            descriptionTitle.text = def.displayName;
            descriptionText.text = string.IsNullOrWhiteSpace(def.description) ? DescriptionPlaceholder : def.description.Trim();
            descriptionBox.style.display = DisplayStyle.Flex;
        }

        void SlotClicked(int i)
        {
            // Clicking the selected slot opens the block browser; otherwise it selects the slot.
            if (game.Slot == i) { OpenBrowser(); return; }
            CloseBrowser();
            game.SelectSlot(i);
        }

        // ---------- breadcrumb ----------
        void RebuildCrumb()
        {
            crumb.Clear();
            var path = game.DimPath();
            for (int i = 0; i < path.Count; i++)
            {
                int id = path[i];
                bool last = i == path.Count - 1;
                var l = Text(crumb, game.DimName(id), "crumb-item");
                if (last) l.AddToClassList("current");
                else l.RegisterCallback<ClickEvent>(_ => game.GoToDim(id, zoomIn: false));
                if (!last) Text(crumb, "›", "crumb-sep");
            }
            leaveButton.style.display = path.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------- escape ----------
        /// <summary>Esc: close the confirm, browser, popup or window first; otherwise toggle the pause menu.</summary>
        public void Escape()
        {
            if (confirm.style.display == DisplayStyle.Flex) { CloseConfirm(); return; }
            if (BrowserOpen) { CloseBrowser(); return; }
            if (popup.style.display == DisplayStyle.Flex) { ClosePopup(); return; }
            if (OpenPanel != Panel.None) { TogglePanel(OpenPanel); return; }
            SetPaused(!game.Paused);
        }
    }
}
