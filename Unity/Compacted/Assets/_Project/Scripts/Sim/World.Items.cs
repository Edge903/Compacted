using System;
using System.Collections.Generic;
using System.Linq;

namespace Compacted.Sim
{
    // Generic item I/O rule, belts, machines (items, fluids, fuel), controllers and hand transfer.
    public sealed partial class World
    {
        // ---------- recipes ----------
        public string GroupOf(Entity e) =>
            e is CoreEntity ? C.Multiblocks[e.Def.Multiblock].Group : e.Def.MachineGroup;

        /// <summary>Unlocked machine recipes this machine or core can run.</summary>
        public List<string> RecipesFor(Entity e)
        {
            var g = GroupOf(e);
            return C.Recipes.Values
                .Where(r => r.Type == RecipeType.Machine && r.Group == g && RecipeUnlocked(r.Id))
                .Select(r => r.Id).ToList();
        }

        public int InputCap(RecipeDef r, string item) =>
            r != null && r.In.TryGetValue(item, out var need) ? need * C.Tuning.InputBufferMult : 0;

        public int FuelCap(MachineEntity m)
        {
            var r = m.Recipe != null ? C.Recipes[m.Recipe] : null;
            return r != null && r.Fuel > 0 ? r.Fuel * C.Tuning.FuelBufferMult : 0;
        }

        public int OutputCapOf(Entity e) => e.Def.OutputCap ?? C.Tuning.OutputCap;

        /// <summary>
        /// Change a machine's or core's recipe. Machine item buffers (and an in-progress craft's inputs)
        /// go back to the inventory; fluid and fuel are cleared. Returns items lost to the cap.
        /// </summary>
        public int SetRecipe(Entity e, string recipeId)
        {
            int lost = 0;
            if (e is MachineEntity m)
            {
                var back = new Dictionary<string, int>(m.InBuf);
                if (m.Crafting) foreach (var kv in C.Recipes[m.Recipe].In) AddTo(back, kv.Key, kv.Value);
                foreach (var kv in back) lost += AddInv(kv.Key, kv.Value);
                m.InBuf.Clear(); m.FluidIn.Clear(); m.Fuel = 0;
                m.Crafting = false; m.Prog = 0;
                m.Recipe = string.IsNullOrEmpty(recipeId) ? null : recipeId;
            }
            else if (e is CoreEntity c)
            {
                c.Crafting = false; c.Prog = 0;
                c.Recipe = string.IsNullOrEmpty(recipeId) ? null : recipeId;
            }
            return lost;
        }

        // ---------- hand transfer ----------
        /// <summary>Insert by hand into a machine (ingredient or fuel) or an input bus, as much as fits.</summary>
        public int InsertFromInventory(Entity e, string item)
        {
            int n = 0;
            while (InvCount(item) > 0 && AcceptByHand(e, item)) { TakeInv(item, 1); n++; }
            return n;
        }

        bool AcceptByHand(Entity e, string item)
        {
            if (e is MachineEntity m) return AcceptMachine(m, item, null);
            if (e is HatchEntity h) return AcceptHatch(h, item, h.Dir);
            return false;
        }

        /// <summary>Take a machine's or output bus's items into the inventory. Returns how many moved.</summary>
        public int TakeOutput(Entity e)
        {
            Dictionary<string, int> buf = e is HatchEntity h ? h.Buf : e is MachineEntity m ? m.OutBuf : null;
            if (buf == null) return 0;
            int moved = 0;
            foreach (var k in buf.Keys.ToList())
            {
                int n = buf[k], left = AddInv(k, n);
                moved += n - left;
                if (left > 0) buf[k] = left; else buf.Remove(k);
            }
            return moved;
        }

        /// <summary>Set a port's mode. An item waiting in the port goes back to the inventory. Returns items lost.</summary>
        public int SetPortMode(ControllerEntity c, Dir d, PortMode mode)
        {
            int i = (int)d, lost = 0;
            if (c.PortBuf[i] != null) { lost = AddInv(c.PortBuf[i], 1); c.PortBuf[i] = null; }
            if (c.Ports[i] == PortMode.Fluid || mode == PortMode.Fluid) MarkSegmentsDirty();
            c.Ports[i] = mode;
            return lost;
        }

