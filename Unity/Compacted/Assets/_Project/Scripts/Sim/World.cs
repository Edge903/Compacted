using System;
using System.Collections.Generic;
using System.Linq;

namespace Compacted.Sim
{
    public readonly struct Result
    {
        public readonly bool Ok;
        public readonly string Reason;
        public Result(bool ok, string reason) { Ok = ok; Reason = reason; }
        public static readonly Result Success = new Result(true, null);
        public static Result Fail(string reason) => new Result(false, reason);
    }

    /// <summary>
    /// The whole game state and its rules. Pure logic with a fixed timestep (Tuning.TickRate).
    /// Ported from prototype/sim.js; split into partial files by system.
    /// </summary>
    public sealed partial class World
    {
        public readonly ContentSet C;
        public readonly double Dt;
        public int NextId = 1;
        public long Tick;
        public int RootId;
        public int ControllerCount;
        public bool FreeBuild;

        public readonly Dictionary<int, Dimension> Dims = new Dictionary<int, Dimension>();
        readonly Dictionary<int, Entity> ents = new Dictionary<int, Entity>();
        /// <summary>Entities in creation order: this is the tick order.</summary>
        readonly List<Entity> order = new List<Entity>();
        public readonly Dictionary<string, int> Inventory = new Dictionary<string, int>();

        public World(ContentSet content)
        {
            C = content;
            Dt = 1.0 / content.Tuning.TickRate;
            foreach (var kv in content.StartInventory) Inventory[kv.Key] = kv.Value;
            RootId = CreateDim(3, 0);
        }

        public IReadOnlyList<Entity> Entities => order;
        public Entity Get(int id) => id != 0 && ents.TryGetValue(id, out var e) ? e : null;
        public T Get<T>(int id) where T : Entity => Get(id) as T;

        // ---------- dimensions ----------
        int CreateDim(int size, int controllerId)
        {
            int id = NextId++;
            Dims[id] = new Dimension { Id = id, Size = size, ControllerId = controllerId, Grid = new int[size * size] };
            return id;
        }

        /// <summary>The entity at a tile, or null if the tile is empty or out of bounds.</summary>
        public Entity At(int dimId, int x, int y)
        {
            var dim = Dims[dimId];
            if (!dim.InBounds(x, y)) return null;
            return Get(dim.Grid[y * dim.Size + x]);
        }
        public Entity At(int dimId, Cell c) => At(dimId, c.X, c.Y);

        public bool IsEmptyDim(int dimId) => Dims[dimId].Grid.All(v => v == 0);
        public int CountBlocks(int dimId) => Dims[dimId].Grid.Count(v => v != 0);

        /// <summary>The dimension that holds the controller owning dimId; -1 for the root.</summary>
        public int ParentDimOf(int dimId)
        {
            var dim = Dims[dimId];
            return dim.IsRoot ? -1 : Get(dim.ControllerId).Dim;
        }

        // ---------- inventory ----------
        public int InvCount(string item) => item != null && Inventory.TryGetValue(item, out var n) ? n : 0;

        /// <summary>Adds up to the cap. Returns the amount that didn't fit (lost).</summary>
        public int AddInv(string item, int n)
        {
            int have = InvCount(item);
            int add = Math.Max(0, Math.Min(n, C.Tuning.InventoryCap - have));
            if (add > 0) Inventory[item] = have + add;
            return n - add;
        }

        public int TakeInv(string item, int n)
        {
            int take = Math.Min(n, InvCount(item));
            int left = InvCount(item) - take;
            if (left > 0) Inventory[item] = left; else Inventory.Remove(item);
            return take;
        }

        public bool HasAll(Dictionary<string, int> map) => map == null || map.All(kv => InvCount(kv.Key) >= kv.Value);

        // ---------- placement ----------
        public Result CanPlace(string type, int dimId, int x, int y)
        {
            var b = C.Buildings[type];
            var dim = Dims[dimId];
            if (!dim.InBounds(x, y)) return Result.Fail("Outside the dimension");
            if (At(dimId, x, y) != null) return Result.Fail("Tile is occupied");
            if (b.Placement == Placement.Wall && !dim.TouchesWall(x, y)) return Result.Fail($"{b.Name} must touch a wall");
            if (b.Kind == BuildingKind.Controller && !dim.IsRoot && !(dim.Size > b.ControllerSize))
                return Result.Fail($"A {b.ControllerSize}×{b.ControllerSize} controller only fits in a bigger dimension");
            if (!b.Free && !FreeBuild && InvCount(type) < 1) return Result.Fail($"No {b.Name} in inventory");
            return Result.Success;
        }

