using System.Collections.Generic;
using System.Linq;
using Compacted.Data;
using Compacted.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Compacted.View
{
    // Large centred windows: block browser, Materials, Tech tree, Crafting. Only one is open at a time.
    public sealed partial class GameUI
    {
        BrowserTab browserTab = BrowserTab.Logistics;
        string windowSignature;
        string techSelected;

        public void TogglePanel(Panel p)
        {
            OpenPanel = OpenPanel == p ? Panel.None : p;
            if (OpenPanel != Panel.None) CloseBrowser();
            windowSignature = null;
            foreach (var kv in sideTabs) kv.Value.EnableInClassList("on", kv.Key == OpenPanel);
            bigWindow.style.display = OpenPanel != Panel.None ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------- block browser ----------
        public void OpenBrowser()
        {
            if (game.Slot < 0) return;
            if (OpenPanel != Panel.None) TogglePanel(OpenPanel);
            BrowserOpen = true;
            RenderBrowser();
        }

        public void CloseBrowser()
        {
            if (!BrowserOpen) return;
            BrowserOpen = false;
            bigWindow.style.display = DisplayStyle.None;
        }

        void RenderBrowser()
        {
            bigWindow.Clear();
            bigWindow.style.display = DisplayStyle.Flex;
            Header(bigWindow, $"Block browser · slot {game.Slot + 1}", CloseBrowser);
            var tabs = Div(bigWindow, "tabs");
            foreach (var t in new[] { BrowserTab.Logistics, BrowserTab.Machines, BrowserTab.Other })
            {
                var tab = Text(tabs, t.ToString(), "tab");
                tab.EnableInClassList("on", t == browserTab);
                tab.RegisterCallback<ClickEvent>(_ => { browserTab = t; RenderBrowser(); });
            }
            var grid = Div(bigWindow, "grid");
            foreach (var b in Db.buildings.Where(b => b != null && b.browserTab == browserTab))
            {
                var cell = Div(grid, "bi");
                cell.Add(new BlockIcon(b, 64));
                Text(cell, b.displayName);
                Text(cell, PlaceCount(b.id), "muted", "small");
                var id = b.id;
                cell.RegisterCallback<ClickEvent>(_ => { game.AssignSlot(game.Slot, id); CloseBrowser(); });
            }
        }

        VisualElement Header(VisualElement parent, string title, System.Action onClose)
        {
            var h = Div(parent, "head");
            Text(h, title);
            Btn(h, "✕", onClose);
            return h;
        }

        // ---------- materials / crafting / tech ----------
        void UpdateBigWindow()
        {
            if (OpenPanel == Panel.None) return;
            string sig = OpenPanel + "|" + string.Join(",", W.Inventory.OrderBy(kv => kv.Key).Select(kv => kv.Key + kv.Value))
                         + "|" + string.Join(",", W.TechDone) + "|" + techSelected + "|" + W.UnlockedHand.Count;
            if (sig == windowSignature) return;
            windowSignature = sig;
            bigWindow.Clear();
            switch (OpenPanel)
            {
                case Panel.Materials: RenderMaterials(); break;
                case Panel.Crafting: RenderCrafting(); break;
                case Panel.Tech: RenderTech(); break;
            }
        }

        void RenderMaterials()
        {
            Header(bigWindow, "Materials", () => TogglePanel(Panel.Materials));
            var scroll = Add(bigWindow, new ScrollView(), "big-body");
            var keys = Db.items.Where(i => i != null && W.InvCount(i.id) > 0).ToList();
            var buildingItems = new HashSet<ItemDefinition>(Db.buildings.Where(b => b != null && b.item != null).Select(b => b.item));
            void Section(string title, IEnumerable<ItemDefinition> list)
            {
                Text(scroll, title, "sec");
                var grid = Div(scroll, "matgrid");
                bool any = false;
                foreach (var i in list)
                {
                    any = true;
                    var r = Div(grid, "row", "matcell");
                    ItemLabel(r, i.id);
                    Text(r, $"{W.InvCount(i.id)} / {game.Content.Tuning.InventoryCap}", "bold");
                }
                if (!any) Text(grid, "None", "muted");
            }
            Section("Materials", keys.Where(i => !buildingItems.Contains(i)));
            Section("Buildings", keys.Where(i => buildingItems.Contains(i)));
        }

        void RenderCrafting()
        {
            Header(bigWindow, "Crafting", () => TogglePanel(Panel.Crafting));
            Text(bigWindow, "Craft by hand from your inventory. Instant.", "muted");
            var scroll = Add(bigWindow, new ScrollView(), "big-body");
            var list = W.HandRecipes();
            if (list.Count == 0) Text(scroll, "No hand recipes yet. Complete tech tree nodes to unlock them.", "muted");
            foreach (var rid in list)
            {
                var r = game.Content.Recipes[rid];
                var outId = r.Out.Keys.First();
                var row = Div(scroll, "crow");
                var name = Div(row, "crow-name");
                ItemLabel(name, outId).Q<Label>().AddToClassList("bold");
                Text(name, $"Have {W.InvCount(outId)}", "muted", "small");
                var ins = Div(row, "crow-in");
                foreach (var kv in r.In) NeedLabel(ins, kv.Key, kv.Value);
                bool ok = W.HasAll(r.In);
                var b = Btn(row, "Craft", () =>
                {
                    var res = W.HandCraft(rid);
                    Toast(res.Ok ? $"Crafted {RecipeLabel(rid)}" : res.Reason);
                });
                b.EnableInClassList("dim", !ok);
            }
        }

        void NeedLabel(VisualElement parent, string item, int need)
        {
            int have = W.InvCount(item);
            var l = ItemLabel(parent, item, $" ×{need} ({have})");
            if (have < need) l.Q<Label>().AddToClassList("lack");
        }

        // ---------- tech tree ----------
        const float CellW = 160, RowH = 84, NodeW = 136, NodeH = 46, Pad = 14;

        void RenderTech()
        {
            Header(bigWindow, "Tech tree", () => TogglePanel(Panel.Tech));
            var nodes = game.Content.TechOrder.Select(id => game.Content.Tech[id]).ToList();
            if (techSelected == null || W.StateOf(techSelected) == TechState.Hidden)
                techSelected = nodes.FirstOrDefault(n => W.StateOf(n.Id) == TechState.Open)?.Id ?? nodes.FirstOrDefault()?.Id;

            var canvasScroll = Add(bigWindow, new ScrollView(ScrollViewMode.Horizontal), "tcanvas");
            int cols = nodes.Count == 0 ? 1 : nodes.Max(n => n.Col) + 1, rows = nodes.Count == 0 ? 1 : nodes.Max(n => n.Row) + 1;
            var canvas = new VisualElement();
            canvas.style.width = cols * CellW + Pad;
            canvas.style.height = rows * RowH + Pad;
            canvasScroll.Add(canvas);

            // Lines: from every visible node, including to hidden next nodes (as a hint).
            var lines = new List<(Vector2 a, Vector2 b, TechState to)>();
            foreach (var n in nodes)
            foreach (var pre in n.Prereqs)
            {
                if (W.StateOf(pre) == TechState.Hidden) continue;
                var p = game.Content.Tech[pre];
                lines.Add((Pos(p) + new Vector2(NodeW, NodeH / 2), Pos(n) + new Vector2(0, NodeH / 2), W.StateOf(n.Id)));
            }
            var lineLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            lineLayer.style.position = Position.Absolute;
            lineLayer.style.left = 0; lineLayer.style.top = 0; lineLayer.style.right = 0; lineLayer.style.bottom = 0;
            lineLayer.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D;
                foreach (var (a, b, to) in lines)
                {
                    p.strokeColor = to == TechState.Done ? Palette.In : to == TechState.Open ? Palette.Muted : Palette.Off;
                    p.lineWidth = to == TechState.Hidden ? 1.5f : 2f;
                    float mx = (a.x + b.x) / 2;
                    p.BeginPath();
                    p.MoveTo(a);
                    p.BezierCurveTo(new Vector2(mx, a.y), new Vector2(mx, b.y), b);
                    p.Stroke();
                    if (to == TechState.Hidden)
                    {
                        p.fillColor = Palette.Off;
                        p.BeginPath(); p.Arc(b, 4, 0, 360); p.Fill();
                    }
                }
            };
            canvas.Add(lineLayer);

            foreach (var n in nodes)
            {
                var st = W.StateOf(n.Id);
                if (st == TechState.Hidden) continue;
                var box = Text(canvas, (st == TechState.Done ? "✓ " : "") + n.Name, "tnode", st == TechState.Done ? "done" : "open");
                box.EnableInClassList("sel", n.Id == techSelected);
                var pos = Pos(n);
                box.style.left = pos.x; box.style.top = pos.y;
                var id = n.Id;
                box.RegisterCallback<ClickEvent>(_ => { techSelected = id; windowSignature = null; });
            }

            if (techSelected == null) return;
            var t = game.Content.Tech[techSelected];
            var state = W.StateOf(t.Id);
            var detail = Div(bigWindow, "tdetail");
            var head = Div(detail, "head");
            Text(head, t.Name);
            Text(head, state == TechState.Done ? "Completed" : "Available", "muted", "small");
            if (t.Cost.Count > 0) NeedRow(detail, "Hand over", t.Cost);
            if (t.Requires.Count > 0) NeedRow(detail, "Requires (checked, not taken)", t.Requires);
            var rewards = new List<string>();
            if (t.Grant.Count > 0) rewards.Add("Grants: " + string.Join(", ", t.Grant.Select(kv => $"{kv.Value} {NameOf(kv.Key)}")));
            if (t.UnlockHand.Count > 0) rewards.Add("Hand recipes: " + string.Join(", ", t.UnlockHand.Select(RecipeLabel)));
            if (t.UnlockMachine.Count > 0) rewards.Add("Machine recipes: " + string.Join(", ", t.UnlockMachine.Select(RecipeLabel)));
            var rr = Row(detail, "Rewards");
            var rl = Text(rr, rewards.Count > 0 ? string.Join("\n", rewards) : "None · checkpoint");
            rl.style.whiteSpace = WhiteSpace.Normal;
            rl.style.unityTextAlign = TextAnchor.UpperRight;
            if (state == TechState.Open)
            {
                var acts = Div(detail, "acts");
                bool ok = W.CanCompleteTech(t.Id).Ok;
                var b = Btn(acts, t.Cost.Count > 0 ? "Hand over" : "Complete", () =>
                {
                    var res = W.CompleteTech(t.Id, out var lost);
                    if (!res.Ok) { Toast(res.Reason); return; }
                    Toast($"{t.Name} complete" + (lost.Count > 0 ? " · inventory full, lost " + string.Join(", ", lost.Select(kv => $"{kv.Value} {NameOf(kv.Key)}")) : ""));
                    game.NotifyChanged();
                }, ok ? "on" : "dim");
            }
        }

        static Vector2 Pos(TechNodeDef n) => new Vector2(Pad + n.Col * CellW, Pad + n.Row * RowH);

        void NeedRow(VisualElement parent, string label, Dictionary<string, int> map)
        {
            var r = Div(parent, "row");
            Text(r, label);
            var list = Div(r, "need-list");
            foreach (var kv in map) NeedLabel(list, kv.Key, kv.Value);
        }
    }
}
