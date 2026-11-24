// Rendering, input and UI for the prototype. Reads/writes the World from sim.js.
(() => {
  const C = CONTENT;
  const SAVE_KEY = 'compacted-proto-save';
  const T = 64, WALL = 18;
  const COL = { in: '#4cc38a', out: '#f0b44c', off: '#5a606c', fluid: '#4ea8f0', danger: '#e5605a', bg: '#15171c', fuel: '#f08a3c' };
  const STATUS_COL = { 'working': COL.in, 'waiting for input': '#9aa1ad', 'needs fuel': COL.fuel, 'output blocked': COL.out, 'no recipe': COL.danger, 'not formed': COL.danger };
  const TABS = [['logistics', 'Logistics'], ['machines', 'Machines'], ['other', 'Other']];
  const TRASH = '<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h16M10 11v6M14 11v6M5 7l1 12a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2l1-12M9 7V4h6v3"/></svg>';
  const ROTATES = ['belt', 'machine', 'hatch'];

  let world = new World(C);
  const $ = id => document.getElementById(id);
  const cv = $('cv'), g = cv.getContext('2d');
  const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  const itemName = k => C.items[k] ? C.items[k].name : C.fluids[k] ? C.fluids[k].name : k;
  const itemColor = k => C.items[k] ? C.items[k].color : C.fluids[k] ? C.fluids[k].color : '#fff';
  const placeCount = type => (C.buildings[type].free || world.freeBuild) ? '∞' : world.invCount(type);
  const dot = k => `<span class="dot" style="background:${itemColor(k)}"></span>`;
  const ml = n => n >= 1000 ? (n / 1000).toFixed(n >= 10000 ? 0 : 1) + ' L' : Math.round(n) + ' ml';
  const recipeLabel = rid => { const r = C.recipes[rid]; return [...Object.keys(r.out), ...Object.keys(r.fluidOut || {})].map(itemName).join(', '); };

  const ui = {
    dim: world.rootId, cam: { x: 0, y: 0 }, keys: new Set(), z: 1,
    toolbar: [...C.defaultToolbar], slot: -1, dir: 1, destroy: false, drag: null,
    hover: null, mouseIn: false, popupEnt: null, live: {}, panel: null, panelCache: '', techSel: null,
    browserOpen: false, browserTab: 'logistics', paused: false, speed: 1, anim: null, plan: null,
  };

  // ---------- helpers ----------
  const curDim = () => world.dims.get(ui.dim);
  const origin = () => { const s = curDim().size; return { x: -s * T / 2, y: -s * T / 2 }; };
  function screenToTile(sx, sy) {
    const wx = (sx - innerWidth / 2) / ui.z + ui.cam.x, wy = (sy - innerHeight / 2) / ui.z + ui.cam.y;
    const o = origin();
    return { x: Math.floor((wx - o.x) / T), y: Math.floor((wy - o.y) / T) };
  }
  function tileToScreen(x, y) {
    const o = origin();
    return { x: (o.x + x * T - ui.cam.x) * ui.z + innerWidth / 2, y: (o.y + y * T - ui.cam.y) * ui.z + innerHeight / 2 };
  }
  let toastTimer;
  function toast(msg) {
    const el = $('toast'); el.textContent = msg; el.classList.add('show');
    clearTimeout(toastTimer); toastTimer = setTimeout(() => el.classList.remove('show'), 2000);
  }

  // ---------- drawing primitives ----------
  function rr(c, x, y, w, h, r) { c.beginPath(); c.roundRect(x, y, w, h, r); }
  function tri(c, cx, cy, d, s) {
    c.save(); c.translate(cx, cy); c.rotate(d * Math.PI / 2);
    c.beginPath(); c.moveTo(0, -s); c.lineTo(s * 0.9, s * 0.6); c.lineTo(-s * 0.9, s * 0.6); c.closePath(); c.fill();
    c.restore();
  }
  function droplet(c, cx, cy, r) {
    c.beginPath();
    c.moveTo(cx, cy - r * 1.3);
    c.bezierCurveTo(cx + r * 0.6, cy - r * 0.5, cx + r, cy, cx + r, cy + r * 0.3);
    c.arc(cx, cy + r * 0.3, r, 0, Math.PI);
    c.bezierCurveTo(cx - r, cy, cx - r * 0.6, cy - r * 0.5, cx, cy - r * 1.3);
    c.fill();
  }
  function circle(c, x, y, r) { c.beginPath(); c.arc(x, y, r, 0, Math.PI * 2); }
  function drawItem(c, x, y, k, r = 9) {
    c.fillStyle = itemColor(k); circle(c, x, y, r); c.fill();
    c.strokeStyle = 'rgba(0,0,0,.55)'; c.lineWidth = 1.5; c.stroke();
  }
  function label(c, text, cx, cy, S, color = '#e6e8ec') {
    c.fillStyle = color; c.font = `600 ${Math.round(S * 0.17)}px system-ui`; c.textAlign = 'center'; c.textBaseline = 'middle';
    c.fillText(text, cx, cy);
  }
  function bar(c, x, y, w, h, frac, color) {
    c.fillStyle = 'rgba(0,0,0,.4)'; c.fillRect(x, y, w, h);
    c.fillStyle = color; c.fillRect(x, y + h * (1 - frac), w, h * frac);
  }

  // Draws one building in a tile at (x, y) of size S. e is the live entity, or null for icons/previews.
  function drawBuilding(c, type, dir, x, y, S, e) {
    const b = C.buildings[type], cx = x + S / 2, cy = y + S / 2;
    if (b.kind === 'belt') {
      c.fillStyle = '#2b2f37'; rr(c, x + 2, y + 2, S - 4, S - 4, 4); c.fill();
      c.save(); c.translate(cx, cy); c.rotate(dir * Math.PI / 2);
      c.strokeStyle = 'rgba(216,200,106,.55)'; c.lineWidth = S * 0.06; c.lineCap = 'round'; c.lineJoin = 'round';
      for (const off of [-S * 0.14, S * 0.14]) { c.beginPath(); c.moveTo(-S * 0.18, off + S * 0.09); c.lineTo(0, off - S * 0.09); c.lineTo(S * 0.18, off + S * 0.09); c.stroke(); }
      c.restore();
      return;
    }
    if (b.kind === 'pipe') {
      const arms = [];
      for (let d = 0; d < 4; d++) {
        if (!e) { if (d % 2 === 1) arms.push(d); continue; }
        const n = world.fluidNeighbor(e, d);
        if (n && (n.kind === 'pipe' || pipeFeeds(n, opp(d)))) arms.push(d);
      }
      const seg = e && world.segOf.get(e.id), fill = seg ? world.segFill(seg) : 0;
      const fc = seg && seg.fluid ? itemColor(seg.fluid) : null;
      c.lineCap = 'round';
      for (const [w, col] of [[S * 0.3, '#4a5160'], [S * 0.16, '#1b1e25']]) {
        c.strokeStyle = col; c.lineWidth = w;
        for (const d of arms) { c.beginPath(); c.moveTo(cx, cy); c.lineTo(cx + DIRS[d].x * S / 2, cy + DIRS[d].y * S / 2); c.stroke(); }
        c.fillStyle = col; circle(c, cx, cy, w * 0.75); c.fill();
      }
      if (fc && fill > 0) {
        c.globalAlpha *= 0.25 + 0.75 * fill; c.strokeStyle = fc; c.fillStyle = fc; c.lineWidth = S * 0.16;
        for (const d of arms) { c.beginPath(); c.moveTo(cx, cy); c.lineTo(cx + DIRS[d].x * S / 2, cy + DIRS[d].y * S / 2); c.stroke(); }
        circle(c, cx, cy, S * 0.12); c.fill();
        c.globalAlpha /= 0.25 + 0.75 * fill;
      }
      return;
    }
    if (b.kind === 'machine' || b.kind === 'core') {
      c.fillStyle = b.color; rr(c, x + 5, y + 5, S - 10, S - 10, S * 0.12); c.fill();
      c.strokeStyle = b.kind === 'core' ? '#3fb5a3' : 'rgba(255,255,255,.18)'; c.lineWidth = b.kind === 'core' ? 2 : 1.5; c.stroke();
      if (b.kind === 'machine') { c.fillStyle = COL.out; tri(c, cx + DIRS[dir].x * (S / 2 - 9), cy + DIRS[dir].y * (S / 2 - 9), dir, S * 0.09); }
      label(c, b.label, cx, cy - (e ? S * 0.05 : 0), S);
      if (e) {
        c.fillStyle = STATUS_COL[e.status] || '#9aa1ad'; circle(c, x + S - 15, y + 15, 4); c.fill();
        if (e.recipe) { const r = C.recipes[e.recipe]; const k = Object.keys(r.out)[0] || Object.keys(r.fluidOut || {})[0]; if (k) drawItem(c, x + 15, y + 15, k, 5); }
        if (b.kind === 'machine') {
          const out = Object.values(e.outBuf).reduce((a, n) => a + n, 0);
          if (out > 0) { c.fillStyle = '#cfd3da'; c.font = `${Math.round(S * 0.15)}px system-ui`; c.fillText(`×${out}`, cx, cy + S * 0.17); }
          const fl = Object.values(e.fIn).concat(Object.values(e.fOut)).reduce((a, n) => a + n, 0);
          if (fl > 0) bar(c, x + 8, y + 20, 3, S - 40, Math.min(1, fl / C.fluidBuffer), COL.fluid);
          const fc = world.fuelCap(e);
          if (fc) bar(c, x + S - 11, y + 20, 3, S - 40, e.fuel / fc, COL.fuel);
        }
        if (e.crafting) {
          c.fillStyle = 'rgba(0,0,0,.35)'; c.fillRect(x + 11, y + S - 15, S - 22, 3);
          c.fillStyle = COL.in; c.fillRect(x + 11, y + S - 15, (S - 22) * e.prog, 3);
        }
      }
      return;
    }
    if (b.kind === 'part') {
      c.fillStyle = b.color; rr(c, x + 5, y + 5, S - 10, S - 10, 4); c.fill();
      c.strokeStyle = 'rgba(240,180,110,.45)'; c.lineWidth = 2;
      for (let i = 1; i <= 3; i++) { c.beginPath(); c.moveTo(x + 9, y + 5 + i * (S - 10) / 4); c.lineTo(x + S - 9, y + 5 + i * (S - 10) / 4); c.stroke(); }
      label(c, b.label, cx, cy, S);
      return;
    }
    if (b.kind === 'hatch') {
      c.fillStyle = b.color; rr(c, x + 5, y + 5, S - 10, S - 10, 4); c.fill();
      c.strokeStyle = b.io === 'in' ? COL.in : COL.out; c.lineWidth = 1.5; c.stroke();
      c.fillStyle = b.io === 'in' ? COL.in : COL.out;
      tri(c, cx + DIRS[dir].x * (S / 2 - 9), cy + DIRS[dir].y * (S / 2 - 9), b.io === 'in' ? opp(dir) : dir, S * 0.09);
      label(c, b.label, cx, cy - (e ? S * 0.05 : 0), S);
      if (e) {
        const n = Object.values(e.buf).reduce((a, v) => a + v, 0);
        if (n > 0) { c.fillStyle = '#cfd3da'; c.font = `${Math.round(S * 0.15)}px system-ui`; c.fillText(`×${n}`, cx, cy + S * 0.17); }
      }
      return;
    }
    if (b.kind === 'controller') {
      c.fillStyle = '#2a2140'; rr(c, x + 4, y + 4, S - 8, S - 8, 6); c.fill();
      c.strokeStyle = '#9a7ad0'; c.lineWidth = 2; c.stroke();
      c.strokeStyle = 'rgba(154,122,208,.4)'; c.lineWidth = 1; rr(c, x + 10, y + 10, S - 20, S - 20, 3); c.stroke();
      label(c, `${b.size}×${b.size}`, cx, cy - (e ? S * 0.07 : 0), S);
      if (e) {
        c.font = `${Math.round(S * 0.13)}px system-ui`; c.fillStyle = '#b9a6e0';
        let name = e.name;
        while (name.length > 1 && c.measureText(name).width > S - 22) name = name.slice(0, -1);
        c.fillText(name === e.name ? name : name.slice(0, -1) + '…', cx, cy + S * 0.15);
        for (let d = 0; d < 4; d++) {
          const m = e.ports[d];
          if (m === 'off') continue;
          c.fillStyle = COL[m];
          const px = cx + DIRS[d].x * (S / 2 - 5), py = cy + DIRS[d].y * (S / 2 - 5);
          if (m === 'fluid') droplet(c, px, py, S * 0.06);
          else tri(c, px, py, m === 'in' ? opp(d) : d, S * 0.08);
        }
      }
    }
  }

  // Does machine m exchange fluid through its side `side` (fluid out of its output side, fluid in through the others)?
  function pipeFeeds(m, side) {
    if (m.kind !== 'machine' || !m.recipe) return false;
    const r = C.recipes[m.recipe];
    return (r.fluidOut && m.dir === side) || (r.fluidIn && m.dir !== side);
  }

  function icon(type, size) {
    const k = document.createElement('canvas');
    const dpr = devicePixelRatio || 1;
    k.width = k.height = size * dpr; k.style.width = k.style.height = size + 'px';
    const c = k.getContext('2d'); c.scale(dpr, dpr);
    drawBuilding(c, type, ROTATES.includes(C.buildings[type].kind) ? 1 : 0, 0, 0, size, null);
    return k;
  }

  // ---------- world rendering ----------
  function render() {
    const dpr = devicePixelRatio || 1;
    if (cv.width !== innerWidth * dpr || cv.height !== innerHeight * dpr) {
      cv.width = innerWidth * dpr; cv.height = innerHeight * dpr;
      cv.style.width = innerWidth + 'px'; cv.style.height = innerHeight + 'px';
    }
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    g.fillStyle = COL.bg; g.fillRect(0, 0, innerWidth, innerHeight);
    let alpha = 1;
    ui.z = 1;
    if (ui.anim) {
      const t = Math.min(1, (performance.now() - ui.anim.t0) / 280), k = 1 - Math.pow(1 - t, 3);
      ui.z = ui.anim.from + (1 - ui.anim.from) * k; alpha = 0.25 + 0.75 * k;
      if (t >= 1) { ui.anim = null; ui.z = 1; }
    }
    if (world.segDirty) world.rebuildSegs();
    g.save();
    g.translate(innerWidth / 2, innerHeight / 2); g.scale(ui.z, ui.z); g.translate(-ui.cam.x, -ui.cam.y);
    g.globalAlpha = alpha;
    drawDimension();
    g.restore();
  }

  function drawDimension() {
    const dim = curDim(), s = dim.size, o = origin(), root = dim.ctrlId == null;
    g.fillStyle = root ? '#2a2e38' : '#3a3f4b';
    rr(g, o.x - WALL, o.y - WALL, s * T + WALL * 2, s * T + WALL * 2, 8); g.fill();
    for (let y = 0; y < s; y++) for (let x = 0; x < s; x++) {
      g.fillStyle = '#1d2027'; g.fillRect(o.x + x * T + 1, o.y + y * T + 1, T - 2, T - 2);
    }
    if (!root) {
      const c = world.ents.get(dim.ctrlId);
      for (let d = 0; d < 4; d++) drawWallPort(o, s, d, c.ports[d], c.portBuf[d]);
    }
    const items = [];
    for (let y = 0; y < s; y++) for (let x = 0; x < s; x++) {
      const e = world.at(ui.dim, x, y);
      if (!e) continue;
      drawBuilding(g, e.type, e.dir, o.x + x * T, o.y + y * T, T, e);
      if (e.kind === 'belt' && e.item) items.push(e);
    }
    for (const e of world.ents.values()) {   // formed multiblock outlines
      if (e.kind !== 'core' || e.dim !== ui.dim || !e.formed) continue;
      const r = e.formed.rect;
      g.strokeStyle = '#3fb5a3'; g.lineWidth = 2.5; g.setLineDash([]);
      rr(g, o.x + r.x * T + 2, o.y + r.y * T + 2, r.w * T - 4, r.h * T - 4, 8); g.stroke();
    }
    for (const e of items) {
      const cx = o.x + e.x * T + T / 2, cy = o.y + e.y * T + T / 2, h = T / 2;
      const a = { x: cx + DIRS[e.from].x * h, y: cy + DIRS[e.from].y * h }, b = { x: cx + DIRS[e.dir].x * h, y: cy + DIRS[e.dir].y * h };
      const p = e.prog < 0.5 ? lerp(a, { x: cx, y: cy }, e.prog * 2) : lerp({ x: cx, y: cy }, b, (e.prog - 0.5) * 2);
      drawItem(g, p.x, p.y, e.item);
    }
    drawOverlays(o, dim);
  }
  const lerp = (a, b, t) => ({ x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t });

  function drawWallPort(o, s, d, mode, buf) {
    const cx = o.x + s * T / 2 + DIRS[d].x * (s * T / 2 + WALL / 2), cy = o.y + s * T / 2 + DIRS[d].y * (s * T / 2 + WALL / 2);
    g.fillStyle = COL[mode]; circle(g, cx, cy, 13); g.fill();
    g.fillStyle = COL.bg;
    if (mode === 'in') tri(g, cx, cy + (d === 0 ? 1 : d === 2 ? -1 : 0), opp(d), 7);
    else if (mode === 'out') tri(g, cx, cy, d, 7);
    else if (mode === 'fluid') droplet(g, cx, cy, 6);
    else g.fillRect(cx - 6, cy - 1.5, 12, 3);
    if (buf) {
      const px = d % 2 === 0 ? 22 : 0, py = d % 2 === 1 ? 22 : 0;
      drawItem(g, cx + px, cy + py, buf, 6);
    }
  }

  function drawOverlays(o, dim) {
    const h = ui.hover;
    const inb = h && ui.mouseIn && world.inBounds(dim, h.x, h.y);
    if (ui.destroy) {
      if (ui.drag) {
        const d = ui.drag, x0 = Math.min(d.x0, d.x1), x1 = Math.max(d.x0, d.x1), y0 = Math.min(d.y0, d.y1), y1 = Math.max(d.y0, d.y1);
        g.fillStyle = 'rgba(229,96,90,.22)'; g.strokeStyle = COL.danger; g.lineWidth = 2; g.setLineDash([6, 4]);
        g.fillRect(o.x + x0 * T, o.y + y0 * T, (x1 - x0 + 1) * T, (y1 - y0 + 1) * T);
        g.strokeRect(o.x + x0 * T, o.y + y0 * T, (x1 - x0 + 1) * T, (y1 - y0 + 1) * T); g.setLineDash([]);
      } else if (inb) {
        g.strokeStyle = COL.danger; g.lineWidth = 2; g.strokeRect(o.x + h.x * T + 2, o.y + h.y * T + 2, T - 4, T - 4);
      }
      return;
    }
    const type = ui.slot >= 0 ? ui.toolbar[ui.slot] : null;
    if (type && C.buildings[type].placement === 'wall') {   // hint which tiles touch a wall
      g.fillStyle = 'rgba(91,156,240,.07)';
      for (let y = 0; y < dim.size; y++) for (let x = 0; x < dim.size; x++)
        if (world.touchesWall(dim, x, y) && !world.at(ui.dim, x, y)) g.fillRect(o.x + x * T + 1, o.y + y * T + 1, T - 2, T - 2);
    }
    if (!inb) return;
    if (type) {
      const ok = world.canPlace(type, ui.dim, h.x, h.y).ok;
      g.save(); g.globalAlpha *= 0.55;
      drawBuilding(g, type, ROTATES.includes(C.buildings[type].kind) ? ui.dir : 0, o.x + h.x * T, o.y + h.y * T, T, null);
      g.restore();
      g.strokeStyle = ok ? 'rgba(91,156,240,.9)' : COL.danger; g.lineWidth = 2; g.setLineDash([5, 4]);
      g.strokeRect(o.x + h.x * T + 2, o.y + h.y * T + 2, T - 4, T - 4); g.setLineDash([]);
    } else if (world.at(ui.dim, h.x, h.y)) {
      g.strokeStyle = 'rgba(255,255,255,.35)'; g.lineWidth = 1.5; g.strokeRect(o.x + h.x * T + 2, o.y + h.y * T + 2, T - 4, T - 4);
    }
  }

  // ---------- navigation ----------
  function enter(c) {
    closePopup(); ui.drag = null;
    ui.dim = c.inner; ui.cam = { x: 0, y: 0 }; ui.anim = { t0: performance.now(), from: 0.3 };
    updateCrumb();
  }
  function goToDim(id) {
    if (id === ui.dim) return;
    closePopup(); ui.drag = null;
    ui.dim = id; ui.cam = { x: 0, y: 0 }; ui.anim = { t0: performance.now(), from: 2.6 };
    updateCrumb();
  }
  function leave() { const p = world.parentDimOf(ui.dim); if (p != null) goToDim(p); }
  function updateCrumb() {
    const path = [];
    for (let d = ui.dim; d != null; d = world.parentDimOf(d)) path.unshift(d);
    $('crumb').innerHTML = path.map((id, i) => {
      const dim = world.dims.get(id);
      const name = dim.ctrlId == null ? 'Root' : esc(world.ents.get(dim.ctrlId).name);
      const last = i === path.length - 1;
      return `<span class="${last ? 'cur' : 'c'}" data-dim="${id}">${name}</span>` + (last ? '' : '<span class="sep">›</span>');
    }).join('');
    $('leave').classList.toggle('hidden', curDim().ctrlId == null);
  }
  $('crumb').onclick = ev => { const id = ev.target.dataset.dim; if (id) goToDim(+id); };
  $('leave').onclick = leave;

  // ---------- toolbar & browser ----------
  function buildBar() {
    const bar = $('bar'); bar.innerHTML = '';
    ui.toolbar.forEach((t, i) => {
      const d = document.createElement('div');
      d.className = 'slot' + (ui.slot === i ? ' sel' : '');
      d.title = t ? C.buildings[t].name : 'Empty slot';
      d.innerHTML = `<span class="k">${i + 1}</span>`;
      if (t) {
        d.appendChild(icon(t, 36));
        const n = document.createElement('span'); n.className = 'n'; n.dataset.count = t; d.appendChild(n);
      }
      d.onclick = () => selectSlot(i);
      bar.appendChild(d);
    });
    const div = document.createElement('div'); div.className = 'divider'; bar.appendChild(div);
    const del = document.createElement('div');
    del.className = 'slot destroy' + (ui.destroy ? ' sel' : '');
    del.title = 'Destroy (Q)'; del.innerHTML = `<span class="k">Q</span>${TRASH}`;
    del.onclick = toggleDestroy;
    bar.appendChild(del);
  }
  function selectSlot(i, fromKey) {
    if (ui.slot === i) { if (fromKey) deselect(); else openBrowser(); return; }
    ui.slot = i; ui.destroy = false; ui.drag = null;
    closeBrowser();
    buildBar();
  }
  function deselect() { ui.slot = -1; closeBrowser(); buildBar(); }
  function toggleDestroy() {
    ui.destroy = !ui.destroy; ui.drag = null;
    if (ui.destroy) { ui.slot = -1; closeBrowser(); closePopup(); }
    buildBar();
  }
  function openBrowser() { if (ui.slot < 0) return; if (ui.panel) togglePanel(ui.panel); ui.browserOpen = true; renderBrowser(); }
  function closeBrowser() { ui.browserOpen = false; $('browser').classList.add('hidden'); }
  function renderBrowser() {
    const el = $('browser'); el.classList.remove('hidden'); el.innerHTML = '';
    const head = document.createElement('div'); head.className = 'ph';
    head.innerHTML = `<span>Block browser · slot ${ui.slot + 1}</span><button>✕</button>`;
    head.querySelector('button').onclick = closeBrowser;
    el.appendChild(head);
    const tabs = document.createElement('div'); tabs.className = 'tabs';
    for (const [id, name] of TABS) {
      const t = document.createElement('div'); t.className = 'tab' + (ui.browserTab === id ? ' on' : ''); t.textContent = name;
      t.onclick = () => { ui.browserTab = id; renderBrowser(); };
      tabs.appendChild(t);
    }
    el.appendChild(tabs);
    const grid = document.createElement('div'); grid.className = 'grid';
    for (const [type, b] of Object.entries(C.buildings)) {
      if (b.tab !== ui.browserTab) continue;
      const it = document.createElement('div'); it.className = 'bi';
      it.appendChild(icon(type, 64));
      it.insertAdjacentHTML('beforeend', `${esc(b.name)}<br>${placeCount(type)}`);
      it.onclick = () => { ui.toolbar[ui.slot] = type; closeBrowser(); buildBar(); };
      grid.appendChild(it);
    }
    el.appendChild(grid);
  }

  // ---------- info popup ----------
  function openPopup(e) { ui.popupEnt = e.id; buildPopup(); }
  function closePopup() { ui.popupEnt = null; $('popup').classList.add('hidden'); $('ports').classList.add('hidden'); }
  function recipeSelect(e) {
    const ids = world.recipesFor(e);
    if (e.recipe && !ids.includes(e.recipe)) ids.unshift(e.recipe);
    const opts = ids.map(id => `<option value="${id}" ${e.recipe === id ? 'selected' : ''}>${esc(recipeLabel(id))}</option>`).join('');
    return `<div class="row"><span>Recipe</span><select data-a="recipe"><option value="">Choose…</option>${opts}</select></div>`;
  }
  function recipeSummary(r) {
    const parts = [...Object.entries(r.in).map(([k, n]) => `${n} ${esc(itemName(k))}`), ...Object.entries(r.fluidIn || {}).map(([f, n]) => `${ml(n)} ${esc(itemName(f))}`)];
    if (r.fuel) parts.push(`${r.fuel} fuel`);
    return parts.length ? parts.join(' + ') : 'nothing';
  }
  function buildPopup() {
    const e = world.ents.get(ui.popupEnt);
    if (!e) return closePopup();
    const b = C.buildings[e.type], p = $('popup');
    let h = `<div class="ph"><span>${esc(e.kind === 'controller' ? e.name : b.name)}</span><button data-a="close">✕</button></div>`;
    if (e.kind === 'machine' || e.kind === 'core') {
      h += recipeSelect(e);
      if (e.kind === 'core') h += `<div class="sec">Structure</div><div data-l="form"></div>`;
      if (e.recipe) {
        const r = C.recipes[e.recipe];
        h += `<div class="sec">Input · needs ${recipeSummary(r)}</div><div data-l="in"></div>`;
        h += `<div class="row"><span>Time</span><b>${r.time}s</b></div>`;
      }
      h += `<div class="sec">Output</div><div data-l="out"></div>`;
      h += `<div class="row"><span>Status</span><span data-l="status"></span></div><div class="prog"><div data-l="prog"></div></div>`;
    } else if (e.kind === 'belt') {
      h += `<div class="row"><span>Carrying</span><span data-l="bitem"></span></div>`;
      h += `<div class="row"><span>Throughput</span><b data-l="brate"></b></div>`;
    } else if (e.kind === 'pipe') {
      h += `<div data-l="pipe"></div>`;
    } else if (e.kind === 'hatch') {
      h += `<div class="row"><span>Facing</span><b>${DIR_NAMES[e.dir]} · ${b.io === 'in' ? 'accepts from' : 'outputs to'} that side only</b></div>`;
      h += `<div data-l="hatch"></div>`;
    } else if (e.kind === 'part') {
      h += `<div data-l="part"></div>`;
    } else if (e.kind === 'controller') {
      h += `<div class="row"><span>Name</span><input type="text" data-a="name" value="${esc(e.name)}" maxlength="24" style="width:140px"></div>`;
      h += `<div class="row"><span>Interior</span><b>${b.size}×${b.size}</b></div>`;
      h += `<div class="row"><span>Blocks inside</span><b data-l="count"></b></div>`;
      h += `<div class="acts"><button data-a="enter" class="on">Enter</button></div>`;
    }
    p.innerHTML = h; p.classList.remove('hidden');
    ui.live = {};
    if (e.kind === 'controller') buildPorts(e);
    else if (e.kind === 'machine') buildSides(e);
    else $('ports').classList.add('hidden');
    updatePopup();
  }
  function setLive(p, key, html) {
    if (ui.live[key] === html) return;
    const el = p.querySelector(`[data-l="${key}"]`);
    if (el) { el.innerHTML = html; ui.live[key] = html; }
  }
  const row = (l, r) => `<div class="row"><span>${l}</span>${r}</div>`;
  const addBtn = k => `<button data-a="insert" data-v="${k}" title="Insert by hand from inventory">+ ${world.invCount(k)}</button>`;
  function updatePopup() {
    if (ui.popupEnt == null) return;
    const e = world.ents.get(ui.popupEnt), p = $('popup');
    if (!e || e.dim !== ui.dim) return closePopup();
    const s = tileToScreen(e.x, e.y);
    let left = s.x + T * ui.z + 12, top = s.y - 20;
    if (left + p.offsetWidth > innerWidth - 50) left = s.x - p.offsetWidth - 12;
    top = Math.max(50, Math.min(top, innerHeight - p.offsetHeight - 90));
    p.style.left = left + 'px'; p.style.top = top + 'px';
    if (e.kind === 'controller' || e.kind === 'machine') {
      const pw = $('ports');
      let pl = left + p.offsetWidth + 8;
      if (pl + pw.offsetWidth > innerWidth - 50) pl = left - pw.offsetWidth - 8;
      pw.style.left = pl + 'px'; pw.style.top = top + 'px';
    }
    const r = e.recipe ? C.recipes[e.recipe] : null;
    if (e.kind === 'machine') {
      if (r) {
        let h = Object.keys(r.in).map(k => row(`${dot(k)}${esc(itemName(k))}`, `<span><b>${e.inBuf[k] || 0}/${world.inputCap(r, k)}</b> ${addBtn(k)}</span>`)).join('');
        h += Object.keys(r.fluidIn || {}).map(f => row(`${dot(f)}${esc(itemName(f))}`, `<b>${ml(e.fIn[f] || 0)} / ${ml(C.fluidBuffer)}</b>`)).join('');
        if (r.fuel) {
          h += row(`<span class="dot" style="background:${COL.fuel}"></span>Fuel`, `<b>${e.fuel} / ${world.fuelCap(e)} pts</b>`);
          const fuels = Object.keys(C.items).filter(k => C.items[k].fuel && world.invCount(k) > 0);
          h += fuels.map(k => row(`&nbsp;&nbsp;${dot(k)}${esc(itemName(k))} <small>(${C.items[k].fuel} pt)</small>`, addBtn(k))).join('');
        }
        setLive(p, 'in', h);
      }
      const outs = Object.entries(e.outBuf), fouts = Object.entries(e.fOut);
      const cap = C.buildings[e.type].outputCap || C.outputCap;
      setLive(p, 'out', (outs.length || fouts.length)
        ? outs.map(([k, n]) => row(`${dot(k)}${esc(itemName(k))}`, `<b>${n} / ${cap}</b>`)).join('')
          + fouts.map(([f, n]) => row(`${dot(f)}${esc(itemName(f))}`, `<b>${ml(n)} / ${ml(C.fluidBuffer)}</b>`)).join('')
          + (outs.length ? `<div class="acts"><button data-a="take">Take all</button></div>` : '')
        : row('Empty', ''));
    }
    if (e.kind === 'core') {
      const mb = C.multiblocks[C.buildings[e.type].multiblock];
      setLive(p, 'form', e.formed
        ? row(`<span style="color:${COL.in}">✓ ${esc(mb.name)} formed</span>`, '')
        : `<div class="row"><span style="color:${COL.danger}">Not formed</span></div><div style="color:var(--muted);font-size:12px">Needs a 2×2 of: this core, 1 coil block touching it, at least 1 input bus and 1 output bus.</div>`);
      const hs = e.formed ? e.formed.hatches.map(id => world.ents.get(id)) : [];
      if (r) {
        const total = k => hs.filter(x => C.buildings[x.type].io === 'in').reduce((a, x) => a + (x.buf[k] || 0), 0);
        setLive(p, 'in', Object.keys(r.in).map(k => row(`${dot(k)}${esc(itemName(k))}`, `<b>${total(k)} / ${r.in[k]}</b>`)).join('') + `<div style="color:var(--muted);font-size:12px">Fill the input buses (click a bus to insert by hand).</div>`);
      }
      const outTot = {};
      for (const x of hs) if (C.buildings[x.type].io === 'out') for (const [k, n] of Object.entries(x.buf)) outTot[k] = (outTot[k] || 0) + n;
      setLive(p, 'out', Object.keys(outTot).length ? Object.entries(outTot).map(([k, n]) => row(`${dot(k)}${esc(itemName(k))}`, `<b>${n}</b>`)).join('') + `<div style="color:var(--muted);font-size:12px">Held in the output bus.</div>` : row('Empty', ''));
    }
    if (e.kind === 'machine' || e.kind === 'core') {
      setLive(p, 'status', `<span style="color:${STATUS_COL[e.status] || 'inherit'}">${e.status}</span>`);
      const pb = p.querySelector('[data-l="prog"]');
      if (pb) pb.style.width = (e.crafting ? e.prog * 100 : 0) + '%';
    } else if (e.kind === 'belt') {
      setLive(p, 'bitem', e.item ? `${dot(e.item)}${esc(itemName(e.item))}` : 'Nothing');
      setLive(p, 'brate', world.beltRate(e).toFixed(1) + ' /s');
    } else if (e.kind === 'pipe') {
      const s = world.segOf.get(e.id);
      setLive(p, 'pipe', s ? [
        row('Fluid', s.fluid && s.amt > 0 ? `<span>${dot(s.fluid)}${esc(itemName(s.fluid))}</span>` : '<b>Empty</b>'),
        row('This pipe', `<b>${ml(world.pipeAmount(e))} / ${ml(C.pipeCapacity)}</b>`),
        row('Segment', `<b>${ml(s.amt)} / ${ml(s.cap)} (${Math.round(world.segFill(s) * 100)}%)</b>`),
        row('Pipes in segment', `<b>${s.pipes.length}</b>`),
      ].join('') : row('Segment', '<b>…</b>'));
    } else if (e.kind === 'hatch') {
      const core = world.ents.get(e.core), formed = core && core.formed && core.formed.hatches.includes(e.id);
      const io = C.buildings[e.type].io;
      let h = row('Structure', formed ? `<b style="color:${COL.in}">Formed</b>` : `<b style="color:${COL.danger}">Not in a formed structure</b>`);
      const items = Object.entries(e.buf);
      h += items.length ? items.map(([k, n]) => row(`${dot(k)}${esc(itemName(k))}`, `<b>${n}</b>`)).join('') : row('Empty', '');
      if (io === 'out' && items.length) h += `<div class="acts"><button data-a="take">Take all</button></div>`;
      if (io === 'in' && formed && core.recipe) {
        const cr = C.recipes[core.recipe];
        h += `<div class="sec">Insert by hand</div>` + Object.keys(cr.in).map(k => row(`${dot(k)}${esc(itemName(k))} <small>${e.buf[k] || 0}/${world.inputCap(cr, k)}</small>`, addBtn(k))).join('');
      }
      setLive(p, 'hatch', h);
    } else if (e.kind === 'part') {
      const core = [...world.ents.values()].find(c => c.kind === 'core' && c.formed && c.formed.parts.includes(e.id));
      setLive(p, 'part', row('Structure', core ? `<b style="color:${COL.in}">Part of a formed ${esc(C.multiblocks[C.buildings[core.type].multiblock].name)}</b>` : `<b style="color:var(--muted)">Not in a formed structure</b>`));
    } else if (e.kind === 'controller') {
      setLive(p, 'count', String(world.countBlocks(e.inner)));
    }
  }
  $('popup').addEventListener('click', ev => {
    const btn = ev.target.closest('[data-a]');
    if (!btn) return;
    const e = world.ents.get(ui.popupEnt);
    const a = btn.dataset.a, v = btn.dataset.v;
    if (a === 'close') return closePopup();
    if (!e) return;
    if (a === 'take') { const n = world.takeOutput(e); toast(n ? `Took ${n} item${n > 1 ? 's' : ''}` : 'Inventory is full for those items'); }
    else if (a === 'insert') {
      const n = world.insertFromInv(e, v);
      if (!n) toast(world.invCount(v) ? 'Buffer is full' : `No ${itemName(v)} in inventory`);
      ui.live = {};
    }
    else if (a === 'enter') enter(e);
  });
  $('popup').addEventListener('change', ev => {
    if (ev.target.dataset.a !== 'recipe') return;
    const e = world.ents.get(ui.popupEnt);
    const lost = world.setRecipe(e, ev.target.value);
    if (lost) toast(`Inventory full: ${lost} item${lost > 1 ? 's' : ''} lost`);
    buildPopup();
  });
  $('popup').addEventListener('input', ev => {
    if (ev.target.dataset.a !== 'name') return;
    const e = world.ents.get(ui.popupEnt);
    e.name = ev.target.value.trim() || 'Box';
    updateCrumb();
  });

  // ---------- port window (controllers) and output-side window (machines) ----------
  const PORT_LABEL = { in: 'Item input', out: 'Item output', fluid: 'Fluid I/O', off: 'Off' };
  const NEXT_MODE = { off: 'in', in: 'out', out: 'fluid', fluid: 'off' }, PREV_MODE = { off: 'fluid', fluid: 'out', out: 'in', in: 'off' };
  const DROPLET_SVG = '<svg viewBox="-10 -10 20 20" width="18" height="18"><path d="M0,-8 C3.5,-3 6,0 6,2.5 A6,6 0 0 1 -6,2.5 C-6,0 -3.5,-3 0,-8Z" fill="currentColor"/></svg>';
  const GRID_POS = [[2, 1], [3, 2], [2, 3], [1, 2]];
  const arrowSvg = d => `<svg viewBox="-10 -10 20 20" width="18" height="18"><path d="M0,-7 L6.5,4 L-6.5,4Z" transform="rotate(${d * 90})" fill="currentColor"/></svg>`;
  function buildPorts(c) {
    const b = C.buildings[c.type];
    let h = `<div class="ph"><span>Ports</span></div><div class="pgrid">`;
    for (let d = 0; d < 4; d++) {
      const m = c.ports[d], [col, rw] = GRID_POS[d];
      const glyph = m === 'in' ? arrowSvg(opp(d)) : m === 'out' ? arrowSvg(d) : m === 'fluid' ? DROPLET_SVG : '<span class="dash"></span>';
      h += `<button class="pbtn ${m}" data-port="${d}" style="grid-column:${col};grid-row:${rw}" title="${['North', 'East', 'South', 'West'][d]}: ${PORT_LABEL[m]}">${glyph}<span class="pl">${DIR_NAMES[d]}</span></button>`;
    }
    h += `<div class="pbox" style="grid-column:2;grid-row:2">${b.size}×${b.size}</div></div>`;
    h += `<div class="plegend"><span><i style="background:var(--in)"></i>In</span><span><i style="background:var(--out)"></i>Out</span><span><i style="background:var(--fluid)"></i>Fluid</span><span><i style="background:var(--off)"></i>Off</span></div>`;
    h += `<div class="phint">Click a port to cycle · right-click to go back</div>`;
    const el = $('ports'); el.innerHTML = h; el.classList.remove('hidden');
  }
  function buildSides(e) {
    const b = C.buildings[e.type];
    let h = `<div class="ph"><span>Output side</span></div><div class="pgrid">`;
    for (let d = 0; d < 4; d++) {
      const out = e.dir === d, [col, rw] = GRID_POS[d];
      h += `<button class="pbtn ${out ? 'out' : 'in'}" data-side="${d}" style="grid-column:${col};grid-row:${rw}" title="${['North', 'East', 'South', 'West'][d]}: ${out ? 'Output' : 'Input'}">${arrowSvg(out ? d : opp(d))}<span class="pl">${DIR_NAMES[d]}</span></button>`;
    }
    h += `<div class="pbox mbox" style="grid-column:2;grid-row:2;background:${b.color}">${b.label}</div></div>`;
    h += `<div class="plegend"><span><i style="background:var(--in)"></i>Input</span><span><i style="background:var(--out)"></i>Output</span></div>`;
    h += `<div class="phint">Click a side to make it the output</div>`;
    const el = $('ports'); el.innerHTML = h; el.classList.remove('hidden');
  }
  function portClick(ev, back) {
    const side = ev.target.closest('[data-side]');
    if (side) { ev.preventDefault(); const m = world.ents.get(ui.popupEnt); m.dir = +side.dataset.side; buildSides(m); return; }
    const btn = ev.target.closest('[data-port]');
    if (!btn) return;
    ev.preventDefault();
    const c = world.ents.get(ui.popupEnt), d = +btn.dataset.port;
    const lost = world.setPortMode(c, d, (back ? PREV_MODE : NEXT_MODE)[c.ports[d]]);
    if (lost) toast('Inventory full: 1 item lost');
    buildPorts(c);
  }
  $('ports').addEventListener('click', ev => portClick(ev, false));
  $('ports').addEventListener('contextmenu', ev => portClick(ev, true));

  // ---------- destroy confirmation ----------
  function showConfirm(plan) {
    const n = plan.targets.length;
    if (!n && !plan.skipped) return;
    ui.plan = plan;
    let h = `<div class="ph"><span>${n ? `Destroy ${n} block${n > 1 ? 's' : ''}?` : 'Nothing can be destroyed'}</span></div>`;
    for (const [k, c] of Object.entries(plan.counts)) h += row(esc(C.buildings[k].name), `<b>${c}</b>`);
    if (plan.skipped) h += `<div class="warn">${plan.skipped} controller${plan.skipped > 1 ? 's' : ''} skipped: not empty</div>`;
    const lost = Object.entries(plan.lost);
    if (lost.length) h += `<div class="warn">Inventory full. These will be lost:<br>${lost.map(([k, c]) => `${c} ${esc(itemName(k))}`).join(', ')}</div>`;
    const ents = plan.targets.map(id => world.ents.get(id));
    if (ents.some(e => (e.kind === 'pipe' && world.pipeAmount(e) > 0) || (e.kind === 'machine' && (e.fuel > 0 || Object.keys(e.fIn).length || Object.keys(e.fOut).length)) || (e.kind === 'core' && e.crafting)))
      h += `<div style="color:var(--muted);margin-top:6px">Fluids, fuel and in-progress multiblock crafts inside are lost.</div>`;
    h += `<div class="acts"><button data-c="cancel">Cancel</button>${n ? '<button data-c="ok" class="danger">Destroy</button>' : ''}</div>`;
    const el = $('confirm'); el.innerHTML = h; el.classList.remove('hidden');
  }
  function closeConfirm() { ui.plan = null; $('confirm').classList.add('hidden'); }
  $('confirm').addEventListener('click', ev => {
    const c = ev.target.dataset.c;
    if (c === 'cancel') closeConfirm();
    if (c === 'ok') {
      const n = ui.plan.targets.length;
      world.execDestroy(ui.plan); closeConfirm(); closePopup();
      toast(`Destroyed ${n} block${n > 1 ? 's' : ''}`);
    }
  });

  // ---------- big windows: Materials, Tech tree, Crafting ----------
  function togglePanel(k) {
    ui.panel = ui.panel === k ? null : k;
    if (ui.panel) closeBrowser();
    ui.panelCache = '';
    $('panel').classList.toggle('hidden', !ui.panel);
    $('lblMat').classList.toggle('on', ui.panel === 'mat');
    $('lblTech').classList.toggle('on', ui.panel === 'tech');
    $('lblCraft').classList.toggle('on', ui.panel === 'craft');
  }
  $('lblMat').onclick = () => togglePanel('mat');
  $('lblTech').onclick = () => togglePanel('tech');
  $('lblCraft').onclick = () => togglePanel('craft');
  $('panel').addEventListener('click', ev => {
    if (ev.target.closest('[data-close]')) return togglePanel(ui.panel);
    const craft = ev.target.closest('[data-craft]');
    if (craft) {
      const res = world.handCraft(craft.dataset.craft);
      toast(res.ok ? `Crafted ${recipeLabel(craft.dataset.craft)}` : res.reason);
      return;
    }
    const node = ev.target.closest('[data-tech]');
    if (node) { ui.techSel = node.dataset.tech; return; }
    const done = ev.target.closest('[data-complete]');
    if (done) {
      const id = done.dataset.complete, res = world.completeTech(id);
      if (!res.ok) return toast(res.reason);
      const lost = Object.entries(res.lost);
      toast(`${C.tech[id].name} complete` + (lost.length ? ` · inventory full, lost ${lost.map(([k, n]) => `${n} ${itemName(k)}`).join(', ')}` : ''));
    }
  });

  const need = (map, take) => Object.entries(map || {}).map(([k, n]) => {
    const have = world.invCount(k);
    return `<span class="${have < n ? 'lack' : ''}">${dot(k)}${n} ${esc(itemName(k))} <small>(${have})</small></span>`;
  }).join('') || '<span>—</span>';

  function techHtml() {
    const nodes = Object.entries(C.tech);
    const CW = 160, RH = 84, NW = 136, NH = 46, PAD = 14;
    const cols = Math.max(...nodes.map(([, t]) => t.pos[0])) + 1, rows = Math.max(...nodes.map(([, t]) => t.pos[1])) + 1;
    const W = cols * CW + PAD, H = rows * RH + PAD;
    const pos = t => ({ x: PAD + t.pos[0] * CW, y: PAD + t.pos[1] * RH });
    let lines = '', boxes = '';
    for (const [id, t] of nodes) {
      const st = world.techState(id), p = pos(t);
      for (const pr of t.prereqs) {
        // Lines from every visible node are drawn, including lines to hidden next nodes (as a hint).
        if (world.techState(pr) === 'hidden') continue;
        const q = pos(C.tech[pr]);
        const x1 = q.x + NW, y1 = q.y + NH / 2, x2 = p.x, y2 = p.y + NH / 2, mx = (x1 + x2) / 2;
        lines += `<path d="M${x1},${y1} C${mx},${y1} ${mx},${y2} ${x2},${y2}" stroke="${st === 'hidden' ? '#5a606c' : st === 'done' ? '#4cc38a' : '#9aa1ad'}" stroke-width="2" fill="none" ${st === 'hidden' ? 'stroke-dasharray="5 4"' : ''}/>`;
        if (st === 'hidden') lines += `<circle cx="${x2}" cy="${y2}" r="4" fill="#5a606c"/>`;
      }
      if (st === 'hidden') continue;
      boxes += `<div class="tnode ${st}${ui.techSel === id ? ' sel' : ''}" data-tech="${id}" style="left:${p.x}px;top:${p.y}px;width:${NW}px;height:${NH}px">${st === 'done' ? '✓ ' : ''}${esc(t.name)}</div>`;
    }
    if (!ui.techSel || world.techState(ui.techSel) === 'hidden') ui.techSel = nodes.find(([id]) => world.techState(id) === 'open')?.[0] || nodes[0][0];
    const t = C.tech[ui.techSel], st = world.techState(ui.techSel);
    const rew = [];
    if (t.grant) rew.push('Grants: ' + Object.entries(t.grant).map(([k, n]) => `${n} ${esc(itemName(k))}`).join(', '));
    if (t.unlockHand) rew.push('Hand recipes: ' + t.unlockHand.map(r => esc(recipeLabel(r))).join(', '));
    if (t.unlockMachine) rew.push('Machine recipes: ' + t.unlockMachine.map(r => esc(recipeLabel(r))).join(', '));
    let d = `<div class="tdetail"><div class="ph" style="font-size:15px"><span>${esc(t.name)}</span><span style="color:var(--muted);font-size:12px">${st === 'done' ? 'Completed' : 'Available'}</span></div>`;
    if (t.cost) d += `<div class="row"><span>Hand over</span><span class="cin">${need(t.cost)}</span></div>`;
    if (t.requires) d += `<div class="row"><span>Requires (checked, not taken)</span><span class="cin">${need(t.requires)}</span></div>`;
    d += `<div class="row"><span>Rewards</span><span style="text-align:right">${rew.join('<br>') || 'None · checkpoint'}</span></div>`;
    if (st === 'open') {
      const ok = world.canCompleteTech(ui.techSel).ok;
      d += `<div class="acts"><button data-complete="${ui.techSel}" class="${ok ? 'on' : 'dim'}">${t.cost ? 'Hand over' : 'Complete'}</button></div>`;
    }
    d += '</div>';
    return `<div class="ph"><span>Tech tree <kbd>T</kbd></span><button data-close="1">✕</button></div>`
      + `<div class="tcanvas"><div style="position:relative;width:${W}px;height:${H}px"><svg width="${W}" height="${H}" style="position:absolute;left:0;top:0">${lines}</svg>${boxes}</div></div>` + d;
  }

  function updatePanel() {
    if (!ui.panel) return;
    let h;
    if (ui.panel === 'mat') {
      const r = k => row(`${dot(k)}${esc(itemName(k))}`, `<b>${world.invCount(k)} / ${C.inventoryCap}</b>`);
      const keys = Object.keys(C.items).filter(k => world.invCount(k) > 0);
      const mats = keys.filter(k => !C.items[k].building), blds = keys.filter(k => C.items[k].building);
      h = `<div class="ph"><span>Materials <kbd>Tab</kbd></span><button data-close="1">✕</button></div>`;
      h += `<div class="sec">Materials</div><div class="matgrid">` + (mats.length ? mats.map(r).join('') : row('No materials yet', '')) + '</div>';
      h += `<div class="sec">Buildings</div><div class="matgrid">` + (blds.length ? blds.map(r).join('') : row('None', '')) + '</div>';
    } else if (ui.panel === 'craft') {
      h = `<div class="ph"><span>Crafting <kbd>C</kbd></span><button data-close="1">✕</button></div>`;
      const list = world.handRecipes();
      if (!list.length) h += `<div style="color:var(--muted)">No hand recipes yet. Complete tech tree nodes to unlock them.</div>`;
      for (const rid of list) {
        const r = C.recipes[rid], outK = Object.keys(r.out)[0];
        const ok = world.hasAll(r.in);
        h += `<div class="crow"><div class="cname">${dot(outK)}${esc(recipeLabel(rid))}<small>Have ${world.invCount(outK)}</small></div><div class="cin">${need(r.in)}</div>`;
        h += `<div class="cbtns"><button class="${ok ? '' : 'dim'}" data-craft="${rid}">Craft</button></div></div>`;
      }
    } else {
      h = techHtml();
    }
    if (h !== ui.panelCache) {
      const el = $('panel'), tc = el.querySelector('.tcanvas'), sx = tc ? tc.scrollLeft : 0;
      el.innerHTML = h; ui.panelCache = h;
      const tc2 = el.querySelector('.tcanvas'); if (tc2) tc2.scrollLeft = sx;
    }
  }

  // ---------- pause menu ----------
  function openPause() { ui.paused = true; $('pause').classList.remove('hidden'); $('settings').classList.add('hidden'); }
  function closePause() { ui.paused = false; $('pause').classList.add('hidden'); }
  function resetUi() {
    ui.dim = world.rootId; ui.cam = { x: 0, y: 0 }; ui.slot = -1; ui.destroy = false; ui.drag = null;
    closePopup(); closeBrowser(); closeConfirm();
    $('optFree').checked = world.freeBuild;
    buildBar(); updateCrumb();
  }
  $('pause').addEventListener('click', ev => {
    const a = ev.target.dataset.p;
    if (a === 'resume') closePause();
    else if (a === 'save') {
      try { localStorage.setItem(SAVE_KEY, JSON.stringify({ world: world.serialize(), toolbar: ui.toolbar })); toast('Game saved'); }
      catch { toast('Couldn’t save in this browser'); }
    } else if (a === 'load') {
      try {
        const raw = localStorage.getItem(SAVE_KEY);
        if (!raw) return toast('No save found');
        const s = JSON.parse(raw);
        world = World.deserialize(C, s.world); ui.toolbar = s.toolbar || [...C.defaultToolbar];
        resetUi(); closePause(); toast('Game loaded');
      } catch (err) { toast('Couldn’t load the save: ' + err.message); }
    } else if (a === 'settings') $('settings').classList.toggle('hidden');
    else if (a === 'alltech') {
      let changed = true;
      while (changed) { changed = false; for (const id of Object.keys(C.tech)) if (world.techState(id) === 'open') { world.completeTech(id, true); changed = true; } }
      toast('All tech completed (debug)');
    } else if (a === 'quit') {
      if (confirm('Start a new game? Unsaved progress will be lost.')) {
        world = new World(C); ui.toolbar = [...C.defaultToolbar]; resetUi(); closePause();
      }
    }
  });
  $('optFree').onchange = ev => { world.freeBuild = ev.target.checked; };
  $('optSpeed').onchange = ev => { ui.speed = +ev.target.value; };

  // ---------- input ----------
  function escape() {
    if (ui.plan) return closeConfirm();
    if (ui.browserOpen) return closeBrowser();
    if (ui.popupEnt != null) return closePopup();
    if (ui.panel) return togglePanel(ui.panel);
    if (ui.paused) return closePause();
    openPause();
  }
  addEventListener('keydown', ev => {
    if (ev.target.matches && ev.target.matches('input[type=text], select')) { if (ev.key === 'Escape') ev.target.blur(); return; }
    if (ev.key === 'Escape') { ev.preventDefault(); return escape(); }
    if (ev.key === 'Tab') { ev.preventDefault(); if (!ui.paused) togglePanel('mat'); return; }
    if (ui.paused) return;
    const k = ev.key.toLowerCase();
    if ('wasd'.includes(k) && k.length === 1) ui.keys.add(k);
    else if (k === 'r') { if (ui.slot >= 0) ui.dir = (ui.dir + (ev.shiftKey ? 3 : 1)) % 4; }
    else if (k === 'q') toggleDestroy();
    else if (k === 't') togglePanel('tech');
    else if (k === 'c') togglePanel('craft');
    else if (k >= '1' && k <= '9') selectSlot(+k - 1, true);
  });
  addEventListener('keyup', ev => ui.keys.delete(ev.key.toLowerCase()));
  addEventListener('blur', () => ui.keys.clear());

  cv.addEventListener('mousemove', ev => {
    ui.mouseIn = true;
    ui.hover = screenToTile(ev.clientX, ev.clientY);
    if (ui.drag) {
      const s = curDim().size;
      ui.drag.x1 = Math.max(0, Math.min(s - 1, ui.hover.x));
      ui.drag.y1 = Math.max(0, Math.min(s - 1, ui.hover.y));
    }
  });
  cv.addEventListener('mouseleave', () => { ui.mouseIn = false; });
  cv.addEventListener('contextmenu', ev => ev.preventDefault());
  cv.addEventListener('mousedown', ev => {
    if (ui.paused) return;
    const t = screenToTile(ev.clientX, ev.clientY), inb = world.inBounds(curDim(), t.x, t.y);
    if (ev.button === 2) { if (ui.slot >= 0) deselect(); else if (ui.destroy) toggleDestroy(); return; }
    if (ev.button !== 0) return;
    if (ui.destroy) { if (inb) ui.drag = { x0: t.x, y0: t.y, x1: t.x, y1: t.y }; return; }
    const type = ui.slot >= 0 ? ui.toolbar[ui.slot] : null;
    if (type) {
      if (!inb) return;
      const r = world.place(type, ui.dim, t.x, t.y, ui.dir);
      if (!r.ok) toast(r.reason);
      return;
    }
    const e = inb ? world.at(ui.dim, t.x, t.y) : null;
    if (e) openPopup(e); else closePopup();
  });
  addEventListener('mouseup', ev => {
    if (ev.button !== 0 || !ui.drag) return;
    const d = ui.drag; ui.drag = null;
    showConfirm(world.planDestroy(ui.dim, d.x0, d.y0, d.x1, d.y1));
  });
  cv.addEventListener('dblclick', ev => {
    if (ui.slot >= 0 && ui.toolbar[ui.slot] || ui.destroy) return;
    const t = screenToTile(ev.clientX, ev.clientY);
    const e = world.inBounds(curDim(), t.x, t.y) ? world.at(ui.dim, t.x, t.y) : null;
    if (e && e.kind === 'controller') enter(e);
  });

  // ---------- HUD & loop ----------
  function updateHud() {
    for (const n of document.querySelectorAll('#bar .n')) n.textContent = placeCount(n.dataset.count);
    const m = $('mode');
    const type = ui.slot >= 0 ? ui.toolbar[ui.slot] : null;
    m.className = ui.destroy ? 'destroy' : '';
    m.textContent = ui.destroy ? 'Destroy mode · click or drag to select'
      : ui.slot >= 0 && !type ? 'Empty slot · click it again to choose a block'
      : type ? `Placing ${C.buildings[type].name}${ROTATES.includes(C.buildings[type].kind) ? ' · facing ' + DIR_NAMES[ui.dir] : ''}${C.buildings[type].placement === 'wall' ? ' · must touch a wall' : ''}` : '';
    updatePanel();
  }

  let last = performance.now(), acc = 0;
  function frame(now) {
    const dt = Math.min(0.1, (now - last) / 1000); last = now;
    const v = 500 * dt / ui.z;
    if (ui.keys.has('w')) ui.cam.y -= v;
    if (ui.keys.has('s')) ui.cam.y += v;
    if (ui.keys.has('a')) ui.cam.x -= v;
    if (ui.keys.has('d')) ui.cam.x += v;
    if (!ui.paused) {
      acc += dt * ui.speed;
      while (acc >= world.dt) { world.step(); acc -= world.dt; }
    }
    render(); updatePopup(); updateHud();
    requestAnimationFrame(frame);
  }

  buildBar(); updateCrumb();
  requestAnimationFrame(frame);
  window.__proto = { get world() { return world; }, ui };   // debug handle for testing
})();
