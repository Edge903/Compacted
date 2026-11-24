using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    [CreateAssetMenu(menuName = "Compacted/Multiblock", fileName = "NewMultiblock")]
    public sealed class MultiblockDefinition : Definition
    {
        public BuildingDefinition core;
        [Tooltip("Recipe group the formed structure runs.")]
        public string group;
        [Tooltip("Footprint; forms in any rotation, never mirrored.")]
        public Vector2Int size = new Vector2Int(2, 2);
        [Tooltip("Every tile must be one of these blocks, with counts in [min, max].")]
        public List<BlockCount> counts = new List<BlockCount>();
        [Tooltip("Each pair: some A must touch some B.")]
        public List<BlockPair> adjacent = new List<BlockPair>();
    }
}
