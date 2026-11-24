using System;
using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    // ScriptableObject definitions: the editable source of all game content.
    // ContentDatabase.Build() turns them into the simulation's plain ContentSet.
    // Ids are the stable keys used by saves; never rename an id once content ships.

    public enum BrowserTab { Logistics, Machines, Other }

    [Serializable]
    public struct ItemAmount
    {
        public ItemDefinition item;
        public int amount;
        public ItemAmount(ItemDefinition item, int amount) { this.item = item; this.amount = amount; }
    }

    [Serializable]
    public struct FluidAmount
    {
        public FluidDefinition fluid;
        [Tooltip("Millilitres")] public float ml;
        public FluidAmount(FluidDefinition fluid, float ml) { this.fluid = fluid; this.ml = ml; }
    }

    [Serializable]
    public struct BlockCount
    {
        public BuildingDefinition block;
        public int min;
        public int max;
        public BlockCount(BuildingDefinition block, int min, int max) { this.block = block; this.min = min; this.max = max; }
    }

    [Serializable]
    public struct BlockPair
    {
        public BuildingDefinition a;
        public BuildingDefinition b;
        public BlockPair(BuildingDefinition a, BuildingDefinition b) { this.a = a; this.b = b; }
    }

    /// <summary>Swappable look of a block or item: abstract shape art, no text labels (requirements §1.1).</summary>
    [Serializable]
    public struct Visual
    {
        [Tooltip("Optional sprite. When empty, the view draws a simple shape in the base color.")]
        public Sprite sprite;
        public Color color;
        public Visual(Color color) { sprite = null; this.color = color; }
    }

    public abstract class Definition : ScriptableObject
    {
        [Tooltip("Stable key used by saves and other definitions.")]
        public string id;
        public string displayName;
    }
}
