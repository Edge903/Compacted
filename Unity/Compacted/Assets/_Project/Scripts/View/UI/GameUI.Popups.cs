using System.Collections.Generic;
using System.Linq;
using Compacted.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Compacted.View
{
    // Info popup for a placed block, the port / output-side window beside it, and the destroy confirmation.
    public sealed partial class GameUI
    {
        readonly Dictionary<string, (VisualElement el, string sig)> live = new Dictionary<string, (VisualElement, string)>();
        VisualElement progressFill;
        World.DestroyPlan pendingPlan;

        public void OpenPopup(Entity e)
        {
            game.SelectedEntity = e.Id;
            BuildPopup();
        }

        public void ClosePopup()
        {
            game.SelectedEntity = 0;
            popup.style.display = DisplayStyle.None;
            sideWindow.style.display = DisplayStyle.None;
        }

        VisualElement Live(VisualElement parent, string key)
        {
            var el = Div(parent);
            live[key] = (el, null);
            return el;
        }

        /// <summary>Rebuild a live section only when its content changed, so buttons inside stay clickable.</summary>
        void SetLive(string key, string sig, System.Action<VisualElement> build)
        {
            if (!live.TryGetValue(key, out var entry) || entry.sig == sig) return;
            entry.el.Clear();
            build(entry.el);
            live[key] = (entry.el, sig);
        }

        void BuildPopup()
        {
            var e = W.Get(game.SelectedEntity);
            if (e == null) { ClosePopup(); return; }
            popup.Clear();
            live.Clear();
            progressFill = null;
            popup.style.display = DisplayStyle.Flex;
            string title = e is ControllerEntity c0 ? c0.Name : e.Def.Name;
            Header(popup, title, ClosePopup);

            switch (e)
            {
                case MachineEntity _:
                case CoreEntity _:
                {
                    RecipeDropdown(popup, e);
                    if (e is CoreEntity) { Text(popup, "Structure", "sec"); Live(popup, "form"); }
                    string recipe = e is MachineEntity m ? m.Recipe : ((CoreEntity)e).Recipe;
                    if (recipe != null)
                    {
                        var r = game.Content.Recipes[recipe];
                        Text(popup, "Input · needs " + RecipeNeeds(r), "sec").style.whiteSpace = WhiteSpace.Normal;
                        Live(popup, "in");
                        Row(popup, "Time", r.Time + "s");
                    }
                    Text(popup, "Output", "sec");
                    Live(popup, "out");
                    var st = Div(popup, "row");
                    Text(st, "Status");
                    Live(st, "status");
                    var prog = Div(popup, "prog");
                    progressFill = Div(prog, "prog-fill");
                    break;
                }
                case BeltEntity _: Live(popup, "belt"); break;
                case PipeEntity _: Live(popup, "pipe"); break;
                case HatchEntity h:
                    Row(popup, "Facing", $"{h.Dir} · {(h.Def.Io == HatchIo.In ? "accepts from" : "outputs to")} that side only");
                    Live(popup, "hatch");
                    break;
                case PartEntity _: Live(popup, "part"); break;
                case ControllerEntity c:
                {
                    var nr = Div(popup, "row");
                    Text(nr, "Name");
                    var field = Add(nr, new TextField { value = c.Name, maxLength = 24 });
                    field.style.width = 150;
                    field.RegisterValueChangedCallback(ev =>
                    {
                        c.Name = string.IsNullOrWhiteSpace(ev.newValue) ? "Box" : ev.newValue.Trim();
                        RebuildCrumb();
                    });
                    Row(popup, "Interior", $"{c.Def.ControllerSize}×{c.Def.ControllerSize}");
                    var br = Div(popup, "row");
                    Text(br, "Blocks inside");
                    Live(br, "count");
                    var acts = Div(popup, "acts");
                    Btn(acts, "Enter", () => { ClosePopup(); game.Enter(c); }, "on");
                    break;
                }
            }

            if (e is ControllerEntity ce) BuildPorts(ce);
            else if (e is MachineEntity me) BuildSides(me);
            else sideWindow.style.display = DisplayStyle.None;
            UpdatePopup();
        }

        string RecipeNeeds(RecipeDef r)
        {
            var parts = r.In.Select(kv => $"{kv.Value} {NameOf(kv.Key)}").Concat(r.FluidIn.Select(kv => $"{Ml(kv.Value)} {NameOf(kv.Key)}")).ToList();
            if (r.Fuel > 0) parts.Add($"{r.Fuel} fuel");
            return parts.Count > 0 ? string.Join(" + ", parts) : "nothing";
        }

        void RecipeDropdown(VisualElement parent, Entity e)
        {
            string current = e is MachineEntity m ? m.Recipe : ((CoreEntity)e).Recipe;
            var ids = W.RecipesFor(e);
            if (current != null && !ids.Contains(current)) ids.Insert(0, current);
            var labels = new List<string> { "Choose…" };
            labels.AddRange(ids.Select(RecipeLabel));
            int index = current == null ? 0 : ids.IndexOf(current) + 1;
            var r = Div(parent, "row");
            Text(r, "Recipe");
            var dd = Add(r, new DropdownField(labels, index));
            dd.style.width = 160;
            dd.RegisterValueChangedCallback(ev =>
            {
                int i = labels.IndexOf(ev.newValue);
                int lost = W.SetRecipe(e, i <= 0 ? null : ids[i - 1]);
                if (lost > 0) Toast($"Inventory full: {lost} item{(lost > 1 ? "s" : "")} lost");
                BuildPopup();
            });
        }

        // ---------- live update ----------
        void UpdatePopup()
        {
            if (popup.style.display != DisplayStyle.Flex) return;
            var e = W.Get(game.SelectedEntity);
            if (e == null || e.Dim != game.CurrentDim) { ClosePopup(); return; }
            PositionPopup(e);

            switch (e)
            {
                case MachineEntity m: LiveMachine(m); break;
                case CoreEntity c: LiveCore(c); break;
                case BeltEntity b:
                    SetLive("belt", (b.Item ?? "") + W.BeltRate(b).ToString("0.0"), el =>
                    {
                        var r = Div(el, "row");
                        Text(r, "Carrying");
                        if (b.Item != null) ItemLabel(r, b.Item); else Text(r, "Nothing", "bold");
                        Row(el, "Throughput", W.BeltRate(b).ToString("0.0") + " /s");
                        Row(el, "Max", (1 / game.Content.Tuning.BeltTime).ToString("0.0") + " /s");
                    });
                    break;
                case PipeEntity p:
                {
                    var s = W.SegmentOf(p);
                    SetLive("pipe", s == null ? "-" : $"{s.Fluid}{s.Amount:0}{s.Pipes.Count}", el =>
                    {
                        if (s == null) { Row(el, "Segment", "…"); return; }
                        var r = Div(el, "row");
                        Text(r, "Fluid");
                        if (s.Fluid != null && s.Amount > 0) ItemLabel(r, s.Fluid); else Text(r, "Empty", "bold");
                        Row(el, "This pipe", $"{Ml(W.PipeAmount(p))} / {Ml(game.Content.Tuning.PipeCapacity)}");
                        Row(el, "Segment", $"{Ml(s.Amount)} / {Ml(s.Capacity)} ({Mathf.RoundToInt((float)s.Fill * 100)}%)");
                        Row(el, "Pipes in segment", s.Pipes.Count.ToString());
                    });
                    break;
                }
                case HatchEntity h: LiveHatch(h); break;
                case PartEntity pt:
                {
                    var core = W.CoreOfPart(pt.Id);
                    SetLive("part", core?.Id.ToString() ?? "-", el =>
                    {
                        var r = Div(el, "row");
                        Text(r, "Structure");
                        Text(r, core != null ? $"Part of a formed {game.Content.Multiblocks[core.Def.Multiblock].Name}" : "Not in a formed structure", core != null ? "ok-text" : "muted");
                    });
                    break;
                }
                case ControllerEntity ctrl:
                {
                    int n = W.CountBlocks(ctrl.Inner);
                    SetLive("count", n.ToString(), el => Text(el, n.ToString(), "bold"));
                    break;
                }
            }
        }

        const float EdgeMargin = 16, BottomMargin = 30, Gap = 8;

        /// <summary>
        /// Fixed screen anchors, independent of the camera: the info window at centre-right, the I/O window
        /// (ports / output side) at bottom-right. If the info window is tall enough to reach the I/O window,
        /// it moves up to sit just above it.
        /// </summary>
        void PositionPopup(Entity e)
        {
            float screenH = root.layout.height;
            float h = popup.layout.height > 0 ? popup.layout.height : 200;
            float top = (screenH - h) / 2;
            if (sideWindow.style.display == DisplayStyle.Flex)
            {
                float sh = sideWindow.layout.height > 0 ? sideWindow.layout.height : 260;
                float sideTop = screenH - BottomMargin - sh;
                if (top + h > sideTop - Gap) top = sideTop - Gap - h;
            }
            popup.style.right = EdgeMargin;
            popup.style.top = Mathf.Max(EdgeMargin, top);
        }

        Button AddButton(VisualElement parent, Entity e, string item) =>
            Btn(parent, $"+ {W.InvCount(item)}", () =>
            {
                int n = W.InsertFromInventory(e, item);
                if (n == 0) Toast(W.InvCount(item) > 0 ? "Buffer is full" : $"No {NameOf(item)} in inventory");
            });

        void LiveMachine(MachineEntity m)
        {
            var r = m.Recipe != null ? game.Content.Recipes[m.Recipe] : null;
            var fuels = game.Content.Items.Values.Where(i => i.Fuel > 0 && W.InvCount(i.Id) > 0).Select(i => i.Id).ToList();
            if (r != null)
            {
                string sig = string.Join(",", r.In.Keys.Select(k => $"{k}{Get(m.InBuf, k)}/{W.InvCount(k)}"))
                           + string.Join(",", r.FluidIn.Keys.Select(f => $"{f}{(int)Get(m.FluidIn, f)}"))
                           + $"|{m.Fuel}|" + string.Join(",", fuels.Select(f => f + W.InvCount(f)));
                SetLive("in", sig, el =>
                {
                    foreach (var k in r.In.Keys)
                    {
                        var row = Div(el, "row");
                        ItemLabel(row, k);
                        var right = Div(row, "with-dot");
                        Text(right, $"{Get(m.InBuf, k)}/{W.InputCap(r, k)}  ", "bold");
                        AddButton(right, m, k);
                    }
                    foreach (var f in r.FluidIn.Keys)
                    {
                        var row = Div(el, "row");
                        ItemLabel(row, f);
                        Text(row, $"{Ml(Get(m.FluidIn, f))} / {Ml(game.Content.Tuning.FluidBuffer)}", "bold");
                    }
                    if (r.Fuel > 0)
                    {
                        var row = Div(el, "row");
                        var lw = Div(row, "with-dot"); Dot(lw, Palette.Fuel); Text(lw, "Fuel");
                        Text(row, $"{m.Fuel} / {W.FuelCap(m)} pts", "bold");
                        foreach (var f in fuels)
                        {
                            var fr = Div(el, "row");
                            ItemLabel(fr, f, $" ({game.Content.Items[f].Fuel} pt)").style.marginLeft = 12;
                            AddButton(fr, m, f);
                        }
                    }
                });
            }
            string osig = string.Join(",", m.OutBuf.Select(kv => kv.Key + kv.Value)) + "|" + string.Join(",", m.FluidOut.Select(kv => kv.Key + (int)kv.Value));
            SetLive("out", osig, el =>
            {
                if (m.OutBuf.Count == 0 && m.FluidOut.Count == 0) { Row(el, "Empty"); return; }
                int cap = W.OutputCapOf(m);
                foreach (var kv in m.OutBuf) { var row = Div(el, "row"); ItemLabel(row, kv.Key); Text(row, $"{kv.Value} / {cap}", "bold"); }
                foreach (var kv in m.FluidOut) { var row = Div(el, "row"); ItemLabel(row, kv.Key); Text(row, $"{Ml(kv.Value)} / {Ml(game.Content.Tuning.FluidBuffer)}", "bold"); }
                if (m.OutBuf.Count > 0)
                    Btn(Div(el, "acts"), "Take all", () => { int n = W.TakeOutput(m); Toast(n > 0 ? $"Took {n} item{(n > 1 ? "s" : "")}" : "Inventory is full for those items"); });
            });
            LiveStatus(m.Status, m.Crafting, m.Prog);
        }

        void LiveCore(CoreEntity c)
        {
            var mb = game.Content.Multiblocks[c.Def.Multiblock];
            SetLive("form", c.Formed != null ? "1" : "0", el =>
            {
                if (c.Formed != null) Text(el, $"✓ {mb.Name} formed", "ok-text");
                else
                {
                    Text(el, "Not formed", "danger-text");
                    var parts = mb.Counts.Select(kv => kv.Value.Min == kv.Value.Max ? $"{kv.Value.Min} {NameOf(kv.Key)}" : $"{kv.Value.Min}–{kv.Value.Max} {NameOf(kv.Key)}");
                    var adj = mb.Adjacent.Select(p => $"{NameOf(p.A)} touching {NameOf(p.B)}");
                    var l = Text(el, $"Needs a {mb.Width}×{mb.Height} of: {string.Join(", ", parts)}; {string.Join(", ", adj)}.", "muted", "small");
                    l.style.whiteSpace = WhiteSpace.Normal;
                }
            });
            var hatches = c.Formed != null ? c.Formed.Hatches.Select(id => W.Get<HatchEntity>(id)).ToList() : new List<HatchEntity>();
            if (c.Recipe != null)
            {
                var r = game.Content.Recipes[c.Recipe];
                int Total(string k) => hatches.Where(h => h.Def.Io == HatchIo.In).Sum(h => Get(h.Buf, k));
                SetLive("in", string.Join(",", r.In.Keys.Select(k => k + Total(k))), el =>
                {
                    foreach (var kv in r.In) { var row = Div(el, "row"); ItemLabel(row, kv.Key); Text(row, $"{Total(kv.Key)} / {kv.Value}", "bold"); }
                    Text(el, "Fill the input buses (click a bus to insert by hand).", "muted", "small").style.whiteSpace = WhiteSpace.Normal;
                });
            }
            var outTot = new Dictionary<string, int>();
            foreach (var bus in hatches.Where(x => x.Def.Io == HatchIo.Out))
                foreach (var kv in bus.Buf) outTot[kv.Key] = Get(outTot, kv.Key) + kv.Value;
            SetLive("out", string.Join(",", outTot.Select(kv => kv.Key + kv.Value)), el =>
            {
                if (outTot.Count == 0) { Row(el, "Empty"); return; }
                foreach (var kv in outTot) { var row = Div(el, "row"); ItemLabel(row, kv.Key); Text(row, kv.Value.ToString(), "bold"); }
                Text(el, "Held in the output bus.", "muted", "small");
            });
            LiveStatus(c.Status, c.Crafting, c.Prog);
        }

        void LiveHatch(HatchEntity h)
        {
            var core = W.Get<CoreEntity>(h.Core);
            bool formed = core?.Formed != null && core.Formed.Hatches.Contains(h.Id);
            string sig = formed + "|" + string.Join(",", h.Buf.Select(kv => kv.Key + kv.Value)) + "|" + string.Join(",", W.Inventory.Select(kv => kv.Key + kv.Value));
            SetLive("hatch", sig, el =>
            {
                var r = Div(el, "row");
                Text(r, "Structure");
                Text(r, formed ? "Formed" : "Not in a formed structure", formed ? "ok-text" : "danger-text");
                if (h.Buf.Count == 0) Row(el, "Empty");
                foreach (var kv in h.Buf) { var row = Div(el, "row"); ItemLabel(row, kv.Key); Text(row, kv.Value.ToString(), "bold"); }
                if (h.Def.Io == HatchIo.Out && h.Buf.Count > 0)
                    Btn(Div(el, "acts"), "Take all", () => { int n = W.TakeOutput(h); Toast(n > 0 ? $"Took {n} item{(n > 1 ? "s" : "")}" : "Inventory is full for those items"); });
                if (h.Def.Io == HatchIo.In && formed && core.Recipe != null)
                {
                    var cr = game.Content.Recipes[core.Recipe];
                    Text(el, "Insert by hand", "sec");
                    foreach (var k in cr.In.Keys)
                    {
                        var row = Div(el, "row");
                        ItemLabel(row, k, $"  {Get(h.Buf, k)}/{W.InputCap(cr, k)}");
                        AddButton(row, h, k);
                    }
                }
            });
        }

        void LiveStatus(MachineStatus status, bool crafting, double prog)
        {
            SetLive("status", status.ToString(), el =>
            {
                var l = Text(el, Palette.StatusText(status));
                l.style.color = Palette.Status(status);
            });
            if (progressFill != null) progressFill.style.width = Length.Percent(crafting ? (float)prog * 100 : 0);
        }

        static int Get(Dictionary<string, int> d, string k) => d.TryGetValue(k, out var v) ? v : 0;
        static double Get(Dictionary<string, double> d, string k) => d.TryGetValue(k, out var v) ? v : 0;

        // ---------- port window (controllers) and output-side window (machines) ----------
        static readonly Vector2[] PortCell = { new Vector2(60, 0), new Vector2(120, 60), new Vector2(60, 120), new Vector2(0, 60) };   // N E S W

        static string Glyph(PortMode m, Dir d)
        {
            switch (m)
            {
                case PortMode.In: return Arrow(d.Opposite());
                case PortMode.Out: return Arrow(d);
                case PortMode.Fluid: return "●";
                default: return "–";
            }
        }
        static string Arrow(Dir d) => d == Dir.N ? "▲" : d == Dir.E ? "▶" : d == Dir.S ? "▼" : "◀";
        static string ModeClass(PortMode m) => m == PortMode.In ? "in" : m == PortMode.Out ? "out" : m == PortMode.Fluid ? "fluid" : "off";
        static string ModeName(PortMode m) => m == PortMode.In ? "Item input" : m == PortMode.Out ? "Item output" : m == PortMode.Fluid ? "Fluid I/O" : "Off";

        void BuildPorts(ControllerEntity c)
        {
            sideWindow.Clear();
            sideWindow.style.display = DisplayStyle.Flex;
            Text(Div(sideWindow, "head"), "Ports");
            var grid = Div(sideWindow, "pgrid");
            for (int i = 0; i < 4; i++)
            {
                var d = (Dir)i;
                var mode = c.Ports[i];
                void Cycle(bool back)
                {
                    var next = back ? Prev(c.Ports[(int)d]) : Next(c.Ports[(int)d]);
                    if (W.SetPortMode(c, d, next) > 0) Toast("Inventory full: 1 item lost");
                    BuildPorts(c);
                }
                var b = Btn(grid, Glyph(mode, d), () => Cycle(false), "pbtn", ModeClass(mode));
                b.style.left = PortCell[i].x; b.style.top = PortCell[i].y;
                b.tooltip = $"{d}: {ModeName(mode)}";
                b.RegisterCallback<PointerDownEvent>(ev => { if (ev.button == 1) { ev.StopPropagation(); Cycle(true); } }, TrickleDown.TrickleDown);
                var lab = Text(grid, d.ToString(), "plabel");
                lab.style.left = PortCell[i].x + 40; lab.style.top = PortCell[i].y - 4;
            }
            var box = Text(grid, $"{c.Def.ControllerSize}×{c.Def.ControllerSize}", "pbox");
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = Palette.ControllerEdge;
            box.style.backgroundColor = Palette.Controller;
            var legend = Div(sideWindow, "legend");
            foreach (var (name, col) in new[] { ("In", Palette.In), ("Out", Palette.Out), ("Fluid", Palette.Fluid), ("Off", Palette.Off) })
            {
                var w = Div(legend, "with-dot"); Dot(w, col); Text(w, name, "small", "muted");
            }
            Text(sideWindow, "Click a port to cycle · right-click to go back", "phint");
        }

        static PortMode Next(PortMode m) => m == PortMode.Off ? PortMode.In : m == PortMode.In ? PortMode.Out : m == PortMode.Out ? PortMode.Fluid : PortMode.Off;
        static PortMode Prev(PortMode m) => m == PortMode.Off ? PortMode.Fluid : m == PortMode.Fluid ? PortMode.Out : m == PortMode.Out ? PortMode.In : PortMode.Off;

        void BuildSides(MachineEntity m)
        {
            sideWindow.Clear();
            sideWindow.style.display = DisplayStyle.Flex;
            Text(Div(sideWindow, "head"), "Output side");
            var grid = Div(sideWindow, "pgrid");
            for (int i = 0; i < 4; i++)
            {
                var d = (Dir)i;
                bool isOut = m.Dir == d;
                var b = Btn(grid, isOut ? Arrow(d) : Arrow(d.Opposite()), () => { m.Dir = d; BuildSides(m); }, "pbtn", isOut ? "out" : "in");
                b.style.left = PortCell[i].x; b.style.top = PortCell[i].y;
                b.tooltip = $"{d}: {(isOut ? "Output" : "Input")}";
                var lab = Text(grid, d.ToString(), "plabel");
                lab.style.left = PortCell[i].x + 40; lab.style.top = PortCell[i].y - 4;
            }
            var box = Div(grid, "pbox");
            box.style.backgroundColor = view.VisualOf(m.Type).color;
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = new Color(1, 1, 1, 0.25f);
            var legend = Div(sideWindow, "legend");
            foreach (var (name, col) in new[] { ("Input", Palette.In), ("Output", Palette.Out) })
            {
                var w = Div(legend, "with-dot"); Dot(w, col); Text(w, name, "small", "muted");
            }
            Text(sideWindow, "Click a side to make it the output", "phint");
        }

        // ---------- destroy confirmation ----------
        public void ShowDestroyConfirm(World.DestroyPlan plan)
        {
            if (plan.Targets.Count == 0 && plan.Skipped == 0) return;
            pendingPlan = plan;
            confirm.Clear();
            confirm.style.display = DisplayStyle.Flex;
            int n = plan.Targets.Count;
            Text(Div(confirm, "head"), n > 0 ? $"Destroy {n} block{(n > 1 ? "s" : "")}?" : "Nothing can be destroyed");
            foreach (var kv in plan.Counts) Row(confirm, NameOf(kv.Key), kv.Value.ToString());
            if (plan.Skipped > 0) Text(confirm, $"{plan.Skipped} controller{(plan.Skipped > 1 ? "s" : "")} skipped: not empty", "warn");
            if (plan.Lost.Count > 0)
                Text(confirm, "Inventory full. These will be lost: " + string.Join(", ", plan.Lost.Select(kv => $"{kv.Value} {NameOf(kv.Key)}")), "warn");
            var ents = plan.Targets.Select(id => W.Get(id)).Where(x => x != null).ToList();
            if (ents.Any(x => (x is PipeEntity p && W.PipeAmount(p) > 0) || (x is MachineEntity m && (m.Fuel > 0 || m.FluidIn.Count > 0 || m.FluidOut.Count > 0)) || (x is CoreEntity c && c.Crafting)))
                Text(confirm, "Fluids, fuel and in-progress multiblock crafts inside are lost.", "muted").style.whiteSpace = WhiteSpace.Normal;
            var acts = Div(confirm, "acts");
            Btn(acts, "Cancel", CloseConfirm);
            if (n > 0)
                Btn(acts, "Destroy", () =>
                {
                    W.ExecuteDestroy(pendingPlan);
                    CloseConfirm();
                    ClosePopup();
                    Toast($"Destroyed {n} block{(n > 1 ? "s" : "")}");
                }, "danger");
        }

        public void CloseConfirm()
        {
            pendingPlan = null;
            confirm.style.display = DisplayStyle.None;
        }
    }
}
