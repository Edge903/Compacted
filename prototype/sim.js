// Simulation core. Pure logic, no DOM or rendering.
// Directions: 0 = N, 1 = E, 2 = S, 3 = W.
const DIRS = [{ x: 0, y: -1 }, { x: 1, y: 0 }, { x: 0, y: 1 }, { x: -1, y: 0 }];
const DIR_NAMES = ['N', 'E', 'S', 'W'];
const opp = d => (d + 2) % 4;
const sumMap = m => Object.values(m).reduce((a, n) => a + n, 0);

class World {
  constructor(content) {
    this.c = content;
    this.dt = 1 / content.tickRate;
    this.nextId = 1;
    this.tick = 0;
    this.dims = new Map();   // id -> { id, size, ctrlId, grid: [entityId|0] }
    this.ents = new Map();   // id -> entity
    this.inventory = { ...content.startInventory };
    this.freeBuild = false;
    this.ctrlCount = 0;
    this.tech = { done: [], hand: [], machine: [] };
    this.segs = [];          // pipe segments (rebuilt when pipes or fluid ports change)
    this.segOf = new Map();  // pipe id -> segment
    this.segDirty = true;
    this.rootId = this.createDim(3, null);
  }

  // ---------- dimensions ----------
  createDim(size, ctrlId) {
    const id = this.nextId++;
    this.dims.set(id, { id, size, ctrlId, grid: new Array(size * size).fill(0) });
    return id;
  }
  inBounds(dim, x, y) { return x >= 0 && y >= 0 && x < dim.size && y < dim.size; }
  at(dimId, x, y) {
    const dim = this.dims.get(dimId);
    if (!this.inBounds(dim, x, y)) return undefined;
    const id = dim.grid[y * dim.size + x];
    return id ? this.ents.get(id) : null;
  }
  isEmptyDim(dimId) { return this.dims.get(dimId).grid.every(v => v === 0); }
  countBlocks(dimId) { return this.dims.get(dimId).grid.filter(v => v !== 0).length; }
  portTile(dim, d) {
    const m = (dim.size - 1) / 2, e = dim.size - 1;
    return [{ x: m, y: 0 }, { x: e, y: m }, { x: m, y: e }, { x: 0, y: m }][d];
  }
  parentDimOf(dimId) {
    const dim = this.dims.get(dimId);
    return dim.ctrlId == null ? null : this.ents.get(dim.ctrlId).dim;
  }
  touchesWall(dim, x, y) { return x === 0 || y === 0 || x === dim.size - 1 || y === dim.size - 1; }
  def(e) { return this.c.buildings[e.type]; }

  // ---------- inventory ----------
  invCount(item) { return this.inventory[item] || 0; }
  addInv(item, n) {   // returns the amount that didn't fit (lost)
    const have = this.invCount(item);
    const add = Math.max(0, Math.min(n, this.c.inventoryCap - have));
    if (add > 0) this.inventory[item] = have + add;
    return n - add;
  }
  takeInv(item, n) {
    const take = Math.min(n, this.invCount(item));
    this.inventory[item] = this.invCount(item) - take;
    if (this.inventory[item] === 0) delete this.inventory[item];
    return take;
  }
  hasAll(map) { return Object.entries(map || {}).every(([k, n]) => this.invCount(k) >= n); }

