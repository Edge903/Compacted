using System.Collections.Generic;

namespace Compacted.Sim.Tests
{
    /// <summary>
    /// The early-game content (docs/early-game.md, prototype/content.js) built in code for tests.
    /// The game itself will load the same content from ScriptableObject assets (phase 3).
    /// </summary>
    public static class EarlyGameContent
    {
        static Dictionary<string, int> M(params (string k, int n)[] e) { var d = new Dictionary<string, int>(); foreach (var (k, n) in e) d[k] = n; return d; }
        static Dictionary<string, double> F(params (string k, double n)[] e) { var d = new Dictionary<string, double>(); foreach (var (k, n) in e) d[k] = n; return d; }
        static List<string> L(params string[] s) => new List<string>(s);

        public static ContentSet Build()
        {
            var c = new ContentSet();

            // Items
            c.Add(new ItemDef { Id = "wall_dust", Name = "Wall dust", Fuel = 1 })
             .Add(new ItemDef { Id = "dim_scrap", Name = "Dimensium scrap" })
             .Add(new ItemDef { Id = "dim_clay", Name = "Dimensium clay" })
             .Add(new ItemDef { Id = "dim_bar", Name = "Dimensium bar" })
             .Add(new ItemDef { Id = "en_dust", Name = "Energetic dust", Fuel = 2 })
             .Add(new ItemDef { Id = "en_scrap", Name = "Energetic scrap", Fuel = 8 })
             .Add(new ItemDef { Id = "energy_rod", Name = "Energy rod" });
            foreach (var b in new[] { "wall_chipper", "wall_miner", "condenser", "compressor", "mixer", "furnace", "assembler", "compactor_core", "dim_coil", "input_bus", "output_bus", "ctrl3" })
                c.Add(new ItemDef { Id = b, Name = b, IsBuilding = true });
            c.Add(new FluidDef { Id = "water", Name = "Water" });

            // Buildings
            c.Add(new BuildingDef { Id = "belt", Name = "Belt", Kind = BuildingKind.Belt, Free = true })
             .Add(new BuildingDef { Id = "pipe", Name = "Pipe", Kind = BuildingKind.Pipe, Free = true })
             .Add(new BuildingDef { Id = "wall_chipper", Name = "Wall chipper", Kind = BuildingKind.Machine, MachineGroup = "chipper", Placement = Placement.Wall, OutputCap = 50 })
             .Add(new BuildingDef { Id = "wall_miner", Name = "Wall miner", Kind = BuildingKind.Machine, MachineGroup = "miner", Placement = Placement.Wall, OutputCap = 50 })
             .Add(new BuildingDef { Id = "condenser", Name = "Atmospheric condenser", Kind = BuildingKind.Machine, MachineGroup = "condenser" })
             .Add(new BuildingDef { Id = "compressor", Name = "Compressor", Kind = BuildingKind.Machine, MachineGroup = "compressor" })
             .Add(new BuildingDef { Id = "mixer", Name = "Mixer", Kind = BuildingKind.Machine, MachineGroup = "mixer" })
             .Add(new BuildingDef { Id = "furnace", Name = "Furnace", Kind = BuildingKind.Machine, MachineGroup = "furnace" })
             .Add(new BuildingDef { Id = "assembler", Name = "Assembler", Kind = BuildingKind.Machine, MachineGroup = "assembler" })
             .Add(new BuildingDef { Id = "compactor_core", Name = "Compactor Core", Kind = BuildingKind.Core, Multiblock = "compactor" })
             .Add(new BuildingDef { Id = "dim_coil", Name = "Dimensium coil block", Kind = BuildingKind.Part })
             .Add(new BuildingDef { Id = "input_bus", Name = "Dimensium input bus", Kind = BuildingKind.Hatch, Io = HatchIo.In })
             .Add(new BuildingDef { Id = "output_bus", Name = "Dimensium output bus", Kind = BuildingKind.Hatch, Io = HatchIo.Out })
             .Add(new BuildingDef { Id = "ctrl3", Name = "3×3 dimension controller", Kind = BuildingKind.Controller, ControllerSize = 3 });

            // Multiblocks
            c.Add(new MultiblockDef
            {
                Id = "compactor", Name = "Dimension Compactor", Core = "compactor_core", Group = "compactor", Width = 2, Height = 2,
                Counts = { ["compactor_core"] = (1, 1), ["dim_coil"] = (1, 1), ["input_bus"] = (1, 2), ["output_bus"] = (1, 2) },
                Adjacent = { ("dim_coil", "compactor_core") },
            });

            // Machine recipes
            void R(string id, string group, Dictionary<string, int> inp, Dictionary<string, int> outp, double time, int fuel = 0, Dictionary<string, double> fin = null, Dictionary<string, double> fout = null, bool locked = false) =>
                c.Add(new RecipeDef { Id = id, Type = RecipeType.Machine, Group = group, In = inp, Out = outp, Time = time, Fuel = fuel, FluidIn = fin ?? F(), FluidOut = fout ?? F(), Locked = locked });
            R("chip_dust", "chipper", M(), M(("wall_dust", 1)), 1);
            R("mine_energy", "miner", M(), M(("en_dust", 1)), 2);
            R("condense", "condenser", M(), M(), 1, fout: F(("water", 40)));
            R("press_scrap", "compressor", M(("wall_dust", 2)), M(("dim_scrap", 1)), 2);
            R("press_en", "compressor", M(("en_dust", 2)), M(("en_scrap", 1)), 2);
            R("press_rod", "compressor", M(("en_scrap", 8)), M(("energy_rod", 1)), 8);
            R("mix_clay", "mixer", M(("wall_dust", 4)), M(("dim_clay", 1)), 6, fin: F(("water", 500)));
            R("smelt_bar", "furnace", M(("dim_clay", 1)), M(("dim_bar", 1)), 4, fuel: 16);
            R("asm_coil", "assembler", M(("dim_bar", 4), ("energy_rod", 1)), M(("dim_coil", 1)), 10, locked: true);
            R("asm_core", "assembler", M(("dim_bar", 16), ("energy_rod", 2)), M(("compactor_core", 1)), 10, locked: true);
            R("compact_ctrl3", "compactor", M(("dim_bar", 32), ("energy_rod", 4)), M(("ctrl3", 1)), 20);

            // Hand recipes
            void H(string id, Dictionary<string, int> inp, string output) =>
                c.Add(new RecipeDef { Id = id, Type = RecipeType.Hand, In = inp, Out = M((output, 1)) });
            H("h_chipper", M(("dim_bar", 4)), "wall_chipper");
            H("h_compressor", M(("dim_bar", 4)), "compressor");
            H("h_condenser", M(("dim_bar", 4)), "condenser");
            H("h_mixer", M(("dim_bar", 4)), "mixer");
            H("h_furnace", M(("dim_bar", 4)), "furnace");
            H("h_miner", M(("dim_bar", 8)), "wall_miner");
            H("h_assembler", M(("dim_bar", 8), ("energy_rod", 1)), "assembler");
            H("h_input_bus", M(("dim_bar", 4)), "input_bus");
            H("h_output_bus", M(("dim_bar", 4)), "output_bus");

            // Tech tree
            c.Add(new TechNodeDef { Id = "fr1", Name = "Field Research I", Col = 0, Row = 1, Cost = M(("wall_dust", 10)), Grant = M(("compressor", 1), ("wall_chipper", 1)) })
             .Add(new TechNodeDef { Id = "fr2", Name = "Field Research II", Col = 1, Row = 1, Prereqs = L("fr1"), Cost = M(("dim_scrap", 10)), Grant = M(("condenser", 1), ("mixer", 1), ("furnace", 1)) })
             .Add(new TechNodeDef { Id = "fr3", Name = "Field Research III", Col = 2, Row = 1, Prereqs = L("fr2"), Cost = M(("dim_bar", 1)), Grant = M(("compressor", 1), ("condenser", 1), ("mixer", 1), ("furnace", 1)) })
             .Add(new TechNodeDef { Id = "basic", Name = "Basic Machinery", Col = 3, Row = 0, Prereqs = L("fr3"), Cost = M(("dim_bar", 10)), UnlockHand = L("h_chipper", "h_compressor", "h_condenser", "h_mixer", "h_furnace") })
             .Add(new TechNodeDef { Id = "mining", Name = "Mining", Col = 4, Row = 0, Prereqs = L("basic"), Cost = M(("dim_bar", 20)), Grant = M(("wall_miner", 1)), UnlockHand = L("h_miner") })
             .Add(new TechNodeDef { Id = "assembler", Name = "Assembler", Col = 5, Row = 0, Prereqs = L("mining"), Cost = M(("energy_rod", 1)), UnlockHand = L("h_assembler", "h_input_bus", "h_output_bus"), UnlockMachine = L("asm_coil", "asm_core") })
             .Add(new TechNodeDef { Id = "dimcomp", Name = "Dimensional Compression", Col = 4, Row = 2, Prereqs = L("fr3"), Requires = M(("compactor_core", 1)) })
             .Add(new TechNodeDef { Id = "expansion", Name = "Time for Expansion", Col = 6, Row = 1, Prereqs = L("assembler", "dimcomp"), Requires = M(("ctrl3", 1)) });

            c.StartInventory["wall_chipper"] = 1;
            return c;
        }
    }
}
