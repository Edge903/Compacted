# Compacted — Early-game content (v0.1)

*Status: draft, 2026-11-22.* This is the content spec for the opening of the game, from the first wall chipper to the first 3×3 dimension controller. It's based on the designer's playthrough. The rules it depends on are in [requirements.md](requirements.md).

## 1. Intended playthrough

1. **Start:** the player starts with one **Wall chipper**. They place it against a wall, collect **wall dust** by hand, and hand over 10 to **Field Research I**.
2. **Scrap:** Field Research I grants a **Compressor** and a second chipper. Compressing dust makes **Dimensium scrap**, and 10 scrap completes **Field Research II**.
3. **Bars:** Field Research II grants an **Atmospheric condenser**, a **Mixer** and a **Furnace**.
   - Condenser water plus dust makes **Dimensium clay** in the mixer.
   - The furnace burns dust as fuel to turn clay into a **Dimensium bar**.
   - 1 bar completes **Field Research III**.
4. **More machines:** Field Research III grants a second set of the four machines. It opens two branches: **Basic Machinery** (hand crafting for the basic machines) and **Dimensional Compression**.
5. **Energy:** **Mining** grants a **Wall miner**. It produces **energetic dust**, which compresses into **energetic scrap**, then into an **energy rod**. Energetic dust and scrap are also better furnace fuel.
6. **Assembler:** 1 energy rod completes **Assembler**. That unlocks crafting the assembler and the multiblock parts.
7. **Dimension Compactor:** the player builds the **Dimension Compactor**, a 2×2 multiblock, and uses it to make the first **3×3 dimension controller**.
8. **Checkpoint:** showing that controller completes **Time for Expansion**, a visual checkpoint on the tech tree.

**Space pressure:** Field Research III needs a chipper, compressor, condenser, mixer and furnace all inside the 3×3 root. Only 8 of its tiles touch a wall.

## 2. Items

| Item | Notes |
|---|---|
| Wall dust | From the wall chipper. Used as an ingredient and as fuel (1 fuel point). |
| Dimensium scrap | Compressed wall dust. |
| Dimensium clay | Made in the mixer. |
| Dimensium bar | Smelted in the furnace. The main early material. |
| Energetic dust | From the wall miner. Fuel: 2 points. |
| Energetic scrap | Compressed energetic dust. Fuel: 8 points. |
| Energy rod | Compressed energetic scrap. |

Fluids: **Water** (§3 of requirements, Fluid model).

Building items: every building below is also an item, except belts and pipes, which are free.

## 3. Buildings

| Building | Kind | Placement | Notes |
|---|---|---|---|
| Wall chipper | machine (source) | wall-adjacent | 1 wall dust per second. Output buffer 50. |
| Wall miner | machine (source) | wall-adjacent | 0.5 energetic dust per second. Output buffer 50. |
| Atmospheric condenser | machine (source) | anywhere | 40 ml of water per second. |
| Compressor | machine | anywhere | |
| Mixer | machine | anywhere | Takes water and an item on different sides. |
| Furnace | machine | anywhere | Uses fuel (requirements §5.8). |
| Assembler | machine | anywhere | |
| Compactor Core | multiblock core | anywhere | The core of the Dimension Compactor. |
| Dimensium coil block | multiblock part | anywhere | Structural; must touch the core. |
| Dimensium input bus | hatch (item in) | anywhere | Accepts only from the side it faces (set by rotation). |
| Dimensium output bus | hatch (item out) | anywhere | Outputs only to the side it faces (set by rotation). |
| 3×3 dimension controller | controller | anywhere | |

**Wall-adjacent:** the tile touches at least one wall. The output side can face any direction. There's no bonus for corners, and port tiles are allowed. The root's border counts as a wall.

## 4. Recipes

### 4.1 Machine recipes