  // ---------- recipes & tech ----------
  recipeUnlocked(rid) {
    const r = this.c.recipes[rid];
    if (r.type === 'hand') return this.tech.hand.includes(rid);
    return !r.locked || this.tech.machine.includes(rid);
  }
  groupOf(e) { const b = this.def(e); return b.kind === 'core' ? this.c.multiblocks[b.multiblock].group : b.machine; }
  recipesFor(e) {
    const g = this.groupOf(e);
    return Object.keys(this.c.recipes).filter(id => { const r = this.c.recipes[id]; return r.type === 'machine' && r.group === g && this.recipeUnlocked(id); });
  }
  handRecipes() { return Object.keys(this.c.recipes).filter(id => this.c.recipes[id].type === 'hand' && this.recipeUnlocked(id)); }
  techState(id) {
    if (this.tech.done.includes(id)) return 'done';
    return this.c.tech[id].prereqs.every(p => this.tech.done.includes(p)) ? 'open' : 'hidden';
  }
  canCompleteTech(id) {
    const t = this.c.tech[id];
    if (this.techState(id) !== 'open') return { ok: false, reason: 'Not available yet' };
    if (!this.hasAll(t.cost)) return { ok: false, reason: 'Not enough items to hand over' };
    if (!this.hasAll(t.requires)) return { ok: false, reason: 'Missing required items' };
    return { ok: true };
  }
  completeTech(id, force) {
    if (!force) { const chk = this.canCompleteTech(id); if (!chk.ok) return chk; }
    const t = this.c.tech[id];
    if (!force) for (const [k, n] of Object.entries(t.cost || {})) this.takeInv(k, n);
    const lost = {};
    for (const [k, n] of Object.entries(t.grant || {})) { const l = this.addInv(k, n); if (l) lost[k] = l; }
    for (const r of t.unlockHand || []) if (!this.tech.hand.includes(r)) this.tech.hand.push(r);
    for (const r of t.unlockMachine || []) if (!this.tech.machine.includes(r)) this.tech.machine.push(r);
    this.tech.done.push(id);
    return { ok: true, lost };
  }

  // ---------- hand crafting (instant) ----------
  handCraft(rid) {
    const r = this.c.recipes[rid];
    if (!r || r.type !== 'hand' || !this.recipeUnlocked(rid)) return { ok: false, reason: 'Recipe is locked' };
    if (!this.hasAll(r.in)) return { ok: false, reason: 'Not enough materials' };
    if (!Object.entries(r.out).every(([k, n]) => this.invCount(k) + n <= this.c.inventoryCap)) return { ok: false, reason: 'Inventory is full for that item' };
    for (const [k, n] of Object.entries(r.in)) this.takeInv(k, n);
    for (const [k, n] of Object.entries(r.out)) this.addInv(k, n);
    return { ok: true };
  }

  // ---------- placement ----------
  canPlace(type, dimId, x, y) {
    const b = this.c.buildings[type];
    const dim = this.dims.get(dimId);
    if (!this.inBounds(dim, x, y)) return { ok: false, reason: 'Outside the dimension' };
    if (this.at(dimId, x, y)) return { ok: false, reason: 'Tile is occupied' };
    if (b.placement === 'wall' && !this.touchesWall(dim, x, y)) return { ok: false, reason: `${b.name} must touch a wall` };
    if (b.kind === 'controller' && dim.ctrlId != null && !(dim.size > b.size))
      return { ok: false, reason: `A ${b.size}×${b.size} controller only fits in a bigger dimension` };
    if (!b.free && !this.freeBuild && this.invCount(type) < 1) return { ok: false, reason: `No ${b.name} in inventory` };
    return { ok: true };
  }

