using System.Collections.Generic;
using System.Linq;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    /// <summary>
    /// Every definition the game uses, plus tuning and the starting inventory.
    /// Build() converts it into the simulation's ContentSet; Validate() lists content mistakes.
    /// </summary>
    [CreateAssetMenu(menuName = "Compacted/Content Database", fileName = "ContentDatabase")]
    public sealed class ContentDatabase : ScriptableObject
    {
        public TuningDefinition tuning;
        public List<ItemAmount> startInventory = new List<ItemAmount>();
        public List<ItemDefinition> items = new List<ItemDefinition>();
        public List<FluidDefinition> fluids = new List<FluidDefinition>();
        public List<BuildingDefinition> buildings = new List<BuildingDefinition>();
        public List<RecipeDefinition> recipes = new List<RecipeDefinition>();
        public List<MultiblockDefinition> multiblocks = new List<MultiblockDefinition>();
        [Tooltip("In display order.")]
        public List<TechNodeDefinition> tech = new List<TechNodeDefinition>();

        public BuildingDefinition Building(string id) => buildings.FirstOrDefault(b => b != null && b.id == id);
        public ItemDefinition Item(string id) => items.FirstOrDefault(i => i != null && i.id == id);
        public FluidDefinition Fluid(string id) => fluids.FirstOrDefault(f => f != null && f.id == id);

        /// <summary>Convert to the simulation's definitions. Run Validate() first: Build assumes valid content.</summary>
        public ContentSet Build()
        {
            var c = new ContentSet { Tuning = tuning != null ? tuning.ToSim() : new Tuning() };
            var buildingItems = new HashSet<ItemDefinition>(buildings.Where(b => b != null && b.item != null).Select(b => b.item));

            foreach (var i in items.Where(x => x != null))
                c.Add(new ItemDef { Id = i.id, Name = i.displayName, Fuel = i.fuel, IsBuilding = buildingItems.Contains(i) });
            foreach (var f in fluids.Where(x => x != null))
                c.Add(new FluidDef { Id = f.id, Name = f.displayName });
            foreach (var b in buildings.Where(x => x != null))
                c.Add(new BuildingDef
                {
                    Id = b.id, Name = b.displayName, Kind = b.kind, Free = b.free, Placement = b.placement,
                    MachineGroup = string.IsNullOrEmpty(b.machineGroup) ? null : b.machineGroup,
                    OutputCap = b.outputCap > 0 ? b.outputCap : (int?)null,
                    ControllerSize = b.controllerSize,
                    Multiblock = b.multiblock != null ? b.multiblock.id : null,
                    Io = b.hatchIo,
                });
            foreach (var r in recipes.Where(x => x != null))
                c.Add(new RecipeDef
                {
                    Id = r.id, Type = r.type, Group = string.IsNullOrEmpty(r.group) ? null : r.group,
                    Locked = r.locked, Time = r.time, Fuel = r.fuel,
                    In = Items(r.inputs), Out = Items(r.outputs),
                    FluidIn = Fluids(r.fluidInputs), FluidOut = Fluids(r.fluidOutputs),
                });
            foreach (var m in multiblocks.Where(x => x != null))
            {
                var def = new MultiblockDef { Id = m.id, Name = m.displayName, Core = m.core.id, Group = m.group, Width = m.size.x, Height = m.size.y };
                foreach (var bc in m.counts) def.Counts[bc.block.id] = (bc.min, bc.max);
                foreach (var p in m.adjacent) def.Adjacent.Add((p.a.id, p.b.id));
                c.Add(def);
            }
            foreach (var t in tech.Where(x => x != null))
                c.Add(new TechNodeDef
                {
                    Id = t.id, Name = t.displayName, Col = t.position.x, Row = t.position.y,
                    Prereqs = t.prerequisites.Where(p => p != null).Select(p => p.id).ToList(),
                    Cost = Items(t.cost), Requires = Items(t.requires), Grant = Items(t.grants),
                    UnlockHand = t.unlockHandRecipes.Where(r => r != null).Select(r => r.id).ToList(),
                    UnlockMachine = t.unlockMachineRecipes.Where(r => r != null).Select(r => r.id).ToList(),
                });
            foreach (var kv in Items(startInventory)) c.StartInventory[kv.Key] = kv.Value;
            return c;
        }

        static Dictionary<string, int> Items(List<ItemAmount> list)
        {
            var d = new Dictionary<string, int>();
            foreach (var a in list) if (a.item != null) d[a.item.id] = (d.TryGetValue(a.item.id, out var n) ? n : 0) + a.amount;
            return d;
        }

        static Dictionary<string, double> Fluids(List<FluidAmount> list)
        {
            var d = new Dictionary<string, double>();
            foreach (var a in list) if (a.fluid != null) d[a.fluid.id] = (d.TryGetValue(a.fluid.id, out var n) ? n : 0) + a.ml;
            return d;
        }

        /// <summary>Every content mistake found, as readable sentences. Empty means the content is valid.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            void Err(string msg) => errors.Add(msg);

            if (tuning == null) Err("The database has no tuning asset.");
            CheckList("Item", items, errors);
            CheckList("Fluid", fluids, errors);
            CheckList("Building", buildings, errors);
            CheckList("Recipe", recipes, errors);
            CheckList("Multiblock", multiblocks, errors);
            CheckList("Tech node", tech, errors);

            var groups = new HashSet<string>(buildings.Where(b => b != null && b.kind == BuildingKind.Machine).Select(b => b.machineGroup));
            foreach (var m in multiblocks.Where(x => x != null)) groups.Add(m.group);

            foreach (var b in buildings.Where(x => x != null))
            {
                string n = $"Building '{b.id}'";
                if (!b.free && b.item == null) Err($"{n} isn't free but has no item to place it from.");
                if (b.item != null && b.item.id != b.id) Err($"{n} uses item '{b.item.id}', but the item's id must match the building's id.");
                if (b.item != null && !items.Contains(b.item)) Err($"{n} uses item '{b.item.id}', which isn't in the database.");
                if (b.kind == BuildingKind.Machine && string.IsNullOrEmpty(b.machineGroup)) Err($"{n} is a machine with no machine group.");
                if (b.kind == BuildingKind.Controller && (b.controllerSize < 3 || b.controllerSize % 2 == 0)) Err($"{n} has controller size {b.controllerSize}; it must be odd and at least 3.");
                if (b.kind == BuildingKind.Core && b.multiblock == null) Err($"{n} is a core with no multiblock.");
                if (b.kind == BuildingKind.Hatch && b.hatchIo == HatchIo.None) Err($"{n} is a hatch with no input/output direction.");
            }

            foreach (var r in recipes.Where(x => x != null))
            {
                string n = $"Recipe '{r.id}'";
                if (r.type == RecipeType.Machine)
                {
                    if (string.IsNullOrEmpty(r.group)) Err($"{n} is a machine recipe with no group.");
                    else if (!groups.Contains(r.group)) Err($"{n} uses group '{r.group}', which no machine or multiblock runs.");
                    if (r.time <= 0) Err($"{n} has a craft time of {r.time}; it must be above 0.");
                }
                else
                {
                    if (r.fluidInputs.Count > 0 || r.fluidOutputs.Count > 0 || r.fuel > 0) Err($"{n} is a hand recipe, which can't use fluids or fuel.");
                    if (r.locked) Err($"{n} is a hand recipe marked locked; hand recipes are unlocked through tech nodes instead.");
                }
                if (r.outputs.Count == 0 && r.fluidOutputs.Count == 0) Err($"{n} has no outputs.");
                foreach (var a in r.inputs.Concat(r.outputs))
                {
                    if (a.item == null) Err($"{n} has an empty item slot.");
                    else if (!items.Contains(a.item)) Err($"{n} uses item '{a.item.id}', which isn't in the database.");
                    if (a.amount <= 0) Err($"{n} has an item amount of {a.amount}; it must be above 0.");
                }
                foreach (var a in r.fluidInputs.Concat(r.fluidOutputs))
                {
                    if (a.fluid == null) Err($"{n} has an empty fluid slot.");
                    else if (!fluids.Contains(a.fluid)) Err($"{n} uses fluid '{a.fluid.id}', which isn't in the database.");
                    if (a.ml <= 0) Err($"{n} has a fluid amount of {a.ml} ml; it must be above 0.");
                }
            }

            foreach (var m in multiblocks.Where(x => x != null))
            {
                string n = $"Multiblock '{m.id}'";
                if (m.core == null) Err($"{n} has no core.");
                else if (m.core.kind != BuildingKind.Core) Err($"{n} uses '{m.core.id}' as its core, but that building isn't a core.");
                if (m.size.x < 1 || m.size.y < 1) Err($"{n} has an empty footprint.");
                if (m.core != null && !m.counts.Any(bc => bc.block == m.core)) Err($"{n} doesn't list its core in its block counts.");
                foreach (var bc in m.counts)
                {
                    if (bc.block == null) Err($"{n} has an empty block count.");
                    else if (bc.min < 0 || bc.max < bc.min) Err($"{n} has an invalid count range for '{bc.block.id}'.");
                }
                foreach (var p in m.adjacent) if (p.a == null || p.b == null) Err($"{n} has an empty adjacency pair.");
                int minTiles = m.counts.Sum(bc => bc.min), maxTiles = m.counts.Sum(bc => bc.max);
                if (m.size.x * m.size.y < minTiles || m.size.x * m.size.y > maxTiles)
                    Err($"{n}'s counts ({minTiles}–{maxTiles} blocks) can't fill its {m.size.x}×{m.size.y} footprint.");
            }

            foreach (var t in tech.Where(x => x != null))
            {
                string n = $"Tech node '{t.id}'";
                foreach (var p in t.prerequisites)
                {
                    if (p == null) Err($"{n} has an empty prerequisite.");
                    else if (!tech.Contains(p)) Err($"{n} needs '{p.id}', which isn't in the database.");
                }
                foreach (var a in t.cost.Concat(t.requires).Concat(t.grants))
                    if (a.item == null || a.amount <= 0) Err($"{n} has an empty or zero item amount.");
                foreach (var r in t.unlockHandRecipes)
                    if (r == null || r.type != RecipeType.Hand) Err($"{n} unlocks '{r?.id}' as a hand recipe, but it isn't one.");
                foreach (var r in t.unlockMachineRecipes)
                    if (r == null || r.type != RecipeType.Machine) Err($"{n} unlocks '{r?.id}' as a machine recipe, but it isn't one.");
            }
            if (HasCycle()) Err("The tech tree has a prerequisite cycle, so some nodes can never open.");
            if (!tech.Any(t => t != null && t.prerequisites.Count == 0)) Err("No tech node is open at the start (every node has prerequisites).");
            foreach (var a in startInventory) if (a.item == null || a.amount <= 0) Err("The starting inventory has an empty or zero entry.");
            return errors;
        }

        static void CheckList<T>(string label, List<T> list, List<string> errors) where T : Definition
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                if (d == null) { errors.Add($"{label} slot {i} is empty."); continue; }
                if (string.IsNullOrWhiteSpace(d.id)) errors.Add($"{label} asset '{d.name}' has no id.");
                else if (!seen.Add(d.id)) errors.Add($"{label} id '{d.id}' is used more than once.");
            }
        }

        bool HasCycle()
        {
            var state = new Dictionary<TechNodeDefinition, int>();   // 1 = visiting, 2 = done
            bool Visit(TechNodeDefinition t)
            {
                if (t == null) return false;
                if (state.TryGetValue(t, out var s)) return s == 1;
                state[t] = 1;
                foreach (var p in t.prerequisites) if (Visit(p)) return true;
                state[t] = 2;
                return false;
            }
            return tech.Any(Visit);
        }
    }
}