        public Result Place(string type, int dimId, int x, int y, Dir dir, out Entity placed)
        {
            placed = null;
            var chk = CanPlace(type, dimId, x, y);
            if (!chk.Ok) return chk;
            var b = C.Buildings[type];
            if (!b.Free && !FreeBuild) TakeInv(type, 1);

            Entity e;
            switch (b.Kind)
            {
                case BuildingKind.Belt: e = new BeltEntity { From = dir.Opposite() }; break;
                case BuildingKind.Pipe: e = new PipeEntity(); break;
                case BuildingKind.Machine: e = new MachineEntity(); break;
                case BuildingKind.Controller:
                    ControllerCount++;
                    e = new ControllerEntity { Name = $"Box {ControllerCount}" };
                    break;
                case BuildingKind.Core: e = new CoreEntity(); break;
                case BuildingKind.Part: e = new PartEntity(); break;
                case BuildingKind.Hatch: e = new HatchEntity(); break;
                default: throw new ArgumentOutOfRangeException();
            }
            e.Id = NextId++;
            e.Type = type;
            e.Def = b;
            e.Dim = dimId;
            e.X = x;
            e.Y = y;
            e.Dir = b.Rotates ? dir : Dir.N;
            ents[e.Id] = e;
            order.Add(e);
            var dim = Dims[dimId];
            dim.Grid[y * dim.Size + x] = e.Id;

            if (e is ControllerEntity c) c.Inner = CreateDim(b.ControllerSize, c.Id);
            // Single-recipe machines (sources) and cores start with that recipe selected.
            if (e is MachineEntity m) { var rs = RecipesFor(m); if (rs.Count == 1) m.Recipe = rs[0]; }
            if (e is CoreEntity core) { var rs = RecipesFor(core); if (rs.Count == 1) core.Recipe = rs[0]; }

            MarkSegmentsDirty();
            placed = e;
            return Result.Success;
        }

        public Result Place(string type, int dimId, int x, int y, Dir dir = Dir.E) => Place(type, dimId, x, y, dir, out _);

        // ---------- destroy ----------
        public sealed class DestroyPlan
        {
            public int DimId;
            public readonly List<int> Targets = new List<int>();
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Refunds = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Lost = new Dictionary<string, int>();
            public int Skipped;
        }

        static void AddTo(Dictionary<string, int> map, string k, int n)
        {
            if (n <= 0) return;
            map[k] = (map.TryGetValue(k, out var v) ? v : 0) + n;
        }

        /// <summary>Items refunded when e is destroyed. Fluids, fuel points and a core's in-progress craft are lost.</summary>
        public Dictionary<string, int> ContentsOf(Entity e)
        {
            var out_ = new Dictionary<string, int>();
            switch (e)
            {
                case BeltEntity b: if (b.Item != null) AddTo(out_, b.Item, 1); break;
                case MachineEntity m:
                    foreach (var kv in m.InBuf) AddTo(out_, kv.Key, kv.Value);
                    foreach (var kv in m.OutBuf) AddTo(out_, kv.Key, kv.Value);
                    if (m.Crafting) foreach (var kv in C.Recipes[m.Recipe].In) AddTo(out_, kv.Key, kv.Value);
                    break;
                case HatchEntity h: foreach (var kv in h.Buf) AddTo(out_, kv.Key, kv.Value); break;
                case ControllerEntity c: foreach (var it in c.PortBuf) if (it != null) AddTo(out_, it, 1); break;
            }
            return out_;
        }

        public DestroyPlan PlanDestroy(int dimId, int x0, int y0, int x1, int y1)
        {
            var plan = new DestroyPlan { DimId = dimId };
            int ax = Math.Min(x0, x1), bx = Math.Max(x0, x1), ay = Math.Min(y0, y1), by = Math.Max(y0, y1);
            for (int y = ay; y <= by; y++)
            for (int x = ax; x <= bx; x++)
            {
                var e = At(dimId, x, y);
                if (e == null) continue;
                if (e is ControllerEntity c && !IsEmptyDim(c.Inner)) { plan.Skipped++; continue; }
                plan.Targets.Add(e.Id);
                AddTo(plan.Counts, e.Type, 1);
                if (!e.Def.Free) AddTo(plan.Refunds, e.Type, 1);
                foreach (var kv in ContentsOf(e)) AddTo(plan.Refunds, kv.Key, kv.Value);
            }
            foreach (var kv in plan.Refunds)
            {
                int space = Math.Max(0, C.Tuning.InventoryCap - InvCount(kv.Key));
                if (kv.Value > space) plan.Lost[kv.Key] = kv.Value - space;
            }
            return plan;
        }

        public void ExecuteDestroy(DestroyPlan plan)
        {
            FlushSegments();   // keep fluid in the pipes that survive
            foreach (var id in plan.Targets)
            {
                var e = Get(id);
                if (e == null) continue;
                var dim = Dims[e.Dim];
                dim.Grid[e.Y * dim.Size + e.X] = 0;
                ents.Remove(id);
                order.Remove(e);
                if (e is ControllerEntity c) Dims.Remove(c.Inner);
            }
            foreach (var kv in plan.Refunds) AddInv(kv.Key, kv.Value);
            MarkSegmentsDirty();
        }

        // ---------- simulation ----------
        public void Step()
        {
            if (segmentsDirty) RebuildSegments();
            Tick++;
            // Snapshot the order so a step never sees entities added or removed mid-tick.
            foreach (var e in order.ToArray())
            {
                switch (e)
                {
                    case BeltEntity b: StepBelt(b); break;
                    case MachineEntity m: StepMachine(m); break;
                    case ControllerEntity c: StepController(c); break;
                    case CoreEntity core: StepCore(core); break;
                    case HatchEntity h: StepHatch(h); break;
                }
            }
        }

        public void Run(double seconds)
        {
            int ticks = (int)Math.Round(seconds * C.Tuning.TickRate);
            for (int i = 0; i < ticks; i++) Step();
        }
    }
}
