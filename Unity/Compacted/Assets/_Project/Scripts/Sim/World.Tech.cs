using System.Collections.Generic;
using System.Linq;

namespace Compacted.Sim
{
    public enum TechState { Hidden, Open, Done }

    // Tech tree, recipe unlocks and hand crafting. See requirements §5.6 and §7.2.
    public sealed partial class World
    {
        public readonly List<string> TechDone = new List<string>();
        public readonly List<string> UnlockedHand = new List<string>();
        public readonly List<string> UnlockedMachine = new List<string>();

        public bool RecipeUnlocked(string recipeId)
        {
            var r = C.Recipes[recipeId];
            if (r.Type == RecipeType.Hand) return UnlockedHand.Contains(recipeId);
            return !r.Locked || UnlockedMachine.Contains(recipeId);
        }

        public List<string> HandRecipes() =>
            C.Recipes.Values.Where(r => r.Type == RecipeType.Hand && RecipeUnlocked(r.Id)).Select(r => r.Id).ToList();

        public TechState StateOf(string techId)
        {
            if (TechDone.Contains(techId)) return TechState.Done;
            return C.Tech[techId].Prereqs.All(TechDone.Contains) ? TechState.Open : TechState.Hidden;
        }

        public Result CanCompleteTech(string techId)
        {
            var t = C.Tech[techId];
            if (StateOf(techId) != TechState.Open) return Result.Fail("Not available yet");
            if (!HasAll(t.Cost)) return Result.Fail("Not enough items to hand over");
            if (!HasAll(t.Requires)) return Result.Fail("Missing required items");
            return Result.Success;
        }

        /// <summary>
        /// Hand over the cost (all at once), check requirements, then apply rewards.
        /// `lost` gets granted items that didn't fit the inventory cap. force = debug: skip checks and cost.
        /// </summary>
        public Result CompleteTech(string techId, out Dictionary<string, int> lost, bool force = false)
        {
            lost = new Dictionary<string, int>();
            if (!force) { var chk = CanCompleteTech(techId); if (!chk.Ok) return chk; }
            var t = C.Tech[techId];
            if (!force) foreach (var kv in t.Cost) TakeInv(kv.Key, kv.Value);
            foreach (var kv in t.Grant) { int l = AddInv(kv.Key, kv.Value); if (l > 0) lost[kv.Key] = l; }
            foreach (var r in t.UnlockHand) if (!UnlockedHand.Contains(r)) UnlockedHand.Add(r);
            foreach (var r in t.UnlockMachine) if (!UnlockedMachine.Contains(r)) UnlockedMachine.Add(r);
            if (!TechDone.Contains(techId)) TechDone.Add(techId);
            return Result.Success;
        }

        public Result CompleteTech(string techId, bool force = false) => CompleteTech(techId, out _, force);

        /// <summary>Instant hand craft of one recipe output.</summary>
        public Result HandCraft(string recipeId)
        {
            if (!C.Recipes.TryGetValue(recipeId, out var r) || r.Type != RecipeType.Hand || !RecipeUnlocked(recipeId))
                return Result.Fail("Recipe is locked");
            if (!HasAll(r.In)) return Result.Fail("Not enough materials");
            if (!r.Out.All(kv => InvCount(kv.Key) + kv.Value <= C.Tuning.InventoryCap))
                return Result.Fail("Inventory is full for that item");
            foreach (var kv in r.In) TakeInv(kv.Key, kv.Value);
            foreach (var kv in r.Out) AddInv(kv.Key, kv.Value);
            return Result.Success;
        }
    }
}
