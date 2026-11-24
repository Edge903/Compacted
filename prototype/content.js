// Early-game content (docs/early-game.md). All game data lives here so it can be edited
// without touching logic (maps to ScriptableObjects in the Unity build).
const CONTENT = {
  // Items: anything that sits in the inventory, on a belt or in a buffer. fuel = fuel points when burned.
  items: {
    wall_dust:      { name: 'Wall dust',        color: '#a89f91', fuel: 1 },
    dim_scrap:      { name: 'Dimensium scrap',  color: '#7d8bb0' },
    dim_clay:       { name: 'Dimensium clay',   color: '#9c7bb5' },
    dim_bar:        { name: 'Dimensium bar',    color: '#8fb4ff' },
    en_dust:        { name: 'Energetic dust',   color: '#f2d25c', fuel: 2 },
    en_scrap:       { name: 'Energetic scrap',  color: '#f0a33c', fuel: 8 },
    energy_rod:     { name: 'Energy rod',       color: '#ff7b54' },
    // building items
    wall_chipper:   { name: 'Wall chipper',     color: '#8a8272', building: true },
    wall_miner:     { name: 'Wall miner',       color: '#c2a640', building: true },
    condenser:      { name: 'Atmospheric condenser', color: '#4e9fd6', building: true },
    compressor:     { name: 'Compressor',       color: '#6c7a96', building: true },
    mixer:          { name: 'Mixer',            color: '#8a6aa8', building: true },
    furnace:        { name: 'Furnace',          color: '#c0603e', building: true },
    assembler:      { name: 'Assembler',        color: '#4f78b0', building: true },
    compactor_core: { name: 'Compactor Core',   color: '#3fb5a3', building: true },
    dim_coil:       { name: 'Dimensium coil block', color: '#c98d4a', building: true },
    input_bus:      { name: 'Dimensium input bus',  color: '#4cc38a', building: true },
    output_bus:     { name: 'Dimensium output bus', color: '#f0b44c', building: true },
    ctrl3:          { name: '3×3 dimension controller', color: '#9a7ad0', building: true },
  },

  // Fluids: measured in ml, never carried by the player.
  fluids: {
    water: { name: 'Water', color: '#4ea8f0' },
  },

  // Placeable blocks.
  //   kind: belt | pipe | machine | controller | core | part | hatch
  //   free: placed without an inventory item (belts, pipes)
  //   placement: 'wall' = must touch a wall (the root's border counts)
  //   outputCap: overrides the default machine output buffer
  buildings: {
    belt:           { name: 'Belt', kind: 'belt', tab: 'logistics', free: true },
    pipe:           { name: 'Pipe', kind: 'pipe', tab: 'logistics', free: true },
    wall_chipper:   { name: 'Wall chipper', kind: 'machine', tab: 'machines', machine: 'chipper',    label: 'CHIP', color: '#4d4a42', placement: 'wall', outputCap: 50 },
    wall_miner:     { name: 'Wall miner',   kind: 'machine', tab: 'machines', machine: 'miner',      label: 'MINE', color: '#5e5226', placement: 'wall', outputCap: 50 },
    condenser:      { name: 'Atmospheric condenser', kind: 'machine', tab: 'machines', machine: 'condenser', label: 'COND', color: '#24506e' },
    compressor:     { name: 'Compressor',   kind: 'machine', tab: 'machines', machine: 'compressor', label: 'COMP', color: '#3a4256' },
    mixer:          { name: 'Mixer',        kind: 'machine', tab: 'machines', machine: 'mixer',      label: 'MIX',  color: '#4a3a5e' },
    furnace:        { name: 'Furnace',      kind: 'machine', tab: 'machines', machine: 'furnace',    label: 'FURN', color: '#6e3626' },
    assembler:      { name: 'Assembler',    kind: 'machine', tab: 'machines', machine: 'assembler',  label: 'ASM',  color: '#2f4a6e' },
    compactor_core: { name: 'Compactor Core', kind: 'core', tab: 'other', multiblock: 'compactor', label: 'CORE', color: '#1f5a52' },
    dim_coil:       { name: 'Dimensium coil block', kind: 'part', tab: 'other', label: 'COIL', color: '#6b4a26' },
    input_bus:      { name: 'Dimensium input bus',  kind: 'hatch', tab: 'other', io: 'in',  label: 'IN',  color: '#24483a' },
    output_bus:     { name: 'Dimensium output bus', kind: 'hatch', tab: 'other', io: 'out', label: 'OUT', color: '#4d3d1e' },
    ctrl3:          { name: '3×3 dimension controller', kind: 'controller', tab: 'other', size: 3 },
  },

  // Multiblocks. Rule-based: every tile of a w×h area must be one of the listed blocks,
  // counts must be within [min, max], and each 'adjacent' pair must touch.
  multiblocks: {
    compactor: {
      name: 'Dimension Compactor', core: 'compactor_core', group: 'compactor', size: [2, 2],
      counts: { compactor_core: [1, 1], dim_coil: [1, 1], input_bus: [1, 2], output_bus: [1, 2] },
      adjacent: [['dim_coil', 'compactor_core']],
    },
  },

  // Recipes.
  //   type: machine | hand. Hand recipes are instant and craft from the inventory.
  //   machine recipes: group = which machines run them; locked = needs a tech unlock.
  //   in / out: items. fluidIn / fluidOut: fluids in ml. fuel: fuel points per craft.
  recipes: {
    chip_dust:     { type: 'machine', group: 'chipper',    in: {}, out: { wall_dust: 1 }, time: 1 },
    mine_energy:   { type: 'machine', group: 'miner',      in: {}, out: { en_dust: 1 }, time: 2 },
    condense:      { type: 'machine', group: 'condenser',  in: {}, out: {}, fluidOut: { water: 40 }, time: 1 },
    press_scrap:   { type: 'machine', group: 'compressor', in: { wall_dust: 2 }, out: { dim_scrap: 1 }, time: 2 },
    press_en:      { type: 'machine', group: 'compressor', in: { en_dust: 2 }, out: { en_scrap: 1 }, time: 2 },
    press_rod:     { type: 'machine', group: 'compressor', in: { en_scrap: 8 }, out: { energy_rod: 1 }, time: 8 },
    mix_clay:      { type: 'machine', group: 'mixer',      in: { wall_dust: 4 }, fluidIn: { water: 500 }, out: { dim_clay: 1 }, time: 6 },
    smelt_bar:     { type: 'machine', group: 'furnace',    in: { dim_clay: 1 }, fuel: 16, out: { dim_bar: 1 }, time: 4 },
    asm_coil:      { type: 'machine', group: 'assembler',  in: { dim_bar: 4, energy_rod: 1 }, out: { dim_coil: 1 }, time: 10, locked: true },
    asm_core:      { type: 'machine', group: 'assembler',  in: { dim_bar: 16, energy_rod: 2 }, out: { compactor_core: 1 }, time: 10, locked: true },
    compact_ctrl3: { type: 'machine', group: 'compactor',  in: { dim_bar: 32, energy_rod: 4 }, out: { ctrl3: 1 }, time: 20 },

    h_chipper:     { type: 'hand', in: { dim_bar: 4 }, out: { wall_chipper: 1 } },
    h_compressor:  { type: 'hand', in: { dim_bar: 4 }, out: { compressor: 1 } },
    h_condenser:   { type: 'hand', in: { dim_bar: 4 }, out: { condenser: 1 } },
    h_mixer:       { type: 'hand', in: { dim_bar: 4 }, out: { mixer: 1 } },
    h_furnace:     { type: 'hand', in: { dim_bar: 4 }, out: { furnace: 1 } },
    h_miner:       { type: 'hand', in: { dim_bar: 8 }, out: { wall_miner: 1 } },
    h_assembler:   { type: 'hand', in: { dim_bar: 8, energy_rod: 1 }, out: { assembler: 1 } },
    h_input_bus:   { type: 'hand', in: { dim_bar: 4 }, out: { input_bus: 1 } },
    h_output_bus:  { type: 'hand', in: { dim_bar: 4 }, out: { output_bus: 1 } },
  },

  // Tech tree. pos = [column, row] in the tree view.
  //   cost: handed over all at once. requires: checked only, never taken.
  tech: {
    fr1:       { name: 'Field Research I',   pos: [0, 1], prereqs: [],            cost: { wall_dust: 10 },
                 grant: { compressor: 1, wall_chipper: 1 } },
    fr2:       { name: 'Field Research II',  pos: [1, 1], prereqs: ['fr1'],       cost: { dim_scrap: 10 },
                 grant: { condenser: 1, mixer: 1, furnace: 1 } },
    fr3:       { name: 'Field Research III', pos: [2, 1], prereqs: ['fr2'],       cost: { dim_bar: 1 },
                 grant: { compressor: 1, condenser: 1, mixer: 1, furnace: 1 } },
    basic:     { name: 'Basic Machinery',    pos: [3, 0], prereqs: ['fr3'],       cost: { dim_bar: 10 },
                 unlockHand: ['h_chipper', 'h_compressor', 'h_condenser', 'h_mixer', 'h_furnace'] },
    mining:    { name: 'Mining',             pos: [4, 0], prereqs: ['basic'],     cost: { dim_bar: 20 },
                 grant: { wall_miner: 1 }, unlockHand: ['h_miner'] },
    assembler: { name: 'Assembler',          pos: [5, 0], prereqs: ['mining'],    cost: { energy_rod: 1 },
                 unlockHand: ['h_assembler', 'h_input_bus', 'h_output_bus'], unlockMachine: ['asm_coil', 'asm_core'] },
    dimcomp:   { name: 'Dimensional Compression', pos: [4, 2], prereqs: ['fr3'],  requires: { compactor_core: 1 } },
    expansion: { name: 'Time for Expansion', pos: [6, 1], prereqs: ['assembler', 'dimcomp'], requires: { ctrl3: 1 } },
  },

  // Tuning
  tickRate: 20,            // simulation ticks per second (fixed timestep)
  beltTime: 0.5,           // seconds for an item to cross one belt tile (max 2 items/s)
  inputBufferMult: 3,      // item input buffer = recipe need × this (machines and input buses)
  outputCap: 10,           // default item output buffer cap per item (machines and output buses)
  fuelBufferMult: 3,       // fuel buffer = recipe fuel × this (points)
  fluidBuffer: 10000,      // ml per fluid in a machine
  pipeCapacity: 1000,      // ml per pipe
  fluidPerTick: 100,       // max ml pushed / pulled per tick per machine
  inventoryCap: 100,       // per item type

  startInventory: { wall_chipper: 1 },
  defaultToolbar: ['belt', 'pipe', 'wall_chipper', 'compressor', 'condenser', 'mixer', 'furnace', 'wall_miner', 'assembler'],
};