        // ---------- generic item I/O rule ----------
        /// <summary>Try to push one item out of e's side d. Returns true if it moved.</summary>
        public bool Push(Entity e, Dir d, string item)
        {
            var dim = Dims[e.Dim];
            var n = e.Pos.Step(d);
            if (!dim.InBounds(n.X, n.Y))
            {
                // Leaving the grid: only through the port on that wall.
                if (dim.IsRoot || dim.PortTile(d) != e.Pos) return false;
                var c = Get<ControllerEntity>(dim.ControllerId);
                int i = (int)d;
                if (c.Ports[i] != PortMode.Out || c.PortBuf[i] != null) return false;
                c.PortBuf[i] = item;
                return true;
            }
            var t = At(e.Dim, n);
            if (t == null) return false;                // empty tile: the output fails
            return Accept(t, item, d.Opposite());       // eligibility check
        }

        /// <summary>Can t take item arriving from side `from`? If so, take it.</summary>
        public bool Accept(Entity t, string item, Dir from)
        {
            switch (t)
            {
                case BeltEntity b:
                    if (from == b.Dir || b.Item != null) return false;
                    b.Item = item; b.From = from; b.Prog = 0;
                    return true;
                case MachineEntity m: return AcceptMachine(m, item, from);
                case HatchEntity h: return AcceptHatch(h, item, from);
                case ControllerEntity c:
                    int i = (int)from;
                    if (c.Ports[i] != PortMode.In || c.PortBuf[i] != null) return false;
                    c.PortBuf[i] = item;
                    return true;
            }
            return false;   // pipes, cores and parts take no items
        }

        /// <summary>from = null means by hand (any side).</summary>
        bool AcceptMachine(MachineEntity m, string item, Dir? from)
        {
            if (from == m.Dir || m.Recipe == null) return false;
            var r = C.Recipes[m.Recipe];
            int cap = InputCap(r, item);
            if (cap > 0)
            {
                int have = m.InBuf.TryGetValue(item, out var v) ? v : 0;
                if (have >= cap) return false;
                m.InBuf[item] = have + 1;
                return true;
            }
            int fuel = C.FuelValue(item);
            if (r.Fuel > 0 && fuel > 0)
            {
                if (m.Fuel + fuel > FuelCap(m)) return false;   // would overflow the fuel buffer: refuse
                m.Fuel += fuel;                                  // converted to points on input
                return true;
            }
            return false;
        }

        // ---------- belts ----------
        void StepBelt(BeltEntity b)
        {
            if (b.Item != null)
            {
                b.Prog = Math.Min(1, b.Prog + Dt / C.Tuning.BeltTime);
                if (b.Prog >= 1 && Push(b, b.Dir, b.Item)) { b.Item = null; b.Hist.Add(Tick); }
            }
            long window = C.Tuning.TickRate * 5;
            b.Hist.RemoveAll(t => t < Tick - window);
        }

        /// <summary>Items per second leaving this belt over the last 5 seconds.</summary>
        public double BeltRate(BeltEntity b)
        {
            double secs = Math.Min(5.0, (double)Tick / C.Tuning.TickRate);
            return secs > 0 ? b.Hist.Count / secs : 0;
        }

