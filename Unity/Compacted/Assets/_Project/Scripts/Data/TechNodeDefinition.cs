using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    [CreateAssetMenu(menuName = "Compacted/Tech Node", fileName = "NewTechNode")]
    public sealed class TechNodeDefinition : Definition
    {
        [Tooltip("Column and row in the tech tree view.")]
        public Vector2Int position;
        public List<TechNodeDefinition> prerequisites = new List<TechNodeDefinition>();
        [Tooltip("Handed over all at once.")]
        public List<ItemAmount> cost = new List<ItemAmount>();
        [Tooltip("Checked only, never taken.")]
        public List<ItemAmount> requires = new List<ItemAmount>();
        [Tooltip("Given once on completion.")]
        public List<ItemAmount> grants = new List<ItemAmount>();
        public List<RecipeDefinition> unlockHandRecipes = new List<RecipeDefinition>();
        public List<RecipeDefinition> unlockMachineRecipes = new List<RecipeDefinition>();
    }
}
