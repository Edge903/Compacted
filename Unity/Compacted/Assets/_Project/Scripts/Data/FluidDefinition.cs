using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    [CreateAssetMenu(menuName = "Compacted/Fluid", fileName = "NewFluid")]
    public sealed class FluidDefinition : Definition
    {
        public Color color = new Color(0.3f, 0.66f, 0.94f);
    }
}
