# Project layout

Everything made for Compacted lives under `Assets/_Project/`. Third-party and package content stays outside it.

| Folder | Assembly | Contents |
|---|---|---|
| `Scripts/Sim/` | `Compacted.Sim` | The simulation: grid, dimensions, I/O rule, machines, fluids, fuel, multiblocks, tech, save/load. Plain C# with **no UnityEngine references** (`noEngineReferences`), ported from `prototype/sim.js`. |
| `Scripts/Data/` | `Compacted.Data` *(phase 3)* | ScriptableObject definitions (items, fluids, buildings, recipes, multiblocks, tech nodes, tuning) and the content database. Turns assets into the simulation's definitions. |
| `Scripts/View/` | `Compacted.View` *(phase 4)* | Rendering, input and UI Toolkit screens. Reads the simulation; never holds game rules. |
| `Scripts/Editor/` | `Compacted.Editor` *(phase 3)* | Editor tools, such as content validation. |
| `Tests/EditMode/` | `Compacted.Sim.Tests` | EditMode tests for the simulation, including the checks ported from the prototype. |
| `Content/` | — | Content assets (definitions) by type. |
| `Art/`, `UI/` | — | Sprites, and UXML/USS for UI Toolkit. |

Dependency direction: `View → Data → Sim`. `Sim` depends on nothing. Assemblies marked with a phase are created when their first code lands.

Requirements and content specs: `docs/requirements.md` and `docs/early-game.md` at the repository root.
