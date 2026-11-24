using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Compacted.Sim;
using Compacted.Sim.Tests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Compacted.Data.Tests
{
    public class ContentDatabaseTests
    {
        const string DatabasePath = "Assets/_Project/Content/ContentDatabase.asset";

        static ContentDatabase LoadDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(DatabasePath);
            Assert.IsNotNull(db, $"No content database at {DatabasePath}");
            return db;
        }

        [Test]
        public void ShippedContentIsValid()
        {
            var errors = LoadDatabase().Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        /// <summary>The assets must describe exactly the content in docs/early-game.md (built in code for the sim tests).</summary>
        [Test]
        public void AssetsMatchTheEarlyGameSpec()
        {
            var fromAssets = Describe(LoadDatabase().Build());
            var fromSpec = Describe(EarlyGameContent.Build());
            var missing = fromSpec.Except(fromAssets).ToList();
            var extra = fromAssets.Except(fromSpec).ToList();
            Assert.IsEmpty(missing, "In the spec but not the assets:\n" + string.Join("\n", missing));
            Assert.IsEmpty(extra, "In the assets but not the spec:\n" + string.Join("\n", extra));
        }

        [Test]
        public void AssetContentPlaysTheOpening()
        {
            var w = new World(LoadDatabase().Build());
            Assert.AreEqual(1, w.InvCount("wall_chipper"));
            Assert.IsTrue(w.Place("wall_chipper", w.RootId, 0, 1, Dir.E, out var chipper).Ok);
            w.Run(12);
            w.TakeOutput(chipper);
            Assert.IsTrue(w.CompleteTech("fr1").Ok, "Field Research I completes with asset content");
            Assert.AreEqual(1, w.InvCount("compressor"));
        }

        [Test]
        public void ValidatorCatchesCommonMistakes()
        {
            var db = ScriptableObject.CreateInstance<ContentDatabase>();
            var a = ScriptableObject.CreateInstance<ItemDefinition>(); a.id = "x";
            var b = ScriptableObject.CreateInstance<ItemDefinition>(); b.id = "x";
            var machine = ScriptableObject.CreateInstance<BuildingDefinition>();
            machine.id = "press"; machine.kind = BuildingKind.Machine; machine.item = a;
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            recipe.id = "r"; recipe.type = RecipeType.Machine; recipe.group = "nobody";
            var t1 = ScriptableObject.CreateInstance<TechNodeDefinition>(); t1.id = "t1";
            var t2 = ScriptableObject.CreateInstance<TechNodeDefinition>(); t2.id = "t2";
            t1.prerequisites.Add(t2); t2.prerequisites.Add(t1);
            db.items.AddRange(new[] { a, b });
            db.buildings.Add(machine);
            db.recipes.Add(recipe);
            db.tech.AddRange(new[] { t1, t2 });

            var errors = string.Join("\n", db.Validate());
            StringAssert.Contains("used more than once", errors);
            StringAssert.Contains("must match the building's id", errors);
            StringAssert.Contains("no machine group", errors);
            StringAssert.Contains("which no machine or multiblock runs", errors);
            StringAssert.Contains("has no outputs", errors);
            StringAssert.Contains("prerequisite cycle", errors);
            StringAssert.Contains("no tuning", errors);

            foreach (var o in new Object[] { db, a, b, machine, recipe, t1, t2 }) Object.DestroyImmediate(o);
        }

        // One line per definition with every field the simulation reads, in a stable order.
        static List<string> Describe(ContentSet c)
        {
            string M<T>(Dictionary<string, T> d) => "{" + string.Join(",", d.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + string.Format(CultureInfo.InvariantCulture, "{0}", kv.Value))) + "}";
            string L(IEnumerable<string> s) => "[" + string.Join(",", s) + "]";
            var lines = new List<string>();
            lines.AddRange(c.Items.Values.Select(i => $"item {i.Id} fuel={i.Fuel} building={i.IsBuilding}"));
            lines.AddRange(c.Fluids.Values.Select(f => $"fluid {f.Id}"));
            lines.AddRange(c.Buildings.Values.Select(b => $"building {b.Id} {b.Kind} free={b.Free} {b.Placement} group={b.MachineGroup} cap={b.OutputCap} size={b.ControllerSize} mb={b.Multiblock} io={b.Io}"));
            lines.AddRange(c.Recipes.Values.Select(r => string.Format(CultureInfo.InvariantCulture,
                "recipe {0} {1} group={2} locked={3} time={4} fuel={5} in={6} fin={7} out={8} fout={9}",
                r.Id, r.Type, r.Group, r.Locked, r.Type == RecipeType.Hand ? 0 : r.Time, r.Fuel, M(r.In), M(r.FluidIn), M(r.Out), M(r.FluidOut))));
            lines.AddRange(c.Multiblocks.Values.Select(m => $"multiblock {m.Id} core={m.Core} group={m.Group} {m.Width}x{m.Height} counts={M(m.Counts)} adj={L(m.Adjacent.Select(p => p.A + "-" + p.B))}"));
            lines.AddRange(c.Tech.Values.Select(t => $"tech {t.Id} at={t.Col},{t.Row} pre={L(t.Prereqs.OrderBy(x => x))} cost={M(t.Cost)} req={M(t.Requires)} grant={M(t.Grant)} hand={L(t.UnlockHand.OrderBy(x => x))} machine={L(t.UnlockMachine.OrderBy(x => x))}"));
            lines.Add("start " + M(c.StartInventory));
            var tu = c.Tuning;
            lines.Add(string.Format(CultureInfo.InvariantCulture, "tuning {0} {1} {2} {3} {4} {5} {6} {7} {8}",
                tu.TickRate, tu.BeltTime, tu.InputBufferMult, tu.OutputCap, tu.FuelBufferMult, tu.FluidBuffer, tu.PipeCapacity, tu.FluidPerTick, tu.InventoryCap));
            return lines;
        }
    }
}
