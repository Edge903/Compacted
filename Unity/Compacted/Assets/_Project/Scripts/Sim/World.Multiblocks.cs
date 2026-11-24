using System;
using System.Collections.Generic;
using System.Linq;

namespace Compacted.Sim
{
    // Rule-based multiblocks: cores, structural parts and hatches. See requirements §5.5.
    public sealed partial class World
    {
        /// <summary>The structure around this core if it's valid, else null. Forms in any rotation, never mirrored.</summary>
        public Formation CheckFormation(CoreEntity core)
        {
            var mb = C.Multiblocks[core.Def.Multiblock];
            var dim = Dims[core.Dim];
            var shapes = mb.Width == mb.Height
                ? new[] { (mb.Width, mb.Height) }
                : new[] { (mb.Width, mb.Height), (mb.Height, mb.Width) };
            foreach (var (w, h) in shapes)
            for (int oy = core.Y - h + 1; oy <= core.Y; oy++)
            for (int ox = core.X - w + 1; ox <= core.X; ox++)
            {
                if (ox < 0 || oy < 0 || ox + w > dim.Size || oy + h > dim.Size) continue;
                var tiles = new List<Entity>();
                bool ok = true;
                for (int y = oy; y < oy + h && ok; y++)
                for (int x = ox; x < ox + w; x++)
                {
                    var t = At(core.Dim, x, y);
                    if (t == null || !mb.Counts.ContainsKey(t.Type) || (t is CoreEntity && t.Id != core.Id)) { ok = false; break; }
                    tiles.Add(t);
                }
                if (!ok) continue;
                var count = tiles.GroupBy(t => t.Type).ToDictionary(g => g.Key, g => g.Count());
                if (!mb.Counts.All(kv => { int n = count.TryGetValue(kv.Key, out var c) ? c : 0; return n >= kv.Value.Min && n <= kv.Value.Max; })) continue;
                bool Touches(Entity a, Entity b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
                if (!mb.Adjacent.All(p => tiles.Any(a => a.Type == p.A && tiles.Any(b => b.Type == p.B && Touches(a, b))))) continue;
                return new Formation
                {
                    Hatches = tiles.Where(t => t is HatchEntity).Select(t => t.Id).ToList(),
                    Parts = tiles.Select(t => t.Id).ToList(),
                    X = ox, Y = oy, W = w, H = h,
                };
            }
            return null;
        }

        /// <summary>Input buses accept only through their facing side, and only what the formed core's recipe needs.</summary>
        bool AcceptHatch(HatchEntity h, string item, Dir from)
        {
            if (h.Def.Io != HatchIo.In || from != h.Dir) return false;
            var core = Get<CoreEntity>(h.Core);
            if (core?.Formed == null || !core.Formed.Hatches.Contains(h.Id) || core.Recipe == null) return false;
            int cap = InputCap(C.Recipes[core.Recipe], item);
            int have = Get(h.Buf, item);
            if (cap == 0 || have >= cap) return false;
            h.Buf[item] = have + 1;
            return true;
        }

        void StepCore(CoreEntity c)
        {
            var st = CheckFormation(c);
            c.Formed = st;
            if (st == null)
            {
                if (c.Crafting) { c.Crafting = false; c.Prog = 0; }   // structure broken: consumed materials are lost
                c.Status = MachineStatus.NotFormed;
                return;
            }
            var hatches = st.Hatches.Select(id => Get<HatchEntity>(id)).ToList();
            foreach (var hatch in hatches) hatch.Core = c.Id;
            if (c.Recipe == null) { c.Status = MachineStatus.NoRecipe; return; }
            var r = C.Recipes[c.Recipe];
            var ins = hatches.Where(x => x.Def.Io == HatchIo.In).ToList();
            var outs = hatches.Where(x => x.Def.Io == HatchIo.Out).ToList();
            if (!c.Crafting)
            {
                if (!r.In.All(kv => ins.Sum(x => Get(x.Buf, kv.Key)) >= kv.Value)) { c.Status = MachineStatus.WaitingForInput; return; }
                foreach (var need in r.In)   // consumed from the input buses at craft start
                {
                    int left = need.Value;
                    foreach (var bus in ins)
                    {
                        int take = Math.Min(left, Get(bus.Buf, need.Key));
                        if (take == 0) continue;
                        bus.Buf[need.Key] -= take;
                        if (bus.Buf[need.Key] == 0) bus.Buf.Remove(need.Key);
                        left -= take;
                    }
                }
                c.Crafting = true; c.Prog = 0;
            }
            c.Prog = Math.Min(1, c.Prog + Dt / r.Time);
            if (c.Prog < 1) { c.Status = MachineStatus.Working; return; }
            int cap = C.Tuning.OutputCap;
            HatchEntity Room(string k, int n) => outs.FirstOrDefault(o => Get(o.Buf, k) + n <= Math.Max(cap, n));
            if (r.Out.Any(kv => Room(kv.Key, kv.Value) == null)) { c.Status = MachineStatus.OutputBlocked; return; }
            foreach (var made in r.Out) { var target = Room(made.Key, made.Value); target.Buf[made.Key] = Get(target.Buf, made.Key) + made.Value; }
            c.Crafting = false; c.Prog = 0; c.Status = MachineStatus.Working;
        }

        /// <summary>Output buses push one item per tick through their facing side.</summary>
        void StepHatch(HatchEntity h)
        {
            if (h.Def.Io != HatchIo.Out) return;
            foreach (var k in h.Buf.Keys.ToList())
            {
                if (h.Buf[k] > 0 && Push(h, h.Dir, k))
                {
                    if (--h.Buf[k] == 0) h.Buf.Remove(k);
                    break;
                }
            }
        }

        /// <summary>The formed core whose structure includes entity id, or null.</summary>
        public CoreEntity CoreOfPart(int id) =>
            order.OfType<CoreEntity>().FirstOrDefault(c => c.Formed != null && c.Formed.Parts.Contains(id));
    }
}
