using System.Runtime.CompilerServices;

// The simulation is plain C# with no UnityEngine references (see the asmdef).
// Tests may reach internal members.
[assembly: InternalsVisibleTo("Compacted.Sim.Tests")]
