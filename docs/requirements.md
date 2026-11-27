# Compacted — Requirements (v0.1)

*Status: draft, 2026-11-12. "TBD" means not yet decided; "Proposed" means suggested but not confirmed.*

## 1. Vision

Compacted is a factory/automation game about **limited space**. Many factory games are about scaling up. This one is about solving automation inside tight spatial limits. The main challenge comes from the constraints: small grids, 4 ports per dimension, nesting rules, and scarce unique parts.

- **Production target:** Unity 2D.
- **Prototyping:** web + JS, used to refine requirements. Prototypes are built only when explicitly requested.

### 1.1 Production decisions (2026-11-24)

- **Engine:** Unity 6.3 (6000.3.27f2), URP with the 2D Renderer. The project is at `Unity/Compacted/`.
- **UI:** UI Toolkit.
- **Platform:** PC, Windows first.
- **Art style:**
  - Abstract and simple, like the web prototype.
  - **No text labels** on blocks; machines and blocks are told apart by shape, color and icon.
  - Each machine and block gets its own design later.
  - Visuals must be **easy to swap**: each building's look comes from data (its definition), not from code.

## 2. Space & grid

| Rule | Value |
|---|---|
| Root dimension (where the player starts) | Fixed **3×3** (9 placeable tiles) |
| Controller interior sizes | (2n+1)², by tier: **3×3, 5×5, 7×7, 9×9, 11×11, 13×13** (6 tiers) |
| Walls | Sit **outside** the grid; all N×N tiles are placeable |
| Tile occupancy | One building per tile (multiblocks take several tiles) |

## 3. Dimension Controller

- Takes **1×1** in its parent dimension and contains its own unique interior dimension.
- Its interior size is set by its tier (§2).
- **Rotation:** fixed, no rotation. Keep it possible to add rotation later.
- **Renaming:** the player can rename a controller. The name shows in the breadcrumb (§8.1).
- **Breaking:** a controller can't be destroyed while its interior isn't empty. The player has to clear the inside first.

### 3.1 Ports

- **One fixed port per wall**, at the middle of the wall: 4 ports in total (N/E/S/W).
- Each port is next to the middle edge tile of the interior, and to the matching side tile outside the controller.
- **Mapping:** something entering the interior's north port comes out on the controller's north side outside, and vice versa. The same goes for each direction.
- **Modes:** each port is set **manually** by the player; nothing is set automatically. It has one of these modes:
  - **Off.**
  - **Item input / item output:** carries items in one direction. Outside-input means inside-output, and vice versa.
  - **Fluid I/O:** carries fluid through the wall.
- **Fluid I/O ports:**
  - The pipes inside and outside a Fluid I/O port **join into one segment** (§5.3). This also works across several nested ports.
  - Fluid flows **both ways**.
  - The port itself has **no throughput limit**.
- **Item ports and fluid:** an item-mode port does nothing for fluids, so no fluid passes through it.
- **One kind per port:** a port carries either items or fluid, never both.

### 3.2 Nesting

- A controller can be placed inside a dimension only if that dimension is **strictly bigger** than the controller's own interior.
  - Example: a 5×5 dimension can hold a 3×3 controller; a 3×3 dimension can't hold a 3×3 controller.
  - So 3×3 controllers can only hold machines, belts, and so on.
- **The root dimension is exempt:** it can hold controllers of any tier.
- This means there's no recursion, and nesting depth is bounded (at most 6 levels below the root).

## 4. Generic I/O rule

One rule covers **every** transfer: machines, belts, hatches, sources and ports.