  place(type, dimId, x, y, dir) {
    const chk = this.canPlace(type, dimId, x, y);
    if (!chk.ok) return chk;
    const b = this.c.buildings[type];
    if (!b.free && !this.freeBuild) this.takeInv(type, 1);
    const rotates = ['belt', 'machine', 'hatch'].includes(b.kind);
    const e = { id: this.nextId++, type, kind: b.kind, dim: dimId, x, y, dir: rotates ? dir : 0 };
    if (b.kind === 'belt') Object.assign(e, { item: null, from: opp(dir), prog: 0, hist: [] });
    if (b.kind === 'pipe') Object.assign(e, { fluid: null, amt: 0 });
    if (b.kind === 'machine') {
      Object.assign(e, { recipe: null, inBuf: {}, outBuf: {}, fIn: {}, fOut: {}, fuel: 0, crafting: false, prog: 0, status: 'no recipe' });
      const rs = this.recipesFor(e);
      if (rs.length === 1) e.recipe = rs[0];   // single-recipe machines (sources) start running right away
    }
    if (b.kind === 'core') Object.assign(e, { recipe: null, crafting: false, prog: 0, status: 'not formed', formed: null });
    if (b.kind === 'hatch') Object.assign(e, { buf: {}, core: null });
    if (b.kind === 'controller') {
      this.ctrlCount++;
      Object.assign(e, { name: `Box ${this.ctrlCount}`, ports: ['off', 'off', 'off', 'off'], portBuf: [null, null, null, null] });
    }
    this.ents.set(e.id, e);
    const dim = this.dims.get(dimId);
    dim.grid[y * dim.size + x] = e.id;
    if (b.kind === 'controller') e.inner = this.createDim(b.size, e.id);
    if (b.kind === 'core') { const rs = this.recipesFor(e); if (rs.length === 1) e.recipe = rs[0]; }
    this.markSegsDirty();
    return { ok: true, ent: e };
  }

  // ---------- destroy ----------
  contentsOf(e) {
    const out = {};
    const add = (k, n) => { if (n > 0) out[k] = (out[k] || 0) + n; };
    if (e.kind === 'belt' && e.item) add(e.item, 1);
    if (e.kind === 'machine') {
      for (const [k, n] of Object.entries(e.inBuf)) add(k, n);
      for (const [k, n] of Object.entries(e.outBuf)) add(k, n);
      if (e.crafting) for (const [k, n] of Object.entries(this.c.recipes[e.recipe].in)) add(k, n);
    }
    if (e.kind === 'hatch') for (const [k, n] of Object.entries(e.buf)) add(k, n);
    if (e.kind === 'controller') for (const it of e.portBuf) if (it) add(it, 1);
    return out;   // fluids, fuel points and a core's in-progress craft are not refunded
  }

  planDestroy(dimId, x0, y0, x1, y1) {
    const plan = { dimId, targets: [], counts: {}, refunds: {}, lost: {}, skipped: 0 };
    const [ax, bx] = [Math.min(x0, x1), Math.max(x0, x1)], [ay, by] = [Math.min(y0, y1), Math.max(y0, y1)];
    for (let y = ay; y <= by; y++) for (let x = ax; x <= bx; x++) {
      const e = this.at(dimId, x, y);
      if (!e) continue;
      if (e.kind === 'controller' && !this.isEmptyDim(e.inner)) { plan.skipped++; continue; }
      plan.targets.push(e.id);
      plan.counts[e.type] = (plan.counts[e.type] || 0) + 1;
      if (!this.def(e).free) plan.refunds[e.type] = (plan.refunds[e.type] || 0) + 1;
      for (const [k, n] of Object.entries(this.contentsOf(e))) plan.refunds[k] = (plan.refunds[k] || 0) + n;
    }
    for (const [k, n] of Object.entries(plan.refunds)) {
      const space = Math.max(0, this.c.inventoryCap - this.invCount(k));
      if (n > space) plan.lost[k] = n - space;
    }
    return plan;
  }

  execDestroy(plan) {
    this.flushSegs();   // keep fluid in the pipes that survive
    for (const id of plan.targets) {
      const e = this.ents.get(id);
      if (!e) continue;
      const dim = this.dims.get(e.dim);
      dim.grid[e.y * dim.size + e.x] = 0;
      this.ents.delete(id);
      if (e.kind === 'controller') this.dims.delete(e.inner);
    }
    for (const [k, n] of Object.entries(plan.refunds)) this.addInv(k, n);
    this.markSegsDirty();
  }

