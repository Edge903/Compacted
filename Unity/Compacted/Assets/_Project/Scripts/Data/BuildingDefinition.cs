using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    [CreateAssetMenu(menuName = "Compacted/Building", fileName = "NewBuilding")]
    public sealed class BuildingDefinition : Definition
    {
        public BuildingKind kind;
        public BrowserTab browserTab;
        public Visual visual = new Visual(Color.gray);

        [Tooltip("Placed without an item and never refunded (belts, pipes).")]
        public bool free;
        [Tooltip("The item this block is placed from and refunded as. Its id must match this building's id.")]
        public ItemDefinition item;
        public Placement placement;

        [Header("Machine")]
        [Tooltip("Recipe group this machine runs.")]
        public string machineGroup;
        [Tooltip("Output buffer cap per item; 0 = use the tuning default.")]
        [Min(0)] public int outputCap;

        [Header("Controller")]
        [Tooltip("Interior size: 3, 5, 7, 9, 11 or 13.")]
        public int controllerSize;

        [Header("Multiblock core")]
        public MultiblockDefinition multiblock;

        [Header("Hatch")]
        public HatchIo hatchIo;
    }
}
