using System.Linq;
using NUnit.Framework;

namespace Compacted.Sim.Tests
{
    /// <summary>Fuel, fluids, multiblocks, tech and save/load (prototype check groups 2–6), plus core rules.</summary>
    public class RuleTests : SimTestBase
    {
        // ---------- fuel ----------
        [Test]
        public void FuelBufferRefusesOverflow()
        {
            var w = NewWorld(freeBuild: true);
            var furnace = Place<MachineEntity>(w, "furnace", w.RootId, 1, 1);
            w.SetRecipe(furnace, "smelt_bar");
            furnace.Fuel = 42;
            w.AddInv("en_scrap", 5);
            w.AddInv("wall_dust", 10);
            Assert.AreEqual(0, w.InsertFromInventory(furnace, "en_scrap"), "refuses scrap (8) that would overflow 48");
            Assert.AreEqual(6, w.InsertFromInventory(furnace, "wall_dust"), "still takes dust up to 48");
            Assert.AreEqual(48, furnace.Fuel);
        }

        // ---------- fluids ----------
        [Test]
        public void PipesJoinAcrossFluidPortAndSplitWhenOff()
        {
            var w = NewWorld(freeBuild: true);
            int R = w.RootId;
            Place<MachineEntity>(w, "condenser", R, 0, 1, Dir.E);
            var outside = Place<PipeEntity>(w, "pipe", R, 1, 1);
            var box = Place<ControllerEntity>(w, "ctrl3", R, 2, 1);
            w.SetPortMode(box, Dir.W, PortMode.Fluid);
            Place<PipeEntity>(w, "pipe", box.Inner, 0, 1);
            Place<PipeEntity>(w, "pipe", box.Inner, 1, 1);
            var mixer = Place<MachineEntity>(w, "mixer", box.Inner, 2, 1, Dir.S);
            w.SetRecipe(mixer, "mix_clay");
            w.Step();

            var seg = w.SegmentOf(outside);
            Assert.AreEqual(3, seg.Pipes.Count, "pipes join across the Fluid I/O port into one segment");
            Assert.AreEqual(3000, seg.Capacity, "1 L per pipe");
            w.Run(20);
            Assert.Greater(mixer.FluidIn.TryGetValue("water", out var water) ? water : 0, 0, "mixer inside the box received water through the port");

            w.SetPortMode(box, Dir.W, PortMode.Off);
            w.Step();
            Assert.AreEqual(1, w.SegmentOf(outside).Pipes.Count, "turning the port off splits the segment again");
        }

        [Test]
        public void SegmentPullAndPushRules()
        {
            var w = NewWorld();
            var half = new Segment { Amount = 500, Capacity = 1000, Fluid = "water" };
            Assert.AreEqual(50, w.PullFromSegment(half, 1e9), 1e-9, "50% full segment gives 50 ml per pull");
            var almostEmpty = new Segment { Amount = 5, Capacity = 1000, Fluid = "water" };
            Assert.AreEqual(5, w.PullFromSegment(almostEmpty, 1e9), 1e-9, "under 1% full, everything drains");
            Assert.AreEqual(0, almostEmpty.Amount);
            Assert.IsNull(almostEmpty.Fluid);
            var nearlyFull = new Segment { Amount = 950, Capacity = 1000, Fluid = "water" };
            Assert.AreEqual(50, w.PushToSegment(nearlyFull, "water", 1000), 1e-9, "push is capped by the room left");
            var empty = new Segment { Amount = 0, Capacity = 1000 };
            Assert.AreEqual(100, w.PushToSegment(empty, "water", 1000), 1e-9, "push is capped at 100 ml per tick");
        }

