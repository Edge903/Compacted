using System;

namespace Compacted.Sim
{
    // Notifications for the view layer (sound, effects). Not saved; listeners re-attach after a load.
    public sealed partial class World
    {
        /// <summary>A block was placed (any kind).</summary>
        public event Action<Entity> BlockPlaced;

        /// <summary>A tech node was completed by the player (not by the debug "complete all").</summary>
        public event Action<string> TechCompleted;

        /// <summary>A machine or multiblock core finished one craft.</summary>
        public event Action<Entity> CraftCompleted;

        /// <summary>The player took items from a machine or output bus by hand (entity, item count).</summary>
        public event Action<Entity, int> ItemsTakenByHand;
    }
}
