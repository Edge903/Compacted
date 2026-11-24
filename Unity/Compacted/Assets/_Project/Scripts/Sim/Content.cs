using System.Collections.Generic;

namespace Compacted.Sim
{
    // Runtime definitions the simulation reads. Plain C#: the Data layer (ScriptableObjects)
    // builds a ContentSet from assets; tests build one in code. See docs/requirements.md §10.

    public enum BuildingKind { Belt, Pipe, Machine, Controller, Core, Part, Hatch }
    public enum Placement { Any, Wall }
    public enum HatchIo { None, In, Out }
    public enum RecipeType { Machine, Hand }

    public sealed class ItemDef
    {
        public string Id;
        public string Name;
        /// <summary>Fuel points when burned; 0 = not a fuel.</summary>
        public int Fuel;
        /// <summary>True if this item places a building.</summary>
        public bool IsBuilding;
    }

    public sealed class FluidDef
    {
        public string Id;
        public string Name;
    }

    public sealed class BuildingDef
    {
        public string Id;
        public string Name;
        public BuildingKind Kind;
        /// <summary>Placed without an inventory item and never refunded (belts, pipes).</summary>
        public bool Free;
        public Placement Placement = Placement.Any;
        /// <summary>Machines: which recipe group they run.</summary>
        public string MachineGroup;
        /// <summary>Machines: overrides Tuning.OutputCap when set.</summary>
        public int? OutputCap;
        /// <summary>Controllers: interior size (3, 5, … 13).</summary>
        public int ControllerSize;
        /// <summary>Cores: which multiblock they control.</summary>
        public string Multiblock;
        /// <summary>Hatches: input or output.</summary>
        public HatchIo Io;

        public bool Rotates => Kind == BuildingKind.Belt || Kind == BuildingKind.Machine || Kind == BuildingKind.Hatch;
    }

    public sealed class RecipeDef
    {
        public string Id;
        public RecipeType Type;
        /// <summary>Machine recipes: the machine group (or multiblock group) that runs it.</summary>
        public string Group;
        public Dictionary<string, int> In = new Dictionary<string, int>();
        /// <summary>Fluid inputs in ml.</summary>
        public Dictionary<string, double> FluidIn = new Dictionary<string, double>();
        /// <summary>Fuel points needed per craft (0 = none).</summary>
        public int Fuel;
        public Dictionary<string, int> Out = new Dictionary<string, int>();
        /// <summary>Fluid outputs in ml.</summary>
        public Dictionary<string, double> FluidOut = new Dictionary<string, double>();
        /// <summary>Seconds at speed 1. Hand recipes are instant.</summary>
        public double Time;
        /// <summary>Machine recipes that need a tech unlock.</summary>
        public bool Locked;
    }

    public sealed class MultiblockDef
    {
        public string Id;
        public string Name;
        public string Core;
        public string Group;
        public int Width;
        public int Height;
        /// <summary>Every tile of the W×H area must be one of these blocks, with a count in [Min, Max].</summary>
        public Dictionary<string, (int Min, int Max)> Counts = new Dictionary<string, (int, int)>();
        /// <summary>Each pair (A, B): some A must touch some B.</summary>
        public List<(string A, string B)> Adjacent = new List<(string, string)>();
    }

    public sealed class TechNodeDef
    {
        public string Id;
        public string Name;
        public int Col;
        public int Row;
        public List<string> Prereqs = new List<string>();
        /// <summary>Handed over all at once.</summary>
        public Dictionary<string, int> Cost = new Dictionary<string, int>();
        /// <summary>Checked only, never taken.</summary>
        public Dictionary<string, int> Requires = new Dictionary<string, int>();
        public Dictionary<string, int> Grant = new Dictionary<string, int>();
        public List<string> UnlockHand = new List<string>();
        public List<string> UnlockMachine = new List<string>();
    }

    public sealed class Tuning
    {
        public int TickRate = 20;
        public double BeltTime = 0.5;
        public int InputBufferMult = 3;
        public int OutputCap = 10;
        public int FuelBufferMult = 3;
        public double FluidBuffer = 10000;
        public double PipeCapacity = 1000;
        public double FluidPerTick = 100;
        public int InventoryCap = 100;
    }

    /// <summary>All definitions for one game, keyed by id. Order of tech nodes is kept for display.</summary>
    public sealed class ContentSet
    {
        public readonly Dictionary<string, ItemDef> Items = new Dictionary<string, ItemDef>();
        public readonly Dictionary<string, FluidDef> Fluids = new Dictionary<string, FluidDef>();
        public readonly Dictionary<string, BuildingDef> Buildings = new Dictionary<string, BuildingDef>();
        public readonly Dictionary<string, RecipeDef> Recipes = new Dictionary<string, RecipeDef>();
        public readonly Dictionary<string, MultiblockDef> Multiblocks = new Dictionary<string, MultiblockDef>();
        public readonly Dictionary<string, TechNodeDef> Tech = new Dictionary<string, TechNodeDef>();
        public readonly List<string> TechOrder = new List<string>();
        public readonly Dictionary<string, int> StartInventory = new Dictionary<string, int>();
        public Tuning Tuning = new Tuning();

        public ContentSet Add(ItemDef d) { Items[d.Id] = d; return this; }
        public ContentSet Add(FluidDef d) { Fluids[d.Id] = d; return this; }
        public ContentSet Add(BuildingDef d) { Buildings[d.Id] = d; return this; }
        public ContentSet Add(RecipeDef d) { Recipes[d.Id] = d; return this; }
        public ContentSet Add(MultiblockDef d) { Multiblocks[d.Id] = d; return this; }
        public ContentSet Add(TechNodeDef d) { if (!Tech.ContainsKey(d.Id)) TechOrder.Add(d.Id); Tech[d.Id] = d; return this; }

        public int FuelValue(string item) => item != null && Items.TryGetValue(item, out var d) ? d.Fuel : 0;
    }
}