        // ---------- multiblocks ----------
        [Test]
        public void DimensionCompactorFormsCraftsAndBreaks()
        {
            var w = NewWorld(freeBuild: true);
            int R = w.RootId;
            foreach (var id in new[] { "fr1", "fr2", "fr3" }) w.CompleteTech(id, force: true);
            var core = Place<CoreEntity>(w, "compactor_core", R, 0, 0);
            Place<PartEntity>(w, "dim_coil", R, 1, 0);
            var inBus = Place<HatchEntity>(w, "input_bus", R, 0, 1, Dir.W);
            var outBus = Place<HatchEntity>(w, "output_bus", R, 1, 1, Dir.S);
            w.Step();
            Assert.IsNotNull(core.Formed, "compactor forms");
            Assert.AreEqual("compact_ctrl3", core.Recipe, "compactor recipe auto-selected");

            w.AddInv("dim_bar", 40);
            w.AddInv("energy_rod", 5);
            w.InsertFromInventory(inBus, "dim_bar");
            w.InsertFromInventory(inBus, "energy_rod");
            Assert.GreaterOrEqual(inBus.Buf["dim_bar"], 32);
            Assert.GreaterOrEqual(inBus.Buf["energy_rod"], 4);
            Assert.IsFalse(w.Accept(inBus, "dim_bar", Dir.E), "input bus refuses items from a side it doesn't face");

            w.Run(21);
            Assert.AreEqual(1, outBus.Buf.TryGetValue("ctrl3", out var made) ? made : 0, $"3×3 controller in the output bus ({core.Status})");

            w.TakeOutput(outBus);
            w.AddInv("dim_bar", 30);
            w.AddInv("energy_rod", 4);
            w.InsertFromInventory(inBus, "dim_bar");
            w.InsertFromInventory(inBus, "energy_rod");
            w.Run(5);
            Assert.IsTrue(core.Crafting, "second craft is running");
            int barsBefore = (inBus.Buf.TryGetValue("dim_bar", out var b) ? b : 0) + w.InvCount("dim_bar");
            Destroy(w, R, 1, 0);   // remove the coil
            w.Step();
            Assert.IsFalse(core.Crafting, "breaking the structure stops the craft");
            Assert.IsNull(core.Formed);
            Assert.AreEqual(barsBefore, (inBus.Buf.TryGetValue("dim_bar", out var b2) ? b2 : 0) + w.InvCount("dim_bar"), "consumed inputs are lost, the rest stay");
        }

        [Test]
        public void WrongLayoutDoesNotForm()
        {
            var w = NewWorld(freeBuild: true);
            int R = w.RootId;
            var core = Place<CoreEntity>(w, "compactor_core", R, 0, 0);
            Place<HatchEntity>(w, "input_bus", R, 1, 0);
            Place<HatchEntity>(w, "output_bus", R, 0, 1);
            Place<PartEntity>(w, "dim_coil", R, 1, 1);   // diagonal: not touching the core
            w.Step();
            Assert.IsNull(core.Formed);
        }

        // ---------- tech ----------
        [Test]
        public void TechRequiresIsCheckedNotTakenAndUnlocksRecipes()
        {
            var w = NewWorld();
            foreach (var id in new[] { "fr1", "fr2", "fr3" }) w.CompleteTech(id, force: true);
            w.AddInv("compactor_core", 1);
            Assert.IsTrue(w.CompleteTech("dimcomp").Ok);
            Assert.AreEqual(1, w.InvCount("compactor_core"), "core was not taken");
            Assert.IsFalse(w.RecipeUnlocked("asm_core"), "assembler core recipe locked before the Assembler tech");
            foreach (var id in new[] { "basic", "mining", "assembler" }) w.CompleteTech(id, force: true);
            Assert.IsTrue(w.RecipeUnlocked("asm_core"));
            Assert.IsTrue(w.RecipeUnlocked("h_assembler"));
            w.AddInv("ctrl3", 1);
            Assert.IsTrue(w.CompleteTech("expansion").Ok, "Time for Expansion completes");
            w.AddInv("dim_bar", 8);
            w.AddInv("energy_rod", 1);
            Assert.IsTrue(w.HandCraft("h_assembler").Ok);
            Assert.AreEqual(1, w.InvCount("assembler"));
        }

        [Test]
        public void HandOverIsAllAtOnce()
        {
            var w = NewWorld();
            w.AddInv("wall_dust", 9);
            Assert.IsFalse(w.CompleteTech("fr1").Ok, "9 of 10 isn't enough");
            Assert.AreEqual(9, w.InvCount("wall_dust"), "nothing was taken");
        }

        // ---------- save / load ----------
        [Test]
        public void SaveLoadRoundTrip()
        {
            var w = NewWorld(freeBuild: true);
            int R = w.RootId;
            Place<PipeEntity>(w, "pipe", R, 0, 0);
            var p2 = Place<PipeEntity>(w, "pipe", R, 1, 0);
            var box = Place<ControllerEntity>(w, "ctrl3", R, 2, 2);
            w.SetPortMode(box, Dir.N, PortMode.In);
            box.Name = "Smelter box";
            var furnace = Place<MachineEntity>(w, "furnace", box.Inner, 1, 1, Dir.W);
            w.SetRecipe(furnace, "smelt_bar");
            furnace.Fuel = 12;
            w.CompleteTech("fr1", force: true);
            w.Step();
            var seg = w.SegmentOf(p2);
            seg.Fluid = "water";
            seg.Amount = 1500;

            var json = w.ToJson();
            StringAssert.Contains("\"Version\":1", json);
            var w2 = World.FromJson(EarlyGameContent.Build(), json);
            w2.Step();

            var p2b = w2.Get<PipeEntity>(p2.Id);
            Assert.AreEqual(1500, w2.SegmentOf(p2b).Amount, 1e-6, "fluid survives save/load");
            var box2 = w2.Get<ControllerEntity>(box.Id);
            Assert.AreEqual("Smelter box", box2.Name);
            Assert.AreEqual(PortMode.In, box2.Ports[(int)Dir.N]);
            var f2 = w2.Get<MachineEntity>(furnace.Id);
            Assert.AreEqual("smelt_bar", f2.Recipe);
            Assert.AreEqual(12, f2.Fuel);
            Assert.AreEqual(Dir.W, f2.Dir);
            CollectionAssert.Contains(w2.TechDone, "fr1");
            CollectionAssert.AreEqual(w.Entities.Select(e => e.Id).ToList(), w2.Entities.Select(e => e.Id).ToList(), "tick order is preserved");
        }

