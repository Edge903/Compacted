using System.Collections.Generic;

namespace Compacted.Sim
{
    public enum PortMode { Off, In, Out, Fluid }

    public enum MachineStatus { NoRecipe, WaitingForInput, NeedsFuel, Working, OutputBlocked, NotFormed }

    /// <summary>A placed block: a reference to its BuildingDef plus its own state.</summary>
    public abstract class Entity
    {
        public int Id;
        public string Type;
        public int Dim;
        public int X;
        public int Y;
        /// <summary>Output side (belts, machines) or facing (hatches). Always N for other kinds.</summary>
        public Dir Dir;
        /// <summary>Resolved from Type; not saved.</summary>
        public BuildingDef Def;

        public BuildingKind Kind => Def.Kind;
        public Cell Pos => new Cell(X, Y);
    }

    public sealed class BeltEntity : Entity
    {
        public string Item;
        public Dir From;
        public double Prog;
        /// <summary>Ticks at which an item left this belt, for throughput stats (last 5 s).</summary>
        public List<long> Hist = new List<long>();
    }

    public sealed class PipeEntity : Entity
    {
        /// <summary>This pipe's share of its segment, written back when segments rebuild.</summary>
        public string Fluid;
        public double Amount;
    }

    public sealed class MachineEntity : Entity
    {
        public string Recipe;
        public Dictionary<string, int> InBuf = new Dictionary<string, int>();
        public Dictionary<string, int> OutBuf = new Dictionary<string, int>();
        public Dictionary<string, double> FluidIn = new Dictionary<string, double>();
        public Dictionary<string, double> FluidOut = new Dictionary<string, double>();
        /// <summary>Fuel buffer in points.</summary>
        public int Fuel;
        public bool Crafting;
        public double Prog;
        public MachineStatus Status = MachineStatus.NoRecipe;
    }

    public sealed class ControllerEntity : Entity
    {
        public string Name;
        public PortMode[] Ports = { PortMode.Off, PortMode.Off, PortMode.Off, PortMode.Off };
        /// <summary>One item in transit per wall.</summary>
        public string[] PortBuf = new string[4];
        /// <summary>Id of the interior dimension.</summary>
        public int Inner;
    }

    public sealed class Formation
    {
        public List<int> Hatches = new List<int>();
        public List<int> Parts = new List<int>();
        public int X, Y, W, H;
    }

    public sealed class CoreEntity : Entity
    {
        public string Recipe;
        public bool Crafting;
        public double Prog;
        public MachineStatus Status = MachineStatus.NotFormed;
        /// <summary>Recomputed every tick; null when the structure isn't valid. Not saved.</summary>
        public Formation Formed;
    }

    public sealed class PartEntity : Entity { }

    public sealed class HatchEntity : Entity
    {
        public Dictionary<string, int> Buf = new Dictionary<string, int>();
        /// <summary>Id of the core whose formed structure includes this hatch (0 = none).</summary>
        public int Core;
    }

    public sealed class Dimension
    {
        public int Id;
        public int Size;
        /// <summary>Controller entity that owns this dimension; 0 for the root.</summary>
        public int ControllerId;
        public int[] Grid;

        public bool IsRoot => ControllerId == 0;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public bool TouchesWall(int x, int y) => x == 0 || y == 0 || x == Size - 1 || y == Size - 1;

        /// <summary>Interior tile next to the port on wall d.</summary>
        public Cell PortTile(Dir d)
        {
            int m = (Size - 1) / 2, e = Size - 1;
            switch (d)
            {
                case Dir.N: return new Cell(m, 0);
                case Dir.E: return new Cell(e, m);
                case Dir.S: return new Cell(m, e);
                default: return new Cell(0, m);
            }
        }
    }

    /// <summary>Connected pipes acting as one tank (Fluid 2.0 style).</summary>
    public sealed class Segment
    {
        public List<int> Pipes = new List<int>();
        public string Fluid;
        public double Amount;
        public double Capacity;
        public double Fill => Capacity > 0 ? Amount / Capacity : 0;
    }
}