| Machine | Inputs | Output | Time |
|---|---|---|---|
| Wall chipper | — | 1 wall dust | 1 s |
| Wall miner | — | 1 energetic dust | 2 s |
| Atmospheric condenser | — | 40 ml water | 1 s (continuous) |
| Compressor | 2 wall dust | 1 Dimensium scrap | 2 s |
| Compressor | 2 energetic dust | 1 energetic scrap | 2 s |
| Compressor | 8 energetic scrap | 1 energy rod | 8 s |
| Mixer | 500 ml water + 4 wall dust | 1 Dimensium clay | 6 s |
| Furnace | 1 Dimensium clay + 16 fuel points | 1 Dimensium bar | 4 s |
| Assembler | 4 Dimensium bar + 1 energy rod | 1 Dimensium coil block | 10 s |
| Assembler | 16 Dimensium bar + 2 energy rod | 1 Compactor Core | 10 s |
| Dimension Compactor | 32 Dimensium bar + 4 energy rod | 1 3×3 dimension controller | 20 s |

### 4.2 Hand recipes (instant)

| Output | Inputs | Unlocked by |
|---|---|---|
| Wall chipper | 4 Dimensium bar | Basic Machinery |
| Compressor | 4 Dimensium bar | Basic Machinery |
| Atmospheric condenser | 4 Dimensium bar | Basic Machinery |
| Mixer | 4 Dimensium bar | Basic Machinery |
| Furnace | 4 Dimensium bar | Basic Machinery |
| Wall miner | 8 Dimensium bar | Mining |
| Assembler | 8 Dimensium bar + 1 energy rod | Assembler |
| Dimensium input bus | 4 Dimensium bar | Assembler |
| Dimensium output bus | 4 Dimensium bar | Assembler |

### 4.3 Throughput notes

- **One bar costs 20 wall dust** (4 for clay + 16 for fuel) and 500 ml of water. A single chipper makes about 1 bar every 20 seconds. A single condenser supplies one clay every 12.5 seconds.
- **Energy rod:** one rod needs 8 energetic scrap, which is 16 energetic dust. That's 32 seconds of one miner.
- **Fuel values:** energetic scrap is worth 8 points, double the 4 points of the 2 dust it's made from. That's intentional: compressing fuel is worth it.

## 5. Tech tree

Hand-over costs are paid **all at once** from the inventory. **Requires** items are checked but not taken.

| Node | Prerequisites | Hand over | Requires (checked only) | Rewards |
|---|---|---|---|---|
| Field Research I | — (starting node) | 10 wall dust | | Grants 1 Compressor and 1 Wall chipper |
| Field Research II | Field Research I | 10 Dimensium scrap | | Grants 1 Atmospheric condenser, 1 Mixer and 1 Furnace |
| Field Research III | Field Research II | 1 Dimensium bar | | Grants 1 Compressor, 1 Atmospheric condenser, 1 Mixer and 1 Furnace |
| Basic Machinery | Field Research III | 10 Dimensium bar | | Unlocks hand recipes: chipper, compressor, condenser, mixer, furnace |
| Mining | Basic Machinery | 20 Dimensium bar | | Grants 1 Wall miner; unlocks its hand recipe |
| Assembler | Mining | 1 energy rod | | Unlocks hand recipes: assembler, input bus, output bus; unlocks machine recipes: coil block, Compactor Core |
| Dimensional Compression | Field Research III | | 1 Compactor Core | None. A checkpoint (the Compactor needs no unlock) |
| Time for Expansion | Assembler **and** Dimensional Compression | | 1 3×3 dimension controller | None. A visual checkpoint |

**Soft dependency:** Dimensional Compression needs a Compactor Core, and that can only be crafted after Assembler. Dimensional Compression opens early, but it can't be finished until Assembler is done.

## 6. Multiblock: Dimension Compactor

- **Footprint:** 2×2.
- **Blocks:**
  - 1 **Compactor Core**.
  - 1 **Dimensium coil block**, which must be next to the core.
  - At least 1 **Dimensium input bus**.
  - At least 1 **Dimensium output bus**.

  In a 2×2 that means exactly one of each. The positions of the buses don't matter.
- **Valid arrangements:** the buses can go anywhere, so any 2×2 arrangement where the coil touches the core is valid.
- **Recipe:** 32 Dimensium bar + 4 energy rod → 1 3×3 dimension controller, 20 s.

## 7. Decided

- **Dimensional Compression** gives no reward. The Dimension Compactor and its recipe need no unlock.
- **Source recipe times** are as listed in §4.1: chipper 1 s, miner 2 s, condenser 40 ml per 1 s (continuous).
- **3×3 dimension controllers** can only be made by the Dimension Compactor.
- **Starting inventory:** only 1 wall chipper.