        [Test]
        public void LoadRejectsOtherVersions()
        {
            var w = NewWorld();
            var json = w.ToJson().Replace("\"Version\":1", "\"Version\":99");
            Assert.Throws<System.NotSupportedException>(() => World.FromJson(EarlyGameContent.Build(), json));
        }

        // ---------- core rules ----------
        [Test]
        public void NestingRule()
        {
            var w = NewWorld(freeBuild: true);
            var box = Place<ControllerEntity>(w, "ctrl3", w.RootId, 1, 1);
            Assert.IsFalse(w.CanPlace("ctrl3", box.Inner, 1, 1).Ok, "a 3×3 controller doesn't fit inside a 3×3 dimension");
        }

        [Test]
        public void ItemsFlowThroughControllerPorts()
        {
            var w = NewWorld(freeBuild: true);
            int R = w.RootId;
            var chipper = Place<MachineEntity>(w, "wall_chipper", R, 0, 1, Dir.E);
            var box = Place<ControllerEntity>(w, "ctrl3", R, 1, 1);
            w.SetPortMode(box, Dir.W, PortMode.In);
            var comp = Place<MachineEntity>(w, "compressor", box.Inner, 0, 1, Dir.S);
            w.SetRecipe(comp, "press_scrap");
            w.Run(10);
            Assert.IsTrue(comp.OutBuf.ContainsKey("dim_scrap") || comp.Crafting, "dust entered the box through the west port and was compressed");
        }

        [Test]
        public void BeltsMoveItemsAndJamWhenBlocked()
        {
            var w = NewWorld(freeBuild: true);
            int R = w.RootId;
            Place<MachineEntity>(w, "wall_chipper", R, 0, 0, Dir.E);
            var belt = Place<BeltEntity>(w, "belt", R, 1, 0, Dir.E);
            var comp = Place<MachineEntity>(w, "compressor", R, 2, 0, Dir.S);
            w.SetRecipe(comp, "press_scrap");
            w.Run(6);
            Assert.Greater(comp.InBuf.Count + comp.OutBuf.Count + (comp.Crafting ? 1 : 0), 0, "belt delivered dust to the compressor");
            Assert.Greater(w.BeltRate(belt), 0);

            var w2 = NewWorld(freeBuild: true);
            Place<MachineEntity>(w2, "wall_chipper", w2.RootId, 0, 0, Dir.E);
            var dead = Place<BeltEntity>(w2, "belt", w2.RootId, 1, 0, Dir.E);   // nothing after it
            w2.Run(3);
            Assert.AreEqual("wall_dust", dead.Item, "item waits at the end of the belt");
            Assert.AreEqual(1, dead.Prog, 1e-9);
        }

        [Test]
        public void DestroyRefundsAndSkipsNonEmptyControllers()
        {
            var w = NewWorld(freeBuild: true);
            int R = w.RootId;
            var box = Place<ControllerEntity>(w, "ctrl3", R, 0, 0);
            Place<PartEntity>(w, "dim_coil", box.Inner, 1, 1);
            var belt = Place<BeltEntity>(w, "belt", R, 1, 0);
            belt.Item = "wall_dust";
            var plan = w.PlanDestroy(R, 0, 0, 2, 2);
            Assert.AreEqual(1, plan.Skipped, "non-empty controller skipped");
            Assert.IsFalse(plan.Refunds.ContainsKey("belt"), "belts are free and not refunded");
            Assert.AreEqual(1, plan.Refunds["wall_dust"], "items on belts are refunded");
            w.Inventory["wall_dust"] = 100;
            plan = w.PlanDestroy(R, 0, 0, 2, 2);
            Assert.AreEqual(1, plan.Lost["wall_dust"], "overflow past the cap is reported as lost");
        }
    }
}
