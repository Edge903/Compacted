# Project layout

Everything made for Compacted lives under `Assets/_Project/`. Third-party and package content stays outside it.

| Folder | Assembly | Contents |
|---|---|---|
| `Scripts/Sim/` | `Compacted.Sim` | The simulation: grid, dimensions, I/O rule, machines, fluids, fuel, multiblocks, tech, save/load. Plain C# with **no UnityEngine references** (`noEngineReferences`), ported from `prototype/sim.js`. |
| `Scripts/Data/` | `Compacted.Data` | ScriptableObject definitions (items, fluids, buildings, recipes, multiblocks, tech nodes, tuning) and `ContentDatabase`, which builds the simulation's `ContentSet` and validates content. |
| `Scripts/View/` | `Compacted.View` *(phase 4)* | Rendering, input and UI Toolkit screens. Reads the simulation; never holds game rules. |
| `Scripts/Editor/` | `Compacted.Editor` | Editor tools under the **Compacted → Content** menu: validate content, collect new definition assets into the database, and regenerate the early-game content. |
| `Tests/EditMode/` | `Compacted.Sim.Tests` | EditMode tests for the simulation, including the checks ported from the prototype. |
| `Tests/EditMode/Data/` | `Compacted.Data.Tests` | Checks that the content assets are valid and match `docs/early-game.md`. |
| `Content/` | — | Content assets by type, plus `ContentDatabase.asset` and `Tuning.asset`. |

## Editing content

- **Change a value:** edit the asset in the Inspector, then run **Compacted → Content → Validate Content**.
- **Add a definition:** right-click → **Create → Compacted → …**, give it a unique `id`, then run **Collect Definitions Into Database**.
- **Ids are save keys:** a building's item must have the same id as the building, and ids shouldn't change once saves exist.
- **The spec check:** `AssetsMatchTheEarlyGameSpec` fails when the assets and `EarlyGameContent` (the spec in code) disagree. After an intended content change, update both, or retire the test once the assets become the only source.
- **Regenerating:** **Generate Early-Game Content** overwrites the early-game assets with the spec values, in place, keeping their GUIDs.
| `Art/`, `UI/` | — | Sprites, and UXML/USS for UI Toolkit. |

Dependency direction: `View → Data → Sim`. `Sim` depends on nothing. Assemblies marked with a phase are created when their first code lands.

Requirements and content specs: `docs/requirements.md` and `docs/early-game.md` at the repository root.
