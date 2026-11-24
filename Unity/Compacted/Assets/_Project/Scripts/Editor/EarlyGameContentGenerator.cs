using System.Collections.Generic;
using System.Linq;
using Compacted.Data;
using Compacted.Sim;
using UnityEditor;
using UnityEngine;

namespace Compacted.Editor
{
    /// <summary>
    /// Creates (or updates in place) the early-game content assets from docs/early-game.md.
    /// Safe to run again: assets are found by path and overwritten field by field, so GUIDs and
    /// references stay stable. After the first run, edit the assets directly instead.
    /// </summary>
    public static class EarlyGameContentGenerator
    {
        public const string Root = "Assets/_Project/Content";
        public const string DatabasePath = Root + "/ContentDatabase.asset";

        [MenuItem("Compacted/Content/Generate Early-Game Content")]
        public static void Generate()
        {
            foreach (var f in new[] { "Items", "Fluids", "Buildings", "Recipes", "Multiblocks", "Tech" }) EnsureFolder(Root + "/" + f);

            // ---------- items ----------
            var it = new Dictionary<string, ItemDefinition>();
            void Item(string id, string name, string hex, int fuel = 0)
            {
                var a = Get<ItemDefinition>("Items", id, name);
                a.visual = new Visual(Hex(hex)) { sprite = a.visual.sprite };
                a.fuel = fuel;
                it[id] = a;
            }
            Item("wall_dust", "Wall dust", "#a89f91", 1);
            Item("dim_scrap", "Dimensium scrap", "#7d8bb0");
            Item("dim_clay", "Dimensium clay", "#9c7bb5");
            Item("dim_bar", "Dimensium bar", "#8fb4ff");
            Item("en_dust", "Energetic dust", "#f2d25c", 2);
            Item("en_scrap", "Energetic scrap", "#f0a33c", 8);
            Item("energy_rod", "Energy rod", "#ff7b54");
            Item("wall_chipper", "Wall chipper", "#8a8272");
            Item("wall_miner", "Wall miner", "#c2a640");
            Item("condenser", "Atmospheric condenser", "#4e9fd6");
            Item("compressor", "Compressor", "#6c7a96");
            Item("mixer", "Mixer", "#8a6aa8");
            Item("furnace", "Furnace", "#c0603e");
            Item("assembler", "Assembler", "#4f78b0");
            Item("compactor_core", "Compactor Core", "#3fb5a3");
            Item("dim_coil", "Dimensium coil block", "#c98d4a");
            Item("input_bus", "Dimensium input bus", "#4cc38a");
            Item("output_bus", "Dimensium output bus", "#f0b44c");
            Item("ctrl3", "3×3 dimension controller", "#9a7ad0");

            var water = Get<FluidDefinition>("Fluids", "water", "Water");
            water.color = Hex("#4ea8f0");

            // ---------- buildings ----------
            var bd = new Dictionary<string, BuildingDefinition>();
            BuildingDefinition Building(string id, string name, BuildingKind kind, BrowserTab tab, string hex)
            {
                var a = Get<BuildingDefinition>("Buildings", id, name);
                a.kind = kind; a.browserTab = tab;
                a.visual = new Visual(Hex(hex)) { sprite = a.visual.sprite };
                a.free = false; a.item = it.TryGetValue(id, out var item) ? item : null;
                a.placement = Placement.Any; a.machineGroup = ""; a.outputCap = 0;
                a.controllerSize = 0; a.multiblock = null; a.hatchIo = HatchIo.None;
                bd[id] = a;
                return a;
            }
            var belt = Building("belt", "Belt", BuildingKind.Belt, BrowserTab.Logistics, "#d8c86a"); belt.free = true;
            var pipe = Building("pipe", "Pipe", BuildingKind.Pipe, BrowserTab.Logistics, "#4a5160"); pipe.free = true;
            BuildingDefinition Machine(string id, string name, string group, string hex, Placement placement = Placement.Any, int outputCap = 0)
            {
                var a = Building(id, name, BuildingKind.Machine, BrowserTab.Machines, hex);
                a.machineGroup = group; a.placement = placement; a.outputCap = outputCap;
                return a;
            }
            Machine("wall_chipper", "Wall chipper", "chipper", "#4d4a42", Placement.Wall, 50);
            Machine("wall_miner", "Wall miner", "miner", "#5e5226", Placement.Wall, 50);
            Machine("condenser", "Atmospheric condenser", "condenser", "#24506e");
            Machine("compressor", "Compressor", "compressor", "#3a4256");
            Machine("mixer", "Mixer", "mixer", "#4a3a5e");
            Machine("furnace", "Furnace", "furnace", "#6e3626");
            Machine("assembler", "Assembler", "assembler", "#2f4a6e");
            var core = Building("compactor_core", "Compactor Core", BuildingKind.Core, BrowserTab.Other, "#1f5a52");
            Building("dim_coil", "Dimensium coil block", BuildingKind.Part, BrowserTab.Other, "#6b4a26");
            Building("input_bus", "Dimensium input bus", BuildingKind.Hatch, BrowserTab.Other, "#24483a").hatchIo = HatchIo.In;
            Building("output_bus", "Dimensium output bus", BuildingKind.Hatch, BrowserTab.Other, "#4d3d1e").hatchIo = HatchIo.Out;
            Building("ctrl3", "3×3 dimension controller", BuildingKind.Controller, BrowserTab.Other, "#2a2140").controllerSize = 3;

            // ---------- multiblock ----------
            var compactor = Get<MultiblockDefinition>("Multiblocks", "compactor", "Dimension Compactor");
            compactor.core = core; compactor.group = "compactor"; compactor.size = new Vector2Int(2, 2);
            compactor.counts = new List<BlockCount>
            {
                new BlockCount(core, 1, 1), new BlockCount(bd["dim_coil"], 1, 1),
                new BlockCount(bd["input_bus"], 1, 2), new BlockCount(bd["output_bus"], 1, 2),
            };
            compactor.adjacent = new List<BlockPair> { new BlockPair(bd["dim_coil"], core) };
            core.multiblock = compactor;

            // ---------- recipes ----------
            var rc = new Dictionary<string, RecipeDefinition>();
            ItemAmount I(string id, int n) => new ItemAmount(it[id], n);
            RecipeDefinition MR(string id, string name, string group, float time, ItemAmount[] inp, ItemAmount[] outp,
                                int fuel = 0, FluidAmount[] fin = null, FluidAmount[] fout = null, bool locked = false)
            {
                var a = Get<RecipeDefinition>("Recipes", id, name);
                a.type = RecipeType.Machine; a.group = group; a.time = time; a.locked = locked; a.fuel = fuel;
                a.inputs = inp.ToList(); a.outputs = outp.ToList();
                a.fluidInputs = (fin ?? new FluidAmount[0]).ToList(); a.fluidOutputs = (fout ?? new FluidAmount[0]).ToList();
                rc[id] = a;
                return a;
            }
            var none = new ItemAmount[0];
            MR("chip_dust", "Wall dust", "chipper", 1, none, new[] { I("wall_dust", 1) });
            MR("mine_energy", "Energetic dust", "miner", 2, none, new[] { I("en_dust", 1) });
            MR("condense", "Water", "condenser", 1, none, none, fout: new[] { new FluidAmount(water, 40) });
            MR("press_scrap", "Dimensium scrap", "compressor", 2, new[] { I("wall_dust", 2) }, new[] { I("dim_scrap", 1) });
            MR("press_en", "Energetic scrap", "compressor", 2, new[] { I("en_dust", 2) }, new[] { I("en_scrap", 1) });
            MR("press_rod", "Energy rod", "compressor", 8, new[] { I("en_scrap", 8) }, new[] { I("energy_rod", 1) });
            MR("mix_clay", "Dimensium clay", "mixer", 6, new[] { I("wall_dust", 4) }, new[] { I("dim_clay", 1) }, fin: new[] { new FluidAmount(water, 500) });
            MR("smelt_bar", "Dimensium bar", "furnace", 4, new[] { I("dim_clay", 1) }, new[] { I("dim_bar", 1) }, fuel: 16);
            MR("asm_coil", "Dimensium coil block", "assembler", 10, new[] { I("dim_bar", 4), I("energy_rod", 1) }, new[] { I("dim_coil", 1) }, locked: true);
            MR("asm_core", "Compactor Core", "assembler", 10, new[] { I("dim_bar", 16), I("energy_rod", 2) }, new[] { I("compactor_core", 1) }, locked: true);
            MR("compact_ctrl3", "3×3 dimension controller", "compactor", 20, new[] { I("dim_bar", 32), I("energy_rod", 4) }, new[] { I("ctrl3", 1) });

            void HR(string id, string output, params ItemAmount[] inp)
            {
                var a = Get<RecipeDefinition>("Recipes", id, it[output].displayName);
                a.type = RecipeType.Hand; a.group = ""; a.time = 0; a.locked = false; a.fuel = 0;
                a.inputs = inp.ToList(); a.outputs = new List<ItemAmount> { I(output, 1) };
                a.fluidInputs = new List<FluidAmount>(); a.fluidOutputs = new List<FluidAmount>();
                rc[id] = a;
            }
            HR("h_chipper", "wall_chipper", I("dim_bar", 4));
            HR("h_compressor", "compressor", I("dim_bar", 4));
            HR("h_condenser", "condenser", I("dim_bar", 4));
            HR("h_mixer", "mixer", I("dim_bar", 4));
            HR("h_furnace", "furnace", I("dim_bar", 4));
            HR("h_miner", "wall_miner", I("dim_bar", 8));
            HR("h_assembler", "assembler", I("dim_bar", 8), I("energy_rod", 1));
            HR("h_input_bus", "input_bus", I("dim_bar", 4));
            HR("h_output_bus", "output_bus", I("dim_bar", 4));

            // ---------- tech ----------
            var tn = new Dictionary<string, TechNodeDefinition>();
            TechNodeDefinition Tech(string id, string name, int col, int row, string[] prereqs,
                                    ItemAmount[] cost = null, ItemAmount[] requires = null, ItemAmount[] grants = null,
                                    string[] hand = null, string[] machine = null)
            {
                var a = Get<TechNodeDefinition>("Tech", id, name);
                a.position = new Vector2Int(col, row);
                a.prerequisites = prereqs.Select(p => tn[p]).ToList();
                a.cost = (cost ?? none).ToList(); a.requires = (requires ?? none).ToList(); a.grants = (grants ?? none).ToList();
                a.unlockHandRecipes = (hand ?? new string[0]).Select(r => rc[r]).ToList();
                a.unlockMachineRecipes = (machine ?? new string[0]).Select(r => rc[r]).ToList();
                tn[id] = a;
                return a;
            }
            Tech("fr1", "Field Research I", 0, 1, new string[0], cost: new[] { I("wall_dust", 10) }, grants: new[] { I("compressor", 1), I("wall_chipper", 1) });
            Tech("fr2", "Field Research II", 1, 1, new[] { "fr1" }, cost: new[] { I("dim_scrap", 10) }, grants: new[] { I("condenser", 1), I("mixer", 1), I("furnace", 1) });
            Tech("fr3", "Field Research III", 2, 1, new[] { "fr2" }, cost: new[] { I("dim_bar", 1) }, grants: new[] { I("compressor", 1), I("condenser", 1), I("mixer", 1), I("furnace", 1) });
            Tech("basic", "Basic Machinery", 3, 0, new[] { "fr3" }, cost: new[] { I("dim_bar", 10) }, hand: new[] { "h_chipper", "h_compressor", "h_condenser", "h_mixer", "h_furnace" });
            Tech("mining", "Mining", 4, 0, new[] { "basic" }, cost: new[] { I("dim_bar", 20) }, grants: new[] { I("wall_miner", 1) }, hand: new[] { "h_miner" });
            Tech("assembler", "Assembler", 5, 0, new[] { "mining" }, cost: new[] { I("energy_rod", 1) }, hand: new[] { "h_assembler", "h_input_bus", "h_output_bus" }, machine: new[] { "asm_coil", "asm_core" });
            Tech("dimcomp", "Dimensional Compression", 4, 2, new[] { "fr3" }, requires: new[] { I("compactor_core", 1) });
            Tech("expansion", "Time for Expansion", 6, 1, new[] { "assembler", "dimcomp" }, requires: new[] { I("ctrl3", 1) });

            // ---------- tuning & database ----------
            var tuning = AssetDatabase.LoadAssetAtPath<TuningDefinition>(Root + "/Tuning.asset");
            if (tuning == null) { tuning = ScriptableObject.CreateInstance<TuningDefinition>(); AssetDatabase.CreateAsset(tuning, Root + "/Tuning.asset"); }
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            if (db == null) { db = ScriptableObject.CreateInstance<ContentDatabase>(); AssetDatabase.CreateAsset(db, DatabasePath); }
            db.tuning = tuning;
            db.startInventory = new List<ItemAmount> { I("wall_chipper", 1) };
            db.items = it.Values.ToList();
            db.fluids = new List<FluidDefinition> { water };
            db.buildings = bd.Values.ToList();
            db.recipes = rc.Values.ToList();
            db.multiblocks = new List<MultiblockDefinition> { compactor };
            db.tech = tn.Values.ToList();

            foreach (var o in it.Values.Cast<Object>().Concat(bd.Values).Concat(rc.Values).Concat(tn.Values).Concat(new Object[] { water, compactor, tuning, db }))
                EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();

            var errors = db.Validate();
            if (errors.Count == 0) Debug.Log($"Early-game content generated at {Root}: {it.Count} items, {bd.Count} buildings, {rc.Count} recipes, {tn.Count} tech nodes. Content is valid.", db);
            else Debug.LogError("Early-game content generated with problems:\n• " + string.Join("\n• ", errors), db);
        }

        static T Get<T>(string folder, string id, string displayName) where T : Definition
        {
            var path = $"{Root}/{folder}/{id}.asset";
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(a, path); }
            a.id = id;
            a.displayName = displayName;
            return a;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
