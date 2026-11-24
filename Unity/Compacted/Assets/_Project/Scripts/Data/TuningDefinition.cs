using System.Collections.Generic;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.Data
{
    [CreateAssetMenu(menuName = "Compacted/Tuning", fileName = "Tuning")]
    public sealed class TuningDefinition : ScriptableObject
    {
        public int tickRate = 20;
        [Tooltip("Seconds for an item to cross one belt tile.")] public float beltTime = 0.5f;
        public int inputBufferMult = 3;
        public int outputCap = 10;
        public int fuelBufferMult = 3;
        [Tooltip("ml per fluid in a machine")] public float fluidBuffer = 10000;
        [Tooltip("ml per pipe")] public float pipeCapacity = 1000;
        [Tooltip("max ml pushed or pulled per tick")] public float fluidPerTick = 100;
        public int inventoryCap = 100;

        public Tuning ToSim() => new Tuning
        {
            TickRate = tickRate, BeltTime = beltTime, InputBufferMult = inputBufferMult, OutputCap = outputCap,
            FuelBufferMult = fuelBufferMult, FluidBuffer = fluidBuffer, PipeCapacity = pipeCapacity,
            FluidPerTick = fluidPerTick, InventoryCap = inventoryCap,
        };
    }
}