        // ---------- machines ----------
        void StepMachine(MachineEntity m)
        {
            // Items out: at most one per tick through the output side.
            foreach (var k in m.OutBuf.Keys.ToList())
            {
                if (m.OutBuf[k] > 0 && Push(m, m.Dir, k))
                {
                    if (--m.OutBuf[k] == 0) m.OutBuf.Remove(k);
                    break;
                }
            }
            // Fluids out: through the output side, into a pipe segment or straight into a machine.
            foreach (var f in m.FluidOut.Keys.ToList())
            {
                double a = m.FluidOut[f];
                if (a <= 0) { m.FluidOut.Remove(f); continue; }
                var t = FluidNeighbor(m, m.Dir);
                double moved = 0;
                if (t is PipeEntity p) moved = PushToSegment(SegmentOf(p), f, a);
                else if (t is MachineEntity tm) moved = AcceptFluid(tm, f, Math.Min(a, C.Tuning.FluidPerTick));
                a -= moved;
                if (a <= 1e-9) m.FluidOut.Remove(f); else m.FluidOut[f] = a;
            }
            if (m.Recipe == null) { m.Status = MachineStatus.NoRecipe; return; }
            var r = C.Recipes[m.Recipe];
            // Fluids in: pull from each adjacent segment once per tick (never through the output side).
            if (r.FluidIn.Count > 0)
            {
                var seen = new HashSet<Segment>();
                for (int i = 0; i < 4; i++)
                {
                    var d = (Dir)i;
                    if (d == m.Dir) continue;
                    if (!(FluidNeighbor(m, d) is PipeEntity p)) continue;
                    var s = SegmentOf(p);
                    if (s == null || !seen.Add(s)) continue;
                    if (s.Fluid == null || !r.FluidIn.ContainsKey(s.Fluid)) continue;
                    string f = s.Fluid;
                    double space = C.Tuning.FluidBuffer - Get(m.FluidIn, f);
                    double n = PullFromSegment(s, space);
                    if (n > 0) m.FluidIn[f] = Get(m.FluidIn, f) + n;
                }
            }
            if (!m.Crafting)
            {
                bool itemsReady = r.In.All(kv => Get(m.InBuf, kv.Key) >= kv.Value);
                bool fluidsReady = r.FluidIn.All(kv => Get(m.FluidIn, kv.Key) >= kv.Value - 1e-6);
                bool fuelReady = r.Fuel == 0 || m.Fuel >= r.Fuel;
                if (!(itemsReady && fluidsReady && fuelReady))
                {
                    m.Status = itemsReady && fluidsReady && !fuelReady ? MachineStatus.NeedsFuel : MachineStatus.WaitingForInput;
                    return;
                }
                foreach (var kv in r.In) { m.InBuf[kv.Key] -= kv.Value; if (m.InBuf[kv.Key] == 0) m.InBuf.Remove(kv.Key); }
                foreach (var kv in r.FluidIn)
                {
                    double left = Math.Max(0, m.FluidIn[kv.Key] - kv.Value);
                    if (left < 1e-6) m.FluidIn.Remove(kv.Key); else m.FluidIn[kv.Key] = left;
                }
                m.Fuel -= r.Fuel;
                m.Crafting = true; m.Prog = 0;
            }
            m.Prog = Math.Min(1, m.Prog + Dt / r.Time);
            if (m.Prog < 1) { m.Status = MachineStatus.Working; return; }
            int cap = OutputCapOf(m);
            bool fits = r.Out.All(kv => Get(m.OutBuf, kv.Key) + kv.Value <= Math.Max(cap, kv.Value))
                     && r.FluidOut.All(kv => Get(m.FluidOut, kv.Key) + kv.Value <= C.Tuning.FluidBuffer);
            if (!fits) { m.Status = MachineStatus.OutputBlocked; return; }
            foreach (var kv in r.Out) m.OutBuf[kv.Key] = Get(m.OutBuf, kv.Key) + kv.Value;
            foreach (var kv in r.FluidOut) m.FluidOut[kv.Key] = Get(m.FluidOut, kv.Key) + kv.Value;
            m.Crafting = false; m.Prog = 0; m.Status = MachineStatus.Working;
        }

        /// <summary>Direct machine-to-machine fluid transfer. Returns ml accepted.</summary>
        double AcceptFluid(MachineEntity t, string f, double amount)
        {
            if (t.Recipe == null) return 0;
            var r = C.Recipes[t.Recipe];
            if (!r.FluidIn.ContainsKey(f)) return 0;
            double n = Math.Min(amount, C.Tuning.FluidBuffer - Get(t.FluidIn, f));
            if (n <= 0) return 0;
            t.FluidIn[f] = Get(t.FluidIn, f) + n;
            return n;
        }

        // ---------- controllers ----------
        void StepController(ControllerEntity c)
        {
            var inner = Dims[c.Inner];
            for (int i = 0; i < 4; i++)
            {
                var item = c.PortBuf[i];
                if (item == null) continue;
                var d = (Dir)i;
                if (c.Ports[i] == PortMode.In)
                {
                    // Outside -> inside: to the interior tile next to wall d, arriving from side d.
                    var t = At(c.Inner, inner.PortTile(d));
                    if (t != null && Accept(t, item, d)) c.PortBuf[i] = null;
                }
                else if (c.Ports[i] == PortMode.Out)
                {
                    // Inside -> outside: out of the controller's side d in the parent dimension.
                    if (Push(c, d, item)) c.PortBuf[i] = null;
                }
            }
        }

        static int Get(Dictionary<string, int> m, string k) => m.TryGetValue(k, out var v) ? v : 0;
        static double Get(Dictionary<string, double> m, string k) => m.TryGetValue(k, out var v) ? v : 0;
    }
}
