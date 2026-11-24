using NUnit.Framework;

namespace Compacted.Sim.Tests
{
    /// <summary>The opening of the early game, played through on the simulation (prototype check group 1).</summary>
    public class PlaythroughTests : SimTestBase
    {
        [Test]
        public void OpeningThroughFieldResearchIII()
        {
            var w = NewWorld();
            int R = w.RootId;

            Assert.AreEqual(1, w.Inventory.Count, "start inventory is one chipper");
            Assert.AreEqual(1, w.InvCount("wall_chipper"));
            Assert.IsFalse(w.CanPlace("wall_chipper", R, 1, 1).Ok, "chipper refused in the centre (no wall)");

            var chipper = Place<MachineEntity>(w, "wall_chipper", R, 0, 1, Dir.E);
            Assert.AreEqual("chip_dust", chipper.Recipe, "single-recipe source starts with its recipe");
            w.Run(12);
            w.TakeOutput(chipper);
            Assert.GreaterOrEqual(w.InvCount("wall_dust"), 10, "chipper makes about 1 dust per second");

            Assert.AreEqual(TechState.Hidden, w.StateOf("fr2"), "Field Research II hidden before I");
            Assert.IsTrue(w.CompleteTech("fr1").Ok, "complete FR I");
            Assert.AreEqual(1, w.InvCount("compressor"));
            Assert.AreEqual(1, w.InvCount("wall_chipper"));

            var compressor = Place<MachineEntity>(w, "compressor", R, 1, 1, Dir.S);   // the chipper faces east into it
            w.SetRecipe(compressor, "press_scrap");
            w.Run(25);
            w.TakeOutput(compressor);
            Assert.GreaterOrEqual(w.InvCount("dim_scrap"), 10, "compressor made scrap");
            Assert.IsTrue(w.CompleteTech("fr2").Ok, "complete FR II");

            // Bar line: chipper A (0,1) -> mixer (1,1) -> furnace (2,1); condenser (1,0) feeds the mixer directly;
            // chipper B (2,0) fuels the furnace.
            Destroy(w, R, 1, 1);
            Place<MachineEntity>(w, "condenser", R, 1, 0, Dir.S);
            var mixer = Place<MachineEntity>(w, "mixer", R, 1, 1, Dir.E);
            w.SetRecipe(mixer, "mix_clay");
            var furnace = Place<MachineEntity>(w, "furnace", R, 2, 1, Dir.S);
            w.SetRecipe(furnace, "smelt_bar");
            Place<MachineEntity>(w, "wall_chipper", R, 2, 0, Dir.S);
            w.Run(40);

            Assert.IsFalse(furnace.InBuf.ContainsKey("wall_dust"), "furnace stores fuel as points, not items");
            Assert.LessOrEqual(furnace.Fuel, 48, "fuel buffer never exceeds 48 points");
            w.TakeOutput(furnace);
            Assert.GreaterOrEqual(w.InvCount("dim_bar"), 1, $"furnace made bars (mixer {mixer.Status}, furnace {furnace.Status})");

            Assert.IsTrue(w.CompleteTech("fr3").Ok, "complete FR III");
            Assert.AreEqual(TechState.Open, w.StateOf("basic"));
            Assert.AreEqual(TechState.Open, w.StateOf("dimcomp"));
            Assert.IsEmpty(w.HandRecipes(), "hand recipes still locked");
            Assert.IsFalse(w.CanCompleteTech("dimcomp").Ok, "Dimensional Compression needs a core (checked)");
        }
    }
}
