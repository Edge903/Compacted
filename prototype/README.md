# Compacted — prototype

A throwaway web prototype for testing the rules in [../docs/requirements.md](../docs/requirements.md) and the early-game content in [../docs/early-game.md](../docs/early-game.md) before the Unity build.

## Run

```bash
python -m http.server 8173 --directory prototype
```

Then open http://localhost:8173.

## Files

- `content.js`: all game data — items, fluids, buildings, multiblocks, recipes, tech nodes and tuning. Edit this to try new content.
- `sim.js`: the simulation. Pure logic with no DOM, using a fixed 20 Hz tick. This is the part that maps to Unity C#.
- `game.js`: rendering, input and UI.

## What's in it

- **The early game:** start with one wall chipper, then go through Field Research I–III, Basic Machinery, Mining, Assembler, Dimensional Compression and Time for Expansion.
- **Tech tree (T):**
  - Hand-overs are taken all at once; "requires" items are only checked.
  - Nodes stay hidden until all their prerequisites are done. Lines to them are still shown.
  - Rewards: item grants and hand/machine recipe unlocks.
- **Wall placement:** the chipper and miner must touch a wall. While one is selected, valid tiles are tinted.
- **Fluids:**
  - Free pipes that join into segments. Each pipe shows its share of the segment's fill.
  - Up to 100 ml per tick in and out, with pulls scaled by the fill fraction.
  - Machines can also pass fluid straight to each other.
  - Controller ports have a Fluid I/O mode that joins the pipes inside and outside into one segment.
- **Fuel:**
  - The furnace stores fuel as points, up to 48 (3 × 16).
  - It refuses a fuel item that would overflow.
  - You can add fuel by hand from its popup.
- **Dimension Compactor:** a 2×2 multiblock built from a core, a coil touching it, and input and output buses.
  - Buses only work through the side they face; set it with R when placing.
  - Input materials are consumed when a craft starts. Breaking the structure loses them.
  - The structure gets a teal outline when it's formed.
- **Hand crafting (C):** only unlocked hand recipes appear, and each click is instant.
- **From earlier versions:** controllers, ports, nesting, belts, destroy and refunds, popups, the pause menu and save/load (save version 2; older saves are refused).

## Debug

Pause → Settings has three tools:
- **Free build:** place blocks without inventory items.
- **Simulation speed:** up to 10×.
- **Complete all tech.**

## Behaviour the spec doesn't cover

- **Auto-selected recipes:** a machine or core with only one unlocked recipe starts with that recipe selected (chippers, miners, condensers, the Compactor).
- **Changing a machine's recipe:** its item, fluid and fuel buffers are cleared. Items go back to your inventory; fluid and fuel are lost.
- **Destroyed fluids and fuel:** fluid and fuel inside destroyed blocks are lost. The confirm popup says so.
- **Mixed fluids:** if pipes holding different fluids are joined, the segment keeps the fluid with the larger amount.
- **Input buses:** they only accept what the formed core's recipe needs, up to 3× the recipe amount.
- **Pipe drawing:** pipes only draw connections to pipes and to machines that actually exchange fluid on that side.
- **Shift+R:** rotates the preview the other way.
- **Clicking a breadcrumb:** jumps straight to that ancestor dimension.