  // ---------- configuration & hand transfer ----------
  setRecipe(e, rid) {   // returns items lost to the inventory cap
    let lost = 0;
    const back = { ...e.inBuf };
    if (e.kind === 'machine' && e.crafting) for (const [k, n] of Object.entries(this.c.recipes[e.recipe].in)) back[k] = (back[k] || 0) + n;
    for (const [k, n] of Object.entries(back)) lost += this.addInv(k, n);
    if (e.kind === 'machine') { e.inBuf = {}; e.fIn = {}; e.fuel = 0; }
    e.crafting = false; e.prog = 0;
    e.recipe = rid || null;
    return lost;
  }
  inputCap(recipe, item) {
    const need = recipe ? recipe.in[item] : 0;
    return need ? need * this.c.inputBufferMult : 0;
  }
  fuelCap(e) { const r = e.recipe && this.c.recipes[e.recipe]; return r && r.fuel ? r.fuel * this.c.fuelBufferMult : 0; }
  fuelValue(item) { return (this.c.items[item] && this.c.items[item].fuel) || 0; }

  // Hand-insert into a machine (ingredient or fuel) or an input bus. Returns how many moved.
  insertFromInv(e, item) {
    let n = 0;
    while (this.invCount(item) > 0 && this.acceptHand(e, item)) { this.takeInv(item, 1); n++; }
    return n;
  }
  acceptHand(e, item) {
    if (e.kind === 'machine') return this.acceptMachine(e, item, -1);
    if (e.kind === 'hatch') return this.acceptHatch(e, item, e.dir);
    return false;
  }
  takeOutput(e) {
    const buf = e.kind === 'hatch' ? e.buf : e.outBuf;
    let moved = 0;
    for (const [k, n] of Object.entries(buf)) {
      const left = this.addInv(k, n);
      moved += n - left;
      if (left > 0) buf[k] = left; else delete buf[k];
    }
    return moved;
  }
  setPortMode(c, d, mode) {
    let lost = 0;
    if (c.portBuf[d]) { lost = this.addInv(c.portBuf[d], 1); c.portBuf[d] = null; }
    if (c.ports[d] === 'fluid' || mode === 'fluid') this.markSegsDirty();
    c.ports[d] = mode;
    return lost;
  }

  // ---------- generic item I/O rule ----------
  push(e, d, item) {
    const dim = this.dims.get(e.dim);
    const nx = e.x + DIRS[d].x, ny = e.y + DIRS[d].y;
    if (!this.inBounds(dim, nx, ny)) {
      if (dim.ctrlId == null) return false;
      const pt = this.portTile(dim, d);
      if (pt.x !== e.x || pt.y !== e.y) return false;
      const c = this.ents.get(dim.ctrlId);
      if (c.ports[d] !== 'out' || c.portBuf[d]) return false;
      c.portBuf[d] = item;
      return true;
    }
    const t = this.at(e.dim, nx, ny);
    if (!t) return false;
    return this.accept(t, item, opp(d));
  }
  accept(t, item, from) {
    switch (t.kind) {
      case 'belt':
        if (from === t.dir || t.item) return false;
        t.item = item; t.from = from; t.prog = 0;
        return true;
      case 'machine': return this.acceptMachine(t, item, from);
      case 'hatch': return this.acceptHatch(t, item, from);
      case 'controller':
        if (t.ports[from] !== 'in' || t.portBuf[from]) return false;
        t.portBuf[from] = item;
        return true;
    }
    return false;   // pipes, cores and parts take no items
  }
  // from = -1 means by hand (any side).
  acceptMachine(t, item, from) {
    if (from === t.dir || !t.recipe) return false;
    const r = this.c.recipes[t.recipe];
    const cap = this.inputCap(r, item);
    if (cap) {
      if ((t.inBuf[item] || 0) >= cap) return false;
      t.inBuf[item] = (t.inBuf[item] || 0) + 1;
      return true;
    }
    const v = this.fuelValue(item);
    if (r.fuel && v) {
      if (t.fuel + v > this.fuelCap(t)) return false;   // would overflow the fuel buffer: refuse
      t.fuel += v;                                       // converted to points on input
      return true;
    }
    return false;
  }
  // Input buses accept only through their facing side, and only what the formed core's recipe needs.
  acceptHatch(t, item, from) {
    if (this.def(t).io !== 'in' || from !== t.dir) return false;
    const core = this.ents.get(t.core);
    if (!core || !core.formed || !core.formed.hatches.includes(t.id) || !core.recipe) return false;
    const cap = this.inputCap(this.c.recipes[core.recipe], item);
    if (!cap || (t.buf[item] || 0) >= cap) return false;
    t.buf[item] = (t.buf[item] || 0) + 1;
    return true;
  }

