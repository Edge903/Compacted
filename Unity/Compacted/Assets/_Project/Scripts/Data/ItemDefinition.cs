using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    [CreateAssetMenu(menuName = "Compacted/Item", fileName = "NewItem")]
    public sealed class ItemDefinition : Definition
    {
        public Visual visual = new Visual(Color.white);
        [Tooltip("Fuel points when burned; 0 = not a fuel.")]
        [Min(0)] public int fuel;
    }
}
