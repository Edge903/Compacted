using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    [CreateAssetMenu(menuName = "Compacted/Recipe", fileName = "NewRecipe")]
    public sealed class RecipeDefinition : Definition
    {
        public RecipeType type;
        [Tooltip("Machine recipes: the machine or multiblock group that runs it.")]
        public string group;
        [Tooltip("Machine recipes: needs a tech unlock before machines can run it.")]
        public bool locked;
        [Tooltip("Seconds at speed 1. Ignored for hand recipes (instant).")]
        [Min(0)] public float time = 1;
        public List<ItemAmount> inputs = new List<ItemAmount>();
        public List<FluidAmount> fluidInputs = new List<FluidAmount>();
        [Tooltip("Fuel points per craft; 0 = none.")]
        [Min(0)] public int fuel;
        public List<ItemAmount> outputs = new List<ItemAmount>();
        public List<FluidAmount> fluidOutputs = new List<FluidAmount>();
    }
}