  // ---------- fluids: pipe segments ----------
  markSegsDirty() { this.flushSegs(); this.segDirty = true; }
  // Write each segment's amount back into its pipes (even share) so it survives a rebuild.
  flushSegs() {
    for (const s of this.segs) {
      const share = s.pipes.length ? s.amt / s.pipes.length : 0;
      for (const id of s.pipes) { const p = this.ents.get(id); if (p) { p.fluid = s.amt > 0 ? s.fluid : null; p.amt = share; } }
    }
  }
  // Entity reached by stepping from e in direction d, crossing Fluid I/O ports both ways.
  fluidNeighbor(e, d) {
    const dim = this.dims.get(e.dim);
    const nx = e.x + DIRS[d].x, ny = e.y + DIRS[d].y;
    let t;
    if (this.inBounds(dim, nx, ny)) t = this.at(e.dim, nx, ny);
    else {
      if (dim.ctrlId == null) return null;
      const pt = this.portTile(dim, d);
      if (pt.x !== e.x || pt.y !== e.y) return null;
      const c = this.ents.get(dim.ctrlId);
      if (c.ports[d] !== 'fluid') return null;
      return this.fluidNeighbor(c, d);   // out through the wall into the parent
    }
    if (t && t.kind === 'controller' && t.ports[opp(d)] === 'fluid') {
      const inner = this.dims.get(t.inner), pt = this.portTile(inner, opp(d));
      t = this.at(t.inner, pt.x, pt.y);   // in through the wall into the interior
      if (t && t.kind === 'controller' && t.ports[opp(d)] === 'fluid') return this.fluidNeighbor({ dim: t.dim, x: t.x - DIRS[d].x, y: t.y - DIRS[d].y }, d);
    }
    return t || null;
  }
  rebuildSegs() {
    this.flushSegs();
    this.segs = []; this.segOf = new Map();
    for (const p of this.ents.values()) {
      if (p.kind !== 'pipe' || this.segOf.has(p.id)) continue;
      const seg = { pipes: [], fluid: null, amt: 0, cap: 0 };
      const byFluid = {};
      const stack = [p]; this.segOf.set(p.id, seg);
      while (stack.length) {
        const q = stack.pop();
        seg.pipes.push(q.id);
        if (q.fluid && q.amt > 0) byFluid[q.fluid] = (byFluid[q.fluid] || 0) + q.amt;
        for (let d = 0; d < 4; d++) {
          const n = this.fluidNeighbor(q, d);
          if (n && n.kind === 'pipe' && !this.segOf.has(n.id)) { this.segOf.set(n.id, seg); stack.push(n); }
        }
      }
      // One fluid per segment: if two segments with different fluids merge, the larger wins.
      for (const [f, a] of Object.entries(byFluid)) if (a > seg.amt) { seg.fluid = f; seg.amt = a; }
      seg.cap = seg.pipes.length * this.c.pipeCapacity;
      seg.amt = Math.min(seg.amt, seg.cap);
      this.segs.push(seg);
    }
    this.segDirty = false;
  }
  segFill(s) { return s.cap ? s.amt / s.cap : 0; }
  pipeAmount(p) { const s = this.segOf.get(p.id); return s ? s.amt / s.pipes.length : 0; }

