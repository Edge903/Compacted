using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Compacted.Sim
{
    /// <summary>Plain, versioned snapshot of a World. Definitions are referenced by id only.</summary>
    public sealed class WorldSave
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int NextId;
        public long Tick;
        public int RootId;
        public int ControllerCount;
        public bool FreeBuild;
        public Dictionary<string, int> Inventory = new Dictionary<string, int>();
        public List<string> TechDone = new List<string>();
        public List<string> UnlockedHand = new List<string>();
        public List<string> UnlockedMachine = new List<string>();
        public List<DimSave> Dims = new List<DimSave>();
        /// <summary>In tick order.</summary>
        public List<EntitySave> Entities = new List<EntitySave>();
    }

    public sealed class DimSave
    {
        public int Id, Size, ControllerId;
        public int[] Grid;
    }

    /// <summary>One flat record for every kind; fields a kind doesn't use stay null.</summary>
    public sealed class EntitySave
    {
        public int Id;
        public string Type;
        public int Dim, X, Y;
        public Dir Dir;
        // belt
        public string Item; public Dir? From; public double? Prog; public List<long> Hist;
        // pipe
        public string Fluid; public double? Amount;
        // machine / core
        public string Recipe; public bool? Crafting; public MachineStatus? Status;
        public Dictionary<string, int> InBuf, OutBuf;
        public Dictionary<string, double> FluidIn, FluidOut;
        public int? Fuel;
        // controller
        public string Name; public PortMode[] Ports; public string[] PortBuf; public int? Inner;
        // hatch
        public Dictionary<string, int> Buf; public int? Core;
    }

    public sealed partial class World
    {
        static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter() },
            Formatting = Formatting.None,
        };

        public string ToJson() => JsonConvert.SerializeObject(ToSave(), JsonSettings);

        public static World FromJson(ContentSet content, string json) =>
            FromSave(content, JsonConvert.DeserializeObject<WorldSave>(json, JsonSettings));

        public WorldSave ToSave()
        {
            FlushSegments();
            var s = new WorldSave
            {
                NextId = NextId, Tick = Tick, RootId = RootId, ControllerCount = ControllerCount, FreeBuild = FreeBuild,
                Inventory = new Dictionary<string, int>(Inventory),
                TechDone = new List<string>(TechDone),
                UnlockedHand = new List<string>(UnlockedHand),
                UnlockedMachine = new List<string>(UnlockedMachine),
                Dims = Dims.Values.Select(d => new DimSave { Id = d.Id, Size = d.Size, ControllerId = d.ControllerId, Grid = (int[])d.Grid.Clone() }).ToList(),
            };
            foreach (var e in order)
            {
                var r = new EntitySave { Id = e.Id, Type = e.Type, Dim = e.Dim, X = e.X, Y = e.Y, Dir = e.Dir };
                switch (e)
                {
                    case BeltEntity b: r.Item = b.Item; r.From = b.From; r.Prog = b.Prog; r.Hist = new List<long>(b.Hist); break;
                    case PipeEntity p: r.Fluid = p.Fluid; r.Amount = p.Amount; break;
                    case MachineEntity m:
                        r.Recipe = m.Recipe; r.Crafting = m.Crafting; r.Prog = m.Prog; r.Status = m.Status; r.Fuel = m.Fuel;
                        r.InBuf = new Dictionary<string, int>(m.InBuf); r.OutBuf = new Dictionary<string, int>(m.OutBuf);
                        r.FluidIn = new Dictionary<string, double>(m.FluidIn); r.FluidOut = new Dictionary<string, double>(m.FluidOut);
                        break;
                    case CoreEntity c: r.Recipe = c.Recipe; r.Crafting = c.Crafting; r.Prog = c.Prog; r.Status = c.Status; break;
                    case ControllerEntity c:
                        r.Name = c.Name; r.Ports = (PortMode[])c.Ports.Clone(); r.PortBuf = (string[])c.PortBuf.Clone(); r.Inner = c.Inner;
                        break;
                    case HatchEntity h: r.Buf = new Dictionary<string, int>(h.Buf); r.Core = h.Core; break;
                }
                s.Entities.Add(r);
            }
            return s;
        }

        public static World FromSave(ContentSet content, WorldSave s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.Version != WorldSave.CurrentVersion)
                throw new NotSupportedException($"Save format version {s.Version} isn't supported (expected {WorldSave.CurrentVersion}).");
            var w = new World(content);
            w.Dims.Clear();
            w.Inventory.Clear();
            w.NextId = s.NextId; w.Tick = s.Tick; w.RootId = s.RootId; w.ControllerCount = s.ControllerCount; w.FreeBuild = s.FreeBuild;
            foreach (var kv in s.Inventory) w.Inventory[kv.Key] = kv.Value;
            w.TechDone.AddRange(s.TechDone);
            w.UnlockedHand.AddRange(s.UnlockedHand);
            w.UnlockedMachine.AddRange(s.UnlockedMachine);
            foreach (var d in s.Dims) w.Dims[d.Id] = new Dimension { Id = d.Id, Size = d.Size, ControllerId = d.ControllerId, Grid = (int[])d.Grid.Clone() };
            foreach (var r in s.Entities)
            {
                if (!content.Buildings.TryGetValue(r.Type, out var def))
                    throw new InvalidOperationException($"Save refers to unknown building '{r.Type}'.");
                Entity e;
                switch (def.Kind)
                {
                    case BuildingKind.Belt:
                        e = new BeltEntity { Item = r.Item, From = r.From ?? r.Dir.Opposite(), Prog = r.Prog ?? 0, Hist = r.Hist ?? new List<long>() };
                        break;
                    case BuildingKind.Pipe: e = new PipeEntity { Fluid = r.Fluid, Amount = r.Amount ?? 0 }; break;
                    case BuildingKind.Machine:
                        e = new MachineEntity
                        {
                            Recipe = r.Recipe, Crafting = r.Crafting ?? false, Prog = r.Prog ?? 0, Status = r.Status ?? MachineStatus.NoRecipe, Fuel = r.Fuel ?? 0,
                            InBuf = r.InBuf ?? new Dictionary<string, int>(), OutBuf = r.OutBuf ?? new Dictionary<string, int>(),
                            FluidIn = r.FluidIn ?? new Dictionary<string, double>(), FluidOut = r.FluidOut ?? new Dictionary<string, double>(),
                        };
                        break;
                    case BuildingKind.Core:
                        e = new CoreEntity { Recipe = r.Recipe, Crafting = r.Crafting ?? false, Prog = r.Prog ?? 0, Status = r.Status ?? MachineStatus.NotFormed };
                        break;
                    case BuildingKind.Controller:
                        e = new ControllerEntity { Name = r.Name, Ports = r.Ports ?? new PortMode[4], PortBuf = r.PortBuf ?? new string[4], Inner = r.Inner ?? 0 };
                        break;
                    case BuildingKind.Hatch: e = new HatchEntity { Buf = r.Buf ?? new Dictionary<string, int>(), Core = r.Core ?? 0 }; break;
                    default: e = new PartEntity(); break;
                }
                e.Id = r.Id; e.Type = r.Type; e.Def = def; e.Dim = r.Dim; e.X = r.X; e.Y = r.Y; e.Dir = r.Dir;
                w.ents[e.Id] = e;
                w.order.Add(e);
            }
            w.segments.Clear();
            w.segmentsDirty = true;
            return w;
        }
    }
}
