using Compacted.Sim;
using UnityEngine;

namespace Compacted.View
{
    /// <summary>Fixed colors for the debug view (abstract, no text on the map).</summary>
    public static class Palette
    {
        public static readonly Color Background = Hex("#15171c");
        public static readonly Color Tile = Hex("#1d2027");
        public static readonly Color RootWall = Hex("#2a2e38");
        public static readonly Color Wall = Hex("#3a3f4b");
        public static readonly Color In = Hex("#4cc38a");
        public static readonly Color Out = Hex("#f0b44c");
        public static readonly Color Off = Hex("#5a606c");
        public static readonly Color Fluid = Hex("#4ea8f0");
        public static readonly Color Fuel = Hex("#f08a3c");
        public static readonly Color Danger = Hex("#e5605a");
        public static readonly Color Accent = Hex("#5b9cf0");
        public static readonly Color Formed = Hex("#3fb5a3");
        public static readonly Color Controller = Hex("#2a2140");
        public static readonly Color ControllerEdge = Hex("#9a7ad0");
        public static readonly Color BeltBase = Hex("#2b2f37");
        public static readonly Color BeltMark = new Color(0.85f, 0.78f, 0.42f, 0.55f);
        public static readonly Color PipeOuter = Hex("#4a5160");
        public static readonly Color PipeInner = Hex("#1b1e25");
        public static readonly Color Muted = Hex("#9aa1ad");
        public static readonly Color ItemOutline = new Color(0, 0, 0, 0.55f);

        public static Color Port(PortMode m) => m == PortMode.In ? In : m == PortMode.Out ? Out : m == PortMode.Fluid ? Fluid : Off;

        public static Color Status(MachineStatus s)
        {
            switch (s)
            {
                case MachineStatus.Working: return In;
                case MachineStatus.NeedsFuel: return Fuel;
                case MachineStatus.OutputBlocked: return Out;
                case MachineStatus.NoRecipe:
                case MachineStatus.NotFormed: return Danger;
                default: return Muted;
            }
        }

        public static string StatusText(MachineStatus s)
        {
            switch (s)
            {
                case MachineStatus.Working: return "Working";
                case MachineStatus.NeedsFuel: return "Needs fuel";
                case MachineStatus.OutputBlocked: return "Output blocked";
                case MachineStatus.NoRecipe: return "No recipe";
                case MachineStatus.NotFormed: return "Not formed";
                default: return "Waiting for input";
            }
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        public static Color A(this Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);
    }
}