  // Push into a segment: up to fluidPerTick, limited only by the room left.
  pushToSeg(seg, fluid, amount) {
    if (seg.amt > 0 && seg.fluid !== fluid) return 0;
    const n = Math.min(amount, this.c.fluidPerTick, seg.cap - seg.amt);
    if (n <= 0) return 0;
    seg.fluid = fluid; seg.amt += n;
    return n;
  }
  // Pull from a segment: fluidPerTick × fill fraction; under 1% full, take everything left.
  pullFromSeg(seg, space) {
    if (seg.amt <= 0) return 0;
    const want = this.segFill(seg) < 0.01 ? seg.amt : this.c.fluidPerTick * this.segFill(seg);
    const n = Math.min(want, space, seg.amt);
    seg.amt -= n;
    if (seg.amt < 1e-6) { seg.amt = 0; seg.fluid = null; }
    return n;
  }

  // ---------- multiblocks ----------
  // Returns { hatches, rect } if the core sits in a valid structure, else null.
  checkFormation(core) {
    const mb = this.c.multiblocks[this.def(core).multiblock];
    const dim = this.dims.get(core.dim);
    const [w0, h0] = mb.size;
    const shapes = w0 === h0 ? [[w0, h0]] : [[w0, h0], [h0, w0]];
    for (const [w, h] of shapes) for (let oy = core.y - h + 1; oy <= core.y; oy++) for (let ox = core.x - w + 1; ox <= core.x; ox++) {
      if (ox < 0 || oy < 0 || ox + w > dim.size || oy + h > dim.size) continue;
      const tiles = [];
      let ok = true;
      for (let y = oy; y < oy + h && ok; y++) for (let x = ox; x < ox + w; x++) {
        const t = this.at(core.dim, x, y);
        if (!t || !(t.type in mb.counts)) { ok = false; break; }
        if (t.kind === 'core' && t.id !== core.id) { ok = false; break; }
        tiles.push(t);
      }
      if (!ok) continue;
      const count = {};
      for (const t of tiles) count[t.type] = (count[t.type] || 0) + 1;
      if (!Object.entries(mb.counts).every(([k, [min, max]]) => (count[k] || 0) >= min && (count[k] || 0) <= max)) continue;
      const touches = (a, b) => Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1;
      if (!mb.adjacent.every(([a, b]) => tiles.some(t => t.type === a && tiles.some(u => u.type === b && touches(t, u))))) continue;
      return { hatches: tiles.filter(t => t.kind === 'hatch').map(t => t.id), parts: tiles.map(t => t.id), rect: { x: ox, y: oy, w, h } };
    }
    return null;
  }

  // ---------- simulation ----------
  step() {
    if (this.segDirty) this.rebuildSegs();
    this.tick++;
    for (const e of this.ents.values()) {
      if (e.kind === 'belt') this.stepBelt(e);
      else if (e.kind === 'machine') this.stepMachine(e);
      else if (e.kind === 'controller') this.stepController(e);
      else if (e.kind === 'core') this.stepCore(e);
      else if (e.kind === 'hatch') this.stepHatch(e);
    }
  }

  stepBelt(e) {
    if (e.item) {
      e.prog = Math.min(1, e.prog + this.dt / this.c.beltTime);
      if (e.prog >= 1 && this.push(e, e.dir, e.item)) { e.item = null; e.hist.push(this.tick); }
    }
    const window = this.c.tickRate * 5;
    while (e.hist.length && e.hist[0] < this.tick - window) e.hist.shift();
  }
  beltRate(e) {
    const secs = Math.min(5, this.tick / this.c.tickRate);
    return secs > 0 ? e.hist.length / secs : 0;
  }