1. A building tries to output an item in its output direction.
2. If the destination tile is **empty**, the output fails.
3. Otherwise, the destination is **eligible** only if all of these are true:
   - it accepts input from that side,
   - it accepts that **item type** (for example, it fits the machine's chosen recipe),
   - it has **buffer space**.
4. If it's eligible, the item transfers automatically. If not, the building **stops**: the item stays put, and the jam backs up the line.

Consequences:
- **Adjacent connections:** neighbouring controllers connect port to port, with no conduit needed.
- **Ports and machines:** a port can feed a machine directly.

## 5. Buildings

### 5.1 Single-block machines

- **One output side**, which the player can set.
- They **accept input from every other side** (3 sides).
- **Recipe:** chosen by the player **after placement**, in the machine's info popup (§8.1). It isn't chosen in the block browser.
  - A newly placed machine has no recipe.
  - *Proposed (in the prototype):* a machine with only one unlocked recipe, such as a source, starts with that recipe selected.

### 5.2 Belts

- Follow the same rule as machines. A belt's direction is its output side; it takes input from the other 3 sides.
- **Items on a belt can't be picked up**, except by destroying the belt (the items are then recovered).
- **Free to place:** belts need no inventory item and no crafting. The player can place them at any time, in any number. They don't appear in the item list.
  - Destroying a belt refunds only the item it was carrying, not the belt itself.

### 5.3 Pipes

- **Free to place,** like belts (§5.2).
- **Fluid model:** similar to Factorio's Fluid 2.0.
  - **Segments:** connected pipes form one **segment**. A segment behaves like a single tank; there's no pressure or flow along the pipe.
  - **One fluid per segment.**
  - **Capacity:** **1 L per pipe**, so a segment holds 1 L × its pipe count.
  - **Throughput:** **100 ml per tick** at most, which is **2 L/s** at 20 ticks per second.
  - **Fill level:** the game tracks one amount per segment and its **fill fraction**. Example: 6 L of water in a 9-pipe segment (9 L capacity) is **66% full**.
  - **Display:** each pipe shows its share of that fill. In the example, each pipe shows **666 ml**.
  - **Extraction:** every tick, each machine taking from the segment pulls **100 ml × fill fraction**. A 50% full segment gives **50 ml** per tick (1 L/s), and a full segment gives the maximum of **100 ml** per tick (2 L/s).
  - **Several consumers:** each consumer **pulls separately**, with its own pull every tick.
  - **Pushing in:** each producer pushes up to **100 ml per tick**, limited only by the room left in the segment. Unlike pulling, the fill fraction doesn't reduce it.
  - **Draining:** if a segment is **under 1%** full, the next pull takes everything left and the segment is empty. This stops tiny amounts from lingering forever.
  - **Across controllers:** pipes connected through Fluid I/O ports count as **one segment** (§3.1).
- **Machine fluid buffers:** **10 L** per fluid.
- **Mixed inputs:** a machine can take a fluid on one side and items on another (e.g. the mixer).
- **Amounts:** fluids are measured in ml/L and can't be carried by the player (§10.3).

### 5.4 Material sources

- **What they are:** machines whose recipes have no inputs. They produce **forever, at a fixed rate**. Examples: wall chipper, wall miner, atmospheric condenser.
- **Placement:** some sources can only be placed next to a wall (§5.9).
- **Content:** source types and materials are specified in [early-game.md](early-game.md).

### 5.5 Multiblock machines

Inspired by GregTech-style multiblocks.

- **Naming:** a multiblock's controlling block is always called a **core** (e.g. Compactor Core). This keeps it distinct from dimension controllers.
- **Structure:** a **core**, plus structural blocks (casings, coils and so on), plus **hatches** (item input/output; called **buses** for Dimensium tier).
- **Size and shape:** vary per machine. A design can be either:
  - **a fixed layout**, which says what goes in each cell, or
  - **a set of rules**, such as "2×2; 1 core; 1 coil next to the core; at least 1 input bus and 1 output bus; positions free".
- **Hatch positions:** the design defines where hatches may go, either replacing casings or placed freely.
- **Orientation:** a structure forms in any of the 4 rotations, never mirrored.
- **Hatch I/O:** hatches follow the generic I/O rule (§4), but **only through the side they face**. Their facing is set by rotating them (R) when placing.
  - An input hatch accepts only from its facing side.
  - An output hatch outputs only to its facing side.
- **Uses:**
  - Advanced production.
  - Higher-tier sources: either **more output of lower-tier materials** in the same space, or **enriching** them into higher-tier materials.
- *Design note:* a multiblock that exactly fills a controller's interior, with hatches on the middle edges, gives a "machine in a box" with no conduits.
- **TBD:** the specific multiblocks and their designs.

### 5.6 Hand crafting

- **Crafting window:** a large centered window (§8.1), opened from its right-side label. It lists every **unlocked hand recipe**.
- **Hand vs machine recipes:** hand recipes and machine recipes are separate recipes.
  - Content always says which one a recipe is.
  - Tech nodes unlock each kind separately (§7.2).
  - The current list is in [early-game.md](early-game.md).
- **Inputs and outputs:** crafting takes inputs from the inventory and puts the result in the inventory, within the 100 cap.
- **Display:** each row shows the output, the inputs with how many you have (missing ones are highlighted), and how many you can craft right now.
- **Instant:** hand crafting has no craft time and no queue.
- **One per click:** each **Craft** click makes one recipe's output. There's no bulk crafting.
- **Shortcut:** **C** opens the window.

### 5.7 Storage

- There are **no storage buildings.** Advanced logistics (such as item networks) are planned for later.

### 5.8 Fuel

- **What burns fuel:** some machines, such as the furnace. Their recipes need **fuel points** as well as normal inputs. Example: a bar needs 1 clay + 16 points.
- **Fuel values:** each fuel item has a value. Wall dust is 1, energetic dust 2, energetic scrap 8.
- **Mixing:** fuels can be mixed freely.
- **Leftover points:** they **carry over** to the next recipe.
- **Fuel buffer:** holds **points, not items**. A fuel item is converted to its points the moment it's accepted. Points are drawn from the buffer when a recipe starts.
- **Buffer size:** **3× the recipe's fuel cost**. For the furnace that's 3 × 16 = **48 points**.
- **Eligibility:** a machine **refuses** a fuel item whose points would push the buffer over its size. For example, with 42 points stored, it refuses energetic scrap (8) but still takes wall dust (1) or energetic dust (2).

### 5.9 Placement rules

- **Default:** a building can be placed on any empty tile.
- **Wall-adjacent buildings** (e.g. wall chipper, wall miner) need a tile that touches at least one wall:
  - The root's border counts as a wall.
  - Port tiles are allowed.
  - Corners give no bonus.
  - The output side is free.

## 6. Player & inventory

- **Hand transfer:** the player can pick up any item and place it directly, **by hand only**.
  - They can take items from machine outputs and put items into machine inputs by hand.
  - On-belt items: see §5.2.
- **Inventory cap:** **100 per item type**. Expansions come from the tech tree.
- **Start:** one **Wall chipper** in the inventory, in an empty 3×3 root dimension ([early-game.md](early-game.md)).

## 7. Progression

### 7.1 Core loop

Place sources → process through machines and controllers → collect by hand → craft better machines and controllers → unlock tech → … → **Dimension Breaker**.

### 7.2 Tech tree

- Acts as a **guideline** for the player.
- **Structure:** a graph. A node can need several prerequisites, and it opens only when **all** of them are complete.
- **Visibility:**
  - Nodes that aren't open yet are **hidden**.
  - The **lines** leading from completed nodes to them stay visible, so the player can see that something is next.
- **Hand-over cost:** items spent from the inventory, **all at once** (no partial deposits).
  - Since the inventory cap is 100 per item, bigger costs need inventory expansions first. Expansion nodes act as natural gates.
- **Requirement:** items that must be in the inventory but are **only checked**, not taken. Example: "have a Compactor Core".
- **Rewards can include:**
  - Unlocking **hand recipes** and **machine recipes**, listed separately.
  - Unlocking other nodes.
  - **Item grants**, given once on completion.
  - Nothing at all: a node can be a visual checkpoint.
  - **Unique items/buildings:** **uncraftable**; the only way to get them is completing a tech tree node. They keep resources scarce; for example, a component for an endgame multiblock source, so the player can't spam endgame sources.
- **Proposed:** dismantling a building made with a unique part gives the part back. Relocating is allowed, and spamming is still prevented.
- **Content:** the early-game nodes are in [early-game.md](early-game.md).

### 7.3 Ending

- Building a **Dimension Breaker** in the root dimension ends the game.
- **TBD:** its requirements.

## 8. Presentation

- Each dimension is shown on its **own screen**.
- Entering and leaving a controller uses a **zoom in/out transition**.

### 8.1 Input & UI

**Screen layout**
- **Breadcrumb** (top left): the current dimension path, e.g. Root › Smelter box. It uses controller names (§3).
- **Leave button** (top left, next to the breadcrumb): exits to the parent dimension. Hidden in the root dimension.
- **Port icons:** each wall's port shows a simple colored icon for its direction, e.g. an arrow colored for in, out or off. No text labels.
- **Placeables toolbar** (bottom). The rightmost button is **Destroy**.
- **Right-side labels:** **Materials**, **Tech tree** and **Crafting**. Clicking a label toggles its window.
- **Large centered windows:** the **block browser**, **Materials**, **Tech tree** and **Crafting** open as a large window in the center of the screen, not as small panels.
  - **Only one is open at a time.** Opening one closes the others.
  - Each has a close button, and Esc closes it.
  - The game keeps running behind them.

**Placing**
- Clicking a toolbar slot selects that block. The **block browser** opens **only** when you click the already-selected slot, even if the slot is empty.
- Pressing a slot's number key (1–9) selects it; pressing the same key again **deselects** it. Number keys never open the browser.
- **Block browser:** has 3 tabs — **Logistics**, **Machines**, **Other**.
  - It only decides **which block** goes in the slot. No recipe or other configuration happens here.
- **Preview:** while a block is selected, a preview follows the mouse.
  - **R** rotates the preview. This sets a belt's direction or a machine's output side. Controllers don't rotate (§3).
- **Left click:** places the selected block.
- **Right click:** deselects it.
- **Description window:** while a toolbar block is selected, a window just above the toolbar shows its name and description.
  - Descriptions are multi-line and written per block (`BuildingDefinition.description`).
  - Until a block's description is written, the window shows "Description placeholder".
  - The window hides when nothing is selected.

**Inspecting**
- **Window placement:** both windows are fixed to the screen and don't move with the camera.
  - The info popup sits at **center-right**. If it would reach the I/O window, it moves up to sit just above it.
  - The I/O window (ports or output side) sits at **bottom-right**.
- With nothing selected, clicking a placed building opens an **info popup**:
  - **Machine:** current recipe (set or changed here), buffer contents, process status (working, idle, no recipe, output blocked).
    - A separate **output side window** opens next to it. It uses the same layout as the controller's port window: the machine as a square with 4 side buttons around it.
      - The output side is amber, with an arrow pointing out.
      - The other three sides are green input sides, with arrows pointing in.
    - Click a side to make it the output.
  - **Belt:** current item on the belt and stats such as items/sec.
  - **Controller:** rename it, see its interior size and block count, and an **Enter** button.
    - A separate **port window** opens next to it. It shows the controller as a square with its 4 ports around it (N/E/S/W), each a round button colored and pointed by mode:
      - Input: green, arrow pointing into the box.
      - Output: amber, arrow pointing out.
      - Off: gray dash.
      - Fluid I/O: blue, with a droplet icon.
    - Left-click a port to cycle off → in → out → fluid → off; right-click goes back.
- **Entering a controller:** the Enter button in its popup, or **double-click** the controller.

**Destroying**
- **Destroy mode:** selected with the rightmost toolbar button.
- **Selecting targets:** click a single building, or drag a square area.
- **Confirmation:** a popup asks for the player's consent, with a quick summary of how many blocks will be removed.
- **Non-empty controllers** in the selected area are skipped, and the popup says how many were skipped.
- **Refunds:** destroyed blocks and their contents (buffers, items on belts) go back to the player's inventory. Free blocks (belts, pipes) refund only their contents.
  - Anything over the per-item cap (100) is **lost**.
  - The confirm popup shows a **warning** when some items will be lost, and says which ones and how many.

**Shortcuts**

| Key | Action |
|---|---|
| WASD | Move camera (eased; faster when zoomed out; can't pan far past the grid) |
| Mouse wheel | Zoom between 1× (the dimension fills the view) and 0.5×; the game starts at 0.5× (eased) |
| 1–9 | Select toolbar slot |
| R | Rotate preview |
| Q | Destroy mode |
| Tab | Materials panel |
| T | Tech tree panel |
| C | Crafting window |
| Esc | Close the open popup or panel; if none is open, pause and show the pause menu |

**Pause menu:** Resume, Save/Load, Settings, Quit.

### 8.2 Saving

- **Save files:** each save is its own **file**, so one world can have several saves.
- **Autosave:** runs on an interval, set in Settings. The default is **10 minutes**.
- **Versioning:** every save file stores a **format version**.
  - *Proposed:* a migration step that upgrades older versions when they're loaded, for example when content ids change.
- **Contents:** the full world state:
  - all dimensions and entities with their state,
  - the inventory,
  - tech tree progress,
  - the toolbar layout.
- **View state is restored:** loading puts you back in the same dimension, with the same camera position and toolbar selection.
- *Prototype note:* the web prototype saves to a single `localStorage` slot. It has no autosave, version or view state.

*Proposed (low priority):*
- **Belt lines:** drag to place a line of belts. With dimensions this small, it probably won't matter much.
- **Leave shortcut:** a key for leaving a controller, such as Backspace or E.

**Toolbar:** 9 slots, matching keys 1–9, with the Destroy button separate on the right.

### 8.3 Sound

- **Ambience:** the tracks in `Audio/Ambience` play as a looping playlist: shuffled, never the same track twice in a row, with a crossfade between tracks.
  - Its volume eases down to **half** while the game is paused and back up on resume.
- **Sound effects:**

| Event | Sound | Notes |
|---|---|---|
| Tech node completed | Positive blip | Not for the debug "complete all tech" |
| Items taken by hand (manual pickup) | Radar | |
| Block placed | Short ping, **pitch 0.5** | Any block except belts and pipes |
| The selected machine finishes one craft | Bliq | Machines and multiblock cores; only while its info popup is open |

- **Volume settings:** Pause → Settings has **Ambience** and **Sound effects** sliders (0–100%, default 100%).
  - They scale the designed mix: ambience 0.30 and effects 0.48, with the place sound a further ×0.8.
  - They're saved per computer, not in save files.
  - Releasing the Sound effects slider plays a sample sound.
- **Import settings:** long tracks stream from disk; short effects load into memory.
- **Credits:** sounds come from Freesound; see `CREDITS.md`.

## 9. Simulation

- Every dimension is simulated all the time, with basic optimization.
- The world is small and nesting is bounded, so the cost is expected to be low.
- *Implementation note:* use a fixed-timestep, deterministic tick, so web prototypes and Unity behave the same.

## 10. Data model

*Status: draft.* There are two layers:
- **Definitions:** static game data. These become ScriptableObjects in Unity, and are `content.js` in the prototype.
- **Runtime contracts:** what every placed block must be able to do. These become C# interfaces.

Notation is language-neutral: `?` = optional, `[D#]` = open question in §10.4.

**Prototype differences:** the prototype differs from this model in two places:
- It links items and buildings by matching keys, instead of `placesBuilding` / `item`.
- Recipe inputs and outputs are maps, not lists.

### 10.1 Definitions

Every definition has a unique string `id`. Definitions refer to each other only by `id`.

```
ItemDef
  id, name, icon/color
  placesBuilding?: BuildingId            // set if this item is a placeable block
  category?: string                      // display only: raw, intermediate, component… (no item tiers)
  fuelValue?: int                        // fuel points when burned (§5.8)

FluidDef
  id, name, color                        // amounts in ml; can't be held in the inventory (§5.3)

BuildingDef
  id, name, icon
  visual: VisualRef                      // swappable look (sprite / prefab); no text labels (§1.1)
  description: multi-line text           // shown above the toolbar when selected (§8.1)
  kind: belt | pipe | machine | controller | core | part | hatch
  browserTab: logistics | machines | other
  item?: ItemId                          // the item placed from / refunded as; absent = free to place (belts, pipes)
  rotatable: bool                        // false for controllers (§3); hatches rotate to set their facing
  placement: any | wallAdjacent          // §5.9
  tier: int                              // 1, 2, 3… each tier ×2 stats (§10.3)
  // one block per kind:
  belt?:       { travelTime }
  pipe?:       { capacity: 1 L }          // per pipe; segment pull max 100 ml/tick (§5.3)
  machine?:    { group: MachineGroupId, speed, inputBufferMult, outputCap, fluidBuffer: 10 L }   // fuel buffer = 3 × recipe fuel (§5.8)
  controller?: { size }                  // 3,5,7,9,11,13
  core?:       { multiblock: MultiblockId }
  part?:       { }                       // structural block of a multiblock: casing, coil…
  hatch?:      { io: in | out, carries: item | fluid, capacity }   // I/O only through its facing side

RecipeDef
  id, name?
  type: machine | hand                   // separate recipes; unlocked separately (§7.2)
  group?: MachineGroupId                 // machine recipes: which machines run it
  inputs:  [{ item | fluid, amount }]
  fuel?: int                             // fuel points needed (§5.8)
  outputs: [{ item | fluid, amount, chance? }]   // multi / chance outputs are late-game only
  time                                   // seconds at speed 1; ignored for hand recipes (instant)

MultiblockDef
  id, core: BuildingId
  size: w × h
  // either a fixed layout…
  layout?: grid of cells: core | part(BuildingId) | hatchable(part: BuildingId, allowed: [BuildingId]) | any | empty
  // …or rules
  rules?: [ count(BuildingId, min, max) | adjacentTo(BuildingId, BuildingId) ]
  group: MachineGroupId                  // recipes it runs once formed
  // forms in all 4 rotations, never mirrored

TechNodeDef
  id, name, position (for the tree UI)
  prereqs: [TechNodeId]                  // all must be complete; any number (a graph)
  cost?: [{ item, count }]               // handed over all at once
  requires?: [{ item, count }]           // checked only, not taken
  rewards:                               // no rule changes; only these:
    unlockHandRecipes?: [RecipeId]
    unlockMachineRecipes?: [RecipeId]
    grantItems?: [{ item, count }]       // given once, on completion
    capBonus?: int                       // raises the global inventory cap
  // a node may have no rewards (visual checkpoint)

Tuning (global)
  tickRate, inventoryCap, default inputBufferMult / outputCap, startInventory
```

### 10.2 Runtime contracts

Each placed block is an **entity**: a reference to its `BuildingDef` id plus its own state (position, dimension, rotation, buffers). Every entity implements the contracts that fit its kind.

| Contract | Meaning | Implemented by |
|---|---|---|
| `ITickable` | `Tick()` once per fixed step (20 Hz) | everything that acts |
| `IItemAcceptor` | `CanAccept(item, fromSide)` and `Accept(item, fromSide)` | belt, machine, controller port, input hatch |
| `IItemEmitter` | has an output side; tries to push via the generic I/O rule (§4) | belt, machine, controller port, output hatch |
| `IConfigurable` | settings changed after placement: recipe, output side, port modes, name | machine, controller |
| `IInspectable` | supplies data for the info popup | all |
| `IDestructible` | `CanDestroy()` and `GetContents()` (what's refunded) | all |
| `ISaveable` | serialize / restore its state | all |

Notes:
- **Two-phase eligibility:** `CanAccept` is separate from `Accept`, so the generic I/O rule can check first and only then transfer (§4).
- **Controller ports** act as an acceptor/emitter pair on both sides of the wall. To the generic I/O rule, a port is just another acceptor.
- **Multiblock hatches** implement the same contracts. Multiblocks then need no special cases in the I/O rule.
- **Multiblock buffers live in the hatches,** not in the core. The core reads its hatches to run recipes and to show the buffers in its info popup.
- **Fluids** need matching contracts (`IFluidAcceptor`, `IFluidEmitter`) for machines and pipe segments. Their exact shape is TBD.
- **Tick order:** entities tick in creation order, and the one that updates first wins a contested target. Save/load must preserve this order so replays are deterministic.

### 10.3 Decided

- **Footprint:** all single blocks are 1×1.
- **Recipes:** chosen by the player after placement.
- **Machine I/O:** one output side.
- **Inventory cap:** one global cap (100), raised only by tech nodes. No per-item overrides. Fluids can't be carried by the player.
- **Unique items:** items that can't be crafted; the only way to get them is completing tech tree nodes. No special field or limit is needed. The only way to lose one is overflow when destroying.
- **Building tiers:** a building can come in several tiers within the same machine group (e.g. Smelter T1, T2). Each tier doubles the previous tier's stats: speed, buffer sizes, and so on.
- **Recipe outputs:** recipes can have multiple or chance-based outputs, but only late in the game. That's when better logistics and a way to destroy overflow items become available. Early recipes are mostly single-output.
- **Multiblocks**
  - **Casings and other parts** are real placeable blocks. A hatch can replace a casing at the positions the design allows; where there's no hatch, a casing goes there instead.
  - **Orientation:** a structure forms in any of the 4 rotations, never mirrored.
  - **Core:** the controlling block is called a **core**, never a "controller".
- **Tech tree:** a graph, so a node can have several prerequisites. Nodes never change rules. They only unlock buildings and recipes, and sometimes grant items once on completion.
- **Dimension Breaker:** a multiblock. Its structure is TBD, but it's expected to be about 3×3 and built from endgame materials and machines.
- **Sources:** stay machines whose recipes have no inputs.
- **Fluids**
  - A separate type (`FluidDef`), not an item.
  - They follow the Fluid 2.0-style segment model (§5.3): 1 L per pipe, pulls of up to 100 ml per tick per consumer, 10 L machine buffers.
  - A machine can take a fluid and a solid on different sides.
  - Controller ports have a **Fluid I/O** mode. Pipes joined through it form one segment (§3.1).
  - **Fluid hatches** exist (`hatch.carries: fluid`).
- **Fuel:** the buffer stores points, sized 3× the recipe's fuel cost (48 for the furnace). Items are converted on input and refused if they'd overflow it (§5.8).
- **Items:** no tiers. Category is display only.
- **Multiblock crafting**
  - Inputs are taken from the hatches when a craft **starts**.
  - Breaking a formed structure stops the craft. The materials already consumed are **lost**.
- **Building tiers:** each tier is a separate building.

*Proposed:* an in-place upgrade. Placing a higher-tier machine on a lower-tier one replaces it and keeps its settings (recipe, output side). The old machine goes back to the inventory.

### 10.4 Still to specify

- **D1. Fluid details:**
  - The shape of the fluid runtime contracts.

## 11. Later / deferred

- **Power:** a possible challenge mechanic. Excluded from prototypes for now.
- **Controller rotation:** may come later.
- **Item networks and advanced logistics.**

## 12. Open questions

1. What's the Dimension Breaker's structure? It's a multiblock of about 3×3 (§10.3).
2. Content beyond the early game: more sources, items, recipes, multiblocks and tech nodes. The early game is in [early-game.md](early-game.md).
3. How do unique items fit into the tech tree?
4. Are unique parts refunded on dismantle (see §7.2)? (Per §10.3, they go back to the inventory and are lost only through overflow.)
5. How are 5×5 and larger controllers crafted? The 3×3 comes from the Dimension Compactor ([early-game.md](early-game.md)).
6. Do conduits and machines placed next to a port need anything special (e.g., can a belt end directly in an outgoing port)?
7. Fluid details: see §10.4 D1.
