using System;
using System.Collections.Generic;

namespace Compacted.Sim
{
    // Pipe segments (Fluid 2.0 style): connected pipes act as one tank. See requirements §5.3.
    public sealed partial class World
    {
        readonly List<Segment> segments = new List<Segment>();
        readonly Dictionary<int, Segment> segmentOf = new Dictionary<int, Segment>();
        bool segmentsDirty = true;

        public IReadOnlyList<Segment> Segments { get { if (segmentsDirty) RebuildSegments(); return segments; } }

        public Segment SegmentOf(PipeEntity p)
        {
            if (segmentsDirty) RebuildSegments();
            return segmentOf.TryGetValue(p.Id, out var s) ? s : null;
        }

        /// <summary>The ml shown in one pipe: its even share of the segment.</summary>
        public double PipeAmount(PipeEntity p)
        {
            var s = SegmentOf(p);
            return s != null && s.Pipes.Count > 0 ? s.Amount / s.Pipes.Count : 0;
        }

        void MarkSegmentsDirty() { FlushSegments(); segmentsDirty = true; }

        /// <summary>Write each segment's amount back into its pipes so it survives a rebuild or a save.</summary>
        void FlushSegments()
        {
            foreach (var s in segments)
            {
                double share = s.Pipes.Count > 0 ? s.Amount / s.Pipes.Count : 0;
                foreach (var id in s.Pipes)
                {
                    if (Get(id) is PipeEntity p) { p.Fluid = s.Amount > 0 ? s.Fluid : null; p.Amount = share; }
                }
            }
        }

        /// <summary>
        /// The entity reached by stepping from e in direction d, crossing Fluid I/O ports both ways
        /// (out through a wall into the parent, and in through a controller's wall, nested any depth).
        /// </summary>
        public Entity FluidNeighbor(Entity e, Dir d) => FluidNeighborAt(e.Dim, e.X, e.Y, d);

        Entity FluidNeighborAt(int dimId, int x, int y, Dir d)
        {
            var dim = Dims[dimId];
            var n = new Cell(x, y).Step(d);
            Entity t;
            if (dim.InBounds(n.X, n.Y)) t = At(dimId, n);
            else
            {
                if (dim.IsRoot || dim.PortTile(d) != new Cell(x, y)) return null;
                var c = Get<ControllerEntity>(dim.ControllerId);
                if (c.Ports[(int)d] != PortMode.Fluid) return null;
                return FluidNeighborAt(c.Dim, c.X, c.Y, d);
            }
            var back = d.Opposite();
            while (t is ControllerEntity tc && tc.Ports[(int)back] == PortMode.Fluid)
                t = At(tc.Inner, Dims[tc.Inner].PortTile(back));
            return t;
        }

        void RebuildSegments()
        {
            FlushSegments();
            segments.Clear();
            segmentOf.Clear();
            foreach (var e in order)
            {
                if (!(e is PipeEntity start) || segmentOf.ContainsKey(start.Id)) continue;
                var seg = new Segment();
                var byFluid = new Dictionary<string, double>();
                var stack = new Stack<PipeEntity>();
                stack.Push(start);
                segmentOf[start.Id] = seg;
                while (stack.Count > 0)
                {
                    var q = stack.Pop();
                    seg.Pipes.Add(q.Id);
                    if (q.Fluid != null && q.Amount > 0)
                        byFluid[q.Fluid] = (byFluid.TryGetValue(q.Fluid, out var a) ? a : 0) + q.Amount;
                    for (int i = 0; i < 4; i++)
                    {
                        if (FluidNeighbor(q, (Dir)i) is PipeEntity n && !segmentOf.ContainsKey(n.Id))
                        {
                            segmentOf[n.Id] = seg;
                            stack.Push(n);
                        }
                    }
                }
                // One fluid per segment: if segments with different fluids merge, the larger amount wins.
                foreach (var kv in byFluid)
                    if (kv.Value > seg.Amount) { seg.Fluid = kv.Key; seg.Amount = kv.Value; }
                seg.Capacity = seg.Pipes.Count * C.Tuning.PipeCapacity;
                seg.Amount = Math.Min(seg.Amount, seg.Capacity);
                segments.Add(seg);
            }
            segmentsDirty = false;
        }

        /// <summary>Push into a segment: up to FluidPerTick, limited only by the room left.</summary>
        public double PushToSegment(Segment s, string fluid, double amount)
        {
            if (s == null) return 0;
            if (s.Amount > 0 && s.Fluid != fluid) return 0;
            double n = Math.Min(Math.Min(amount, C.Tuning.FluidPerTick), s.Capacity - s.Amount);
            if (n <= 0) return 0;
            s.Fluid = fluid;
            s.Amount += n;
            return n;
        }

        /// <summary>Pull from a segment: FluidPerTick × fill fraction; under 1% full, take everything left.</summary>
        public double PullFromSegment(Segment s, double space)
        {
            if (s == null || s.Amount <= 0) return 0;
            double want = s.Fill < 0.01 ? s.Amount : C.Tuning.FluidPerTick * s.Fill;
            double n = Math.Min(Math.Min(want, space), s.Amount);
            if (n <= 0) return 0;
            s.Amount -= n;
            if (s.Amount < 1e-6) { s.Amount = 0; s.Fluid = null; }
            return n;
        }
    }
}