  stepMachine(e) {
    // Items out: at most one per tick through the output side.
    for (const [k, n] of Object.entries(e.outBuf)) {
      if (n > 0 && this.push(e, e.dir, k)) { if (n === 1) delete e.outBuf[k]; else e.outBuf[k] = n - 1; break; }
    }
    // Fluids out: through the output side, into a pipe segment or straight into a machine.
    for (const [f, a] of Object.entries(e.fOut)) {
      if (a <= 0) continue;
      const t = this.fluidNeighbor(e, e.dir);
      let n = 0;
      if (t && t.kind === 'pipe') n = this.pushToSeg(this.segOf.get(t.id), f, a);
      else if (t && t.kind === 'machine') n = this.acceptFluid(t, f, Math.min(a, this.c.fluidPerTick));
      e.fOut[f] = a - n;
      if (e.fOut[f] <= 0) delete e.fOut[f];
    }
    if (!e.recipe) { e.status = 'no recipe'; return; }
    const r = this.c.recipes[e.recipe];
    // Fluids in: pull from each adjacent segment once per tick (not through the output side).
    if (r.fluidIn) {
      const seen = new Set();
      for (let d = 0; d < 4; d++) {
        if (d === e.dir) continue;
        const t = this.fluidNeighbor(e, d);
        if (!t || t.kind !== 'pipe') continue;
        const s = this.segOf.get(t.id);
        if (!s || seen.has(s)) continue;
        seen.add(s);
        if (!s.fluid || !(s.fluid in r.fluidIn)) continue;
        const space = this.c.fluidBuffer - (e.fIn[s.fluid] || 0);
        const f = s.fluid, n = this.pullFromSeg(s, space);
        if (n > 0) e.fIn[f] = (e.fIn[f] || 0) + n;
      }
    }
    if (!e.crafting) {
      const ready = Object.entries(r.in).every(([k, n]) => (e.inBuf[k] || 0) >= n)
        && Object.entries(r.fluidIn || {}).every(([f, n]) => (e.fIn[f] || 0) >= n - 1e-6)
        && (!r.fuel || e.fuel >= r.fuel);
      if (!ready) { e.status = r.fuel && e.fuel < r.fuel && Object.entries(r.in).every(([k, n]) => (e.inBuf[k] || 0) >= n) ? 'needs fuel' : 'waiting for input'; return; }
      for (const [k, n] of Object.entries(r.in)) { e.inBuf[k] -= n; if (e.inBuf[k] === 0) delete e.inBuf[k]; }
      for (const [f, n] of Object.entries(r.fluidIn || {})) { e.fIn[f] = Math.max(0, e.fIn[f] - n); if (e.fIn[f] < 1e-6) delete e.fIn[f]; }
      if (r.fuel) e.fuel -= r.fuel;
      e.crafting = true; e.prog = 0;
    }
    e.prog = Math.min(1, e.prog + this.dt / r.time);
    if (e.prog < 1) { e.status = 'working'; return; }
    const cap = this.def(e).outputCap || this.c.outputCap;
    const fits = Object.entries(r.out).every(([k, n]) => (e.outBuf[k] || 0) + n <= Math.max(cap, n))
      && Object.entries(r.fluidOut || {}).every(([f, n]) => (e.fOut[f] || 0) + n <= this.c.fluidBuffer);
    if (!fits) { e.status = 'output blocked'; return; }
    for (const [k, n] of Object.entries(r.out)) e.outBuf[k] = (e.outBuf[k] || 0) + n;
    for (const [f, n] of Object.entries(r.fluidOut || {})) e.fOut[f] = (e.fOut[f] || 0) + n;
    e.crafting = false; e.prog = 0; e.status = 'working';
  }
  // Direct machine-to-machine fluid transfer. Returns ml accepted.
  acceptFluid(t, f, amount) {
    if (!t.recipe) return 0;
    const r = this.c.recipes[t.recipe];
    if (!r.fluidIn || !(f in r.fluidIn)) return 0;
    const n = Math.min(amount, this.c.fluidBuffer - (t.fIn[f] || 0));
    if (n > 0) t.fIn[f] = (t.fIn[f] || 0) + n;
    return Math.max(0, n);
  }

  stepCore(c) {
    const st = this.checkFormation(c);
    c.formed = st;
    if (!st) {
      if (c.crafting) { c.crafting = false; c.prog = 0; }   // structure broken: the consumed materials are lost
      c.status = 'not formed';
      return;
    }
    for (const id of st.hatches) this.ents.get(id).core = c.id;
    if (!c.recipe) { c.status = 'no recipe'; return; }
    const r = this.c.recipes[c.recipe];
    const ins = st.hatches.map(id => this.ents.get(id)).filter(h => this.def(h).io === 'in');
    const outs = st.hatches.map(id => this.ents.get(id)).filter(h => this.def(h).io === 'out');
    if (!c.crafting) {
      const total = k => ins.reduce((a, h) => a + (h.buf[k] || 0), 0);
      if (!Object.entries(r.in).every(([k, n]) => total(k) >= n)) { c.status = 'waiting for input'; return; }
      for (const [k, n] of Object.entries(r.in)) {   // consumed from the input buses at craft start
        let left = n;
        for (const h of ins) { const take = Math.min(left, h.buf[k] || 0); h.buf[k] -= take; if (!h.buf[k]) delete h.buf[k]; left -= take; }
      }
      c.crafting = true; c.prog = 0;
    }
    c.prog = Math.min(1, c.prog + this.dt / r.time);
    if (c.prog < 1) { c.status = 'working'; return; }
    for (const [k, n] of Object.entries(r.out)) {
      const h = outs.find(o => (o.buf[k] || 0) + n <= Math.max(this.c.outputCap, n));
      if (!h) { c.status = 'output blocked'; return; }
    }
    for (const [k, n] of Object.entries(r.out)) {
      const h = outs.find(o => (o.buf[k] || 0) + n <= Math.max(this.c.outputCap, n));
      h.buf[k] = (h.buf[k] || 0) + n;
    }
    c.crafting = false; c.prog = 0; c.status = 'working';
  }

  // Output buses push one item per tick through their facing side.
  stepHatch(h) {
    if (this.def(h).io !== 'out') return;
    for (const [k, n] of Object.entries(h.buf)) {
      if (n > 0 && this.push(h, h.dir, k)) { if (n === 1) delete h.buf[k]; else h.buf[k] = n - 1; break; }
    }
  }

  stepController(c) {
    const inner = this.dims.get(c.inner);
    for (let d = 0; d < 4; d++) {
      const item = c.portBuf[d];
      if (!item) continue;
      if (c.ports[d] === 'in') {
        const pt = this.portTile(inner, d);
        const t = this.at(c.inner, pt.x, pt.y);
        if (t && this.accept(t, item, d)) c.portBuf[d] = null;
      } else if (c.ports[d] === 'out') {
        if (this.push(c, d, item)) c.portBuf[d] = null;
      }
    }
  }

  // ---------- save / load ----------
  serialize() {
    this.flushSegs();
    return {
      version: 2,
      nextId: this.nextId, tick: this.tick, rootId: this.rootId, ctrlCount: this.ctrlCount,
      inventory: this.inventory, freeBuild: this.freeBuild, tech: this.tech,
      dims: [...this.dims.values()], ents: [...this.ents.values()].map(e => ({ ...e, formed: null })),
    };
  }
  static deserialize(content, s) {
    if (s.version !== 2) throw new Error('Save is from an older prototype version');
    const w = new World(content);
    Object.assign(w, { nextId: s.nextId, tick: s.tick, rootId: s.rootId, ctrlCount: s.ctrlCount, inventory: s.inventory, freeBuild: s.freeBuild, tech: s.tech });
    w.dims = new Map(s.dims.map(d => [d.id, d]));
    w.ents = new Map(s.ents.map(e => [e.id, e]));
    w.segs = []; w.segDirty = true;
    return w;
  }
}
