using System.Collections.Generic;
using Compacted.Data;
using Compacted.Sim;
using UnityEngine;

namespace Compacted.View
{
    /// <summary>
    /// Draws the current dimension with flat abstract shapes (no text on the map). One tile = one world unit.
    /// A building's sprite, when its definition has one, replaces its shape, so per-block art can be swapped in later.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public GameController game;
        public Camera cam;
        public Material shapeMaterial;

        const float WallWidth = 0.28f;
        ShapeMesh baseMesh, overMesh;
        Transform root;
        readonly List<SpriteRenderer> spritePool = new List<SpriteRenderer>();
        int spritesUsed;
        readonly Dictionary<string, Visual> buildingVisuals = new Dictionary<string, Visual>();
        readonly Dictionary<string, Color> itemColors = new Dictionary<string, Color>();

        float transitionStart = -10, transitionFrom = 1;
        float currentScale = 1;

        void Awake()
        {
            root = new GameObject("WorldRoot").transform;
            root.SetParent(transform, false);
            baseMesh = new ShapeMesh("WorldBase");
            overMesh = new ShapeMesh("WorldOverlay");
            MakeRenderer("Base", baseMesh, 0);
            MakeRenderer("Overlay", overMesh, 20);
            var db = game.database;
            foreach (var b in db.buildings) if (b != null) buildingVisuals[b.id] = b.visual;
            foreach (var i in db.items) if (i != null) itemColors[i.id] = i.visual.color;
            foreach (var f in db.fluids) if (f != null) itemColors[f.id] = f.color;
            cam.backgroundColor = Palette.Background;
        }

        void MakeRenderer(string name, ShapeMesh mesh, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = shapeMaterial;
            mr.sortingOrder = order;
        }

        public void StartTransition(bool zoomIn)
        {
            transitionStart = Time.unscaledTime;
            transitionFrom = zoomIn ? 0.3f : 2.6f;
        }

        public Color ItemColor(string id) => id != null && itemColors.TryGetValue(id, out var c) ? c : Color.white;
        public Visual VisualOf(string buildingId) => buildingVisuals.TryGetValue(buildingId, out var v) ? v : new Visual(Color.gray);

        // ---------- coordinates ----------
        Dimension Dim => game.World.Dims[game.CurrentDim];

        public static Vector2 DirVec(Dir d)
        {
            switch (d)
            {
                case Dir.N: return Vector2.up;
                case Dir.E: return Vector2.right;
                case Dir.S: return Vector2.down;
                default: return Vector2.left;
            }
        }

        /// <summary>World position (before the transition scale) of a tile's centre.</summary>
        public Vector2 TileCenter(int x, int y)
        {
            float half = Dim.Size / 2f;
            return new Vector2(x + 0.5f - half, half - y - 0.5f);
        }

        public Vector3 TileCenterWorld(int x, int y) => root.TransformPoint(TileCenter(x, y));

        public Cell ScreenToTile(Vector2 screen)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            var local = root.InverseTransformPoint(w);
            float half = Dim.Size / 2f;
            return new Cell(Mathf.FloorToInt(local.x + half), Mathf.FloorToInt(half - local.y));
        }

        // ---------- frame ----------
        void LateUpdate()
        {
            var g = game;
            var dim = Dim;

            // Transition: scale and fade in after entering or leaving a controller.
            float t = Mathf.Clamp01((Time.unscaledTime - transitionStart) / 0.28f);
            float k = 1 - Mathf.Pow(1 - t, 3);
            currentScale = Mathf.Lerp(transitionFrom, 1, k);
            float alpha = Mathf.Lerp(0.25f, 1, k);
            root.localScale = new Vector3(currentScale, currentScale, 1);

            UpdateCamera(dim);

            baseMesh.Clear(); overMesh.Clear();
            baseMesh.Alpha = alpha; overMesh.Alpha = alpha;
            spritesUsed = 0;

            DrawDimension(dim);
            DrawOverlays(dim);

            baseMesh.Apply(); overMesh.Apply();
            for (int i = spritesUsed; i < spritePool.Count; i++) spritePool[i].enabled = false;
        }

        // ---------- camera ----------
        const float PanSmoothTime = 0.12f, ZoomSmoothTime = 0.1f;
        Vector2 camPos, camVel;
        float camSize, camSizeVel;
        bool snapCamera = true;

        /// <summary>Jump straight to the camera target next frame (new dimension, load, new game).</summary>
        public void SnapCamera() => snapCamera = true;

        /// <summary>Orthographic size at zoom 1: the dimension fills the view, with room for the toolbar.</summary>
        float FitSize(Dimension dim)
        {
            float need = dim.Size + 2 * WallWidth + 1.6f;
            return Mathf.Max(need / 2f, need / 2f / cam.aspect) + 0.7f;
        }

        void UpdateCamera(Dimension dim)
        {
            var g = game;
            // Keep the target near the grid so it can't be panned away and lost.
            float limit = dim.Size / 2f + 1f;
            g.CameraOffset = new Vector2(Mathf.Clamp(g.CameraOffset.x, -limit, limit), Mathf.Clamp(g.CameraOffset.y, -limit, limit));
            float targetSize = FitSize(dim) / g.Zoom;
            var targetPos = g.CameraOffset + new Vector2(0, -0.4f);
            if (snapCamera)
            {
                camPos = targetPos; camSize = targetSize; camVel = Vector2.zero; camSizeVel = 0;
                snapCamera = false;
            }
            else
            {
                float dt = Time.unscaledDeltaTime;
                camPos = Vector2.SmoothDamp(camPos, targetPos, ref camVel, PanSmoothTime, Mathf.Infinity, dt);
                camSize = Mathf.SmoothDamp(camSize, targetSize, ref camSizeVel, ZoomSmoothTime, Mathf.Infinity, dt);
            }
            cam.orthographicSize = camSize;
            cam.transform.position = new Vector3(camPos.x, camPos.y, -10);
        }

        void DrawDimension(Dimension dim)
        {
            var w = game.World;
            float half = dim.Size / 2f;
            baseMesh.RoundRect(new Rect(-half - WallWidth, -half - WallWidth, dim.Size + 2 * WallWidth, dim.Size + 2 * WallWidth), 0.12f, dim.IsRoot ? Palette.RootWall : Palette.Wall);
            for (int y = 0; y < dim.Size; y++)
            for (int x = 0; x < dim.Size; x++)
            {
                var c = TileCenter(x, y);
                baseMesh.Rect(new Rect(c.x - 0.48f, c.y - 0.48f, 0.96f, 0.96f), Palette.Tile);
            }

            if (!dim.IsRoot)
            {
                var ctrl = w.Get<ControllerEntity>(dim.ControllerId);
                for (int i = 0; i < 4; i++) DrawWallPort(half, (Dir)i, ctrl.Ports[i], ctrl.PortBuf[i]);
            }

            for (int y = 0; y < dim.Size; y++)
            for (int x = 0; x < dim.Size; x++)
            {
                var e = w.At(dim.Id, x, y);
                if (e != null) DrawBlock(baseMesh, e.Def, e.Dir, TileCenter(x, y), e);
            }

            foreach (var e in w.Entities)
            {
                if (e is CoreEntity core && core.Dim == dim.Id && core.Formed != null)
                {
                    var f = core.Formed;
                    var a = TileCenter(f.X, f.Y + f.H - 1);
                    overMesh.RectOutline(new Rect(a.x - 0.47f, a.y - 0.47f, f.W - 0.06f, f.H - 0.06f), 0.05f, Palette.Formed);
                }
                if (e is BeltEntity b && b.Dim == dim.Id && b.Item != null) DrawBeltItem(b);
            }
        }

        void DrawWallPort(float half, Dir d, PortMode mode, string buf)
        {
            var v = DirVec(d);
            var c = v * (half + WallWidth / 2);
            baseMesh.Circle(c, 0.2f, Palette.Port(mode), 18);
            var bg = Palette.Background;
            switch (mode)
            {
                case PortMode.In: baseMesh.Arrow(c, -v, 0.11f, bg); break;
                case PortMode.Out: baseMesh.Arrow(c, v, 0.11f, bg); break;
                case PortMode.Fluid: baseMesh.Droplet(c, 0.08f, bg); break;
                default: baseMesh.Rect(new Rect(c.x - 0.09f, c.y - 0.022f, 0.18f, 0.044f), bg); break;
            }
            if (buf != null)
            {
                var side = new Vector2(-v.y, v.x) * 0.34f;
                ItemDot(overMesh, c + side, buf, 0.09f);
            }
        }

        void ItemDot(ShapeMesh m, Vector2 c, string item, float r)
        {
            m.Circle(c, r + 0.025f, Palette.ItemOutline, 14);
            m.Circle(c, r, ItemColor(item), 14);
        }

        void DrawBeltItem(BeltEntity b)
        {
            var c = TileCenter(b.X, b.Y);
            var from = c + DirVec(b.From) * 0.5f;
            var to = c + DirVec(b.Dir) * 0.5f;
            float p = (float)b.Prog;
            var pos = p < 0.5f ? Vector2.Lerp(from, c, p * 2) : Vector2.Lerp(c, to, (p - 0.5f) * 2);
            ItemDot(overMesh, pos, b.Item, 0.14f);
        }

        // ---------- blocks ----------
        /// <summary>Draw one block. e is the live entity, or null for previews.</summary>
        public void DrawBlock(ShapeMesh m, BuildingDef def, Dir dir, Vector2 c, Entity e)
        {
            var vis = VisualOf(def.Id);
            if (vis.sprite != null && def.Kind != BuildingKind.Belt && def.Kind != BuildingKind.Pipe)
            {
                PlaceSprite(vis.sprite, c, m == overMesh ? m.Alpha : 1);
                DrawDecorations(def, dir, c, e);
                return;
            }
            switch (def.Kind)
            {
                case BuildingKind.Belt: DrawBelt(m, dir, c); break;
                case BuildingKind.Pipe: DrawPipe(m, c, e as PipeEntity); break;
                case BuildingKind.Machine:
                    m.RoundRect(Inset(c, 0.08f), 0.12f, vis.color);
                    Glyph(m, def.MachineGroup, c, Lighten(vis.color, 0.35f));
                    m.Arrow(c + DirVec(dir) * 0.36f, DirVec(dir), 0.09f, Palette.Out);
                    break;
                case BuildingKind.Core:
                    m.RoundRect(Inset(c, 0.06f), 0.12f, Palette.Formed);
                    m.RoundRect(Inset(c, 0.1f), 0.1f, vis.color);
                    m.Quad(c + new Vector2(0, 0.18f), c + new Vector2(0.18f, 0), c + new Vector2(0, -0.18f), c + new Vector2(-0.18f, 0), Palette.Formed);
                    break;
                case BuildingKind.Part:
                    m.RoundRect(Inset(c, 0.08f), 0.05f, vis.color);
                    for (int i = -1; i <= 1; i++) m.Rect(new Rect(c.x - 0.32f, c.y + i * 0.2f - 0.03f, 0.64f, 0.06f), Lighten(vis.color, 0.4f));
                    break;
                case BuildingKind.Hatch:
                {
                    bool input = def.Io == HatchIo.In;
                    var edge = input ? Palette.In : Palette.Out;
                    m.RoundRect(Inset(c, 0.07f), 0.06f, edge);
                    m.RoundRect(Inset(c, 0.11f), 0.04f, vis.color);
                    var v = DirVec(dir);
                    m.Arrow(c + v * 0.3f, input ? -v : v, 0.12f, edge);
                    m.Rect(new Rect(c.x - 0.12f, c.y - 0.12f, 0.24f, 0.24f), Lighten(vis.color, 0.25f));
                    break;
                }
                case BuildingKind.Controller:
                {
                    m.RoundRect(Inset(c, 0.06f), 0.1f, Palette.ControllerEdge);
                    m.RoundRect(Inset(c, 0.1f), 0.08f, Palette.Controller);
                    int n = Mathf.Clamp(def.ControllerSize, 3, 5);
                    float step = 0.36f / (n - 1);
                    for (int gy = 0; gy < n; gy++)
                    for (int gx = 0; gx < n; gx++)
                        m.Rect(new Rect(c.x - 0.18f + gx * step - 0.025f, c.y - 0.18f + gy * step - 0.025f, 0.05f, 0.05f), Palette.ControllerEdge.A(0.8f));
                    break;
                }
            }
            DrawDecorations(def, dir, c, e);
        }

        /// <summary>Live-state marks drawn on top of a block: status, recipe, buffers, progress, ports.</summary>
        void DrawDecorations(BuildingDef def, Dir dir, Vector2 c, Entity e)
        {
            var m = overMesh;
            switch (e)
            {
                case MachineEntity me:
                {
                    m.Circle(c + new Vector2(0.27f, 0.27f), 0.065f, Palette.Status(me.Status), 12);
                    if (me.Recipe != null)
                    {
                        var r = game.Content.Recipes[me.Recipe];
                        string outId = null;
                        foreach (var kv in r.Out) { outId = kv.Key; break; }
                        if (outId == null) foreach (var kv in r.FluidOut) { outId = kv.Key; break; }
                        if (outId != null) ItemDot(m, c + new Vector2(-0.27f, 0.27f), outId, 0.07f);
                    }
                    double fluid = 0;
                    foreach (var v in me.FluidIn.Values) fluid += v;
                    foreach (var v in me.FluidOut.Values) fluid += v;
                    if (fluid > 0) Bar(m, c + new Vector2(-0.36f, -0.2f), (float)(fluid / game.Content.Tuning.FluidBuffer), Palette.Fluid);
                    int fuelCap = game.World.FuelCap(me);
                    if (fuelCap > 0) Bar(m, c + new Vector2(0.33f, -0.2f), (float)me.Fuel / fuelCap, Palette.Fuel);
                    int outCount = 0;
                    foreach (var v in me.OutBuf.Values) outCount += v;
                    Pips(m, c + new Vector2(0, -0.2f), outCount, game.World.OutputCapOf(me));
                    if (me.Crafting) Progress(m, c, (float)me.Prog);
                    break;
                }
                case CoreEntity ce:
                    m.Circle(c + new Vector2(0.27f, 0.27f), 0.065f, Palette.Status(ce.Status), 12);
                    if (ce.Crafting) Progress(m, c, (float)ce.Prog);
                    break;
                case HatchEntity he:
                {
                    int n = 0;
                    foreach (var v in he.Buf.Values) n += v;
                    Pips(m, c + new Vector2(0, -0.27f), n, 10);
                    break;
                }
                case ControllerEntity ctrl:
                    for (int i = 0; i < 4; i++)
                    {
                        var mode = ctrl.Ports[i];
                        if (mode == PortMode.Off) continue;
                        var v = DirVec((Dir)i);
                        var p = c + v * 0.42f;
                        if (mode == PortMode.Fluid) m.Droplet(p, 0.05f, Palette.Fluid);
                        else m.Arrow(p, mode == PortMode.In ? -v : v, 0.07f, Palette.Port(mode));
                    }
                    break;
            }
        }

        static void Bar(ShapeMesh m, Vector2 bottom, float frac, Color col)
        {
            frac = Mathf.Clamp01(frac);
            m.Rect(new Rect(bottom.x - 0.025f, bottom.y, 0.05f, 0.4f), new Color(0, 0, 0, 0.4f));
            m.Rect(new Rect(bottom.x - 0.025f, bottom.y, 0.05f, 0.4f * frac), col);
        }

        static void Progress(ShapeMesh m, Vector2 c, float p)
        {
            m.Rect(new Rect(c.x - 0.32f, c.y - 0.36f, 0.64f, 0.045f), new Color(0, 0, 0, 0.35f));
            m.Rect(new Rect(c.x - 0.32f, c.y - 0.36f, 0.64f * Mathf.Clamp01(p), 0.045f), Palette.In);
        }

        /// <summary>Up to five pips showing how full an output buffer is (no numbers on the map).</summary>
        static void Pips(ShapeMesh m, Vector2 c, int count, int cap)
        {
            if (count <= 0) return;
            int lit = Mathf.Clamp(Mathf.CeilToInt(5f * count / Mathf.Max(1, cap)), 1, 5);
            for (int i = 0; i < 5; i++)
                m.Rect(new Rect(c.x - 0.2f + i * 0.085f, c.y - 0.022f, 0.06f, 0.045f), i < lit ? new Color(0.9f, 0.92f, 0.95f, 0.9f) : new Color(1, 1, 1, 0.12f));
        }

        void DrawBelt(ShapeMesh m, Dir dir, Vector2 c)
        {
            m.Rect(new Rect(c.x - 0.47f, c.y - 0.47f, 0.94f, 0.94f), Palette.BeltBase);
            var v = DirVec(dir);
            var n = new Vector2(-v.y, v.x);
            foreach (var off in new[] { -0.14f, 0.14f })
            {
                var tip = c + v * (off + 0.09f);
                var back = c + v * (off - 0.09f);
                m.Line(back + n * 0.18f, tip, 0.06f, Palette.BeltMark);
                m.Line(back - n * 0.18f, tip, 0.06f, Palette.BeltMark);
            }
        }

        void DrawPipe(ShapeMesh m, Vector2 c, PipeEntity p)
        {
            var arms = new List<Vector2>();
            for (int i = 0; i < 4; i++)
            {
                var d = (Dir)i;
                if (p == null) { if (d == Dir.E || d == Dir.W) arms.Add(DirVec(d)); continue; }
                var n = game.World.FluidNeighbor(p, d);
                if (n is PipeEntity || (n is MachineEntity me && ExchangesFluid(me, d.Opposite()))) arms.Add(DirVec(d));
            }
            foreach (var a in arms) m.Line(c, c + a * 0.5f, 0.3f, Palette.PipeOuter);
            m.Circle(c, 0.22f, Palette.PipeOuter, 16);
            foreach (var a in arms) m.Line(c, c + a * 0.5f, 0.16f, Palette.PipeInner);
            m.Circle(c, 0.12f, Palette.PipeInner, 14);
            var seg = p != null ? game.World.SegmentOf(p) : null;
            if (seg != null && seg.Fluid != null && seg.Amount > 0)
            {
                var col = ItemColor(seg.Fluid).A(0.25f + 0.75f * (float)seg.Fill);
                foreach (var a in arms) m.Line(c, c + a * 0.5f, 0.16f, col);
                m.Circle(c, 0.12f, col, 14);
            }
        }

        bool ExchangesFluid(MachineEntity m, Dir side)
        {
            if (m.Recipe == null) return false;
            var r = game.Content.Recipes[m.Recipe];
            return (r.FluidOut.Count > 0 && m.Dir == side) || (r.FluidIn.Count > 0 && m.Dir != side);
        }

        /// <summary>A small emblem per machine group, so machines read apart without text.</summary>
        static void Glyph(ShapeMesh m, string group, Vector2 c, Color col)
        {
            switch (group)
            {
                case "chipper":   // wedge
                    m.Tri(c + new Vector2(-0.16f, -0.12f), c + new Vector2(0.16f, -0.12f), c + new Vector2(-0.16f, 0.16f), col); break;
                case "miner":     // diamond
                    m.Quad(c + new Vector2(0, 0.16f), c + new Vector2(0.14f, 0), c + new Vector2(0, -0.16f), c + new Vector2(-0.14f, 0), col); break;
                case "condenser": m.Droplet(c, 0.1f, col); break;
                case "compressor":   // two plates pressing
                    m.Rect(new Rect(c.x - 0.15f, c.y + 0.06f, 0.3f, 0.06f), col);
                    m.Rect(new Rect(c.x - 0.15f, c.y - 0.12f, 0.3f, 0.06f), col); break;
                case "mixer":     // three dots
                    m.Circle(c + new Vector2(0, 0.09f), 0.065f, col, 12);
                    m.Circle(c + new Vector2(-0.09f, -0.07f), 0.065f, col, 12);
                    m.Circle(c + new Vector2(0.09f, -0.07f), 0.065f, col, 12); break;
                case "furnace":   // flame
                    m.Tri(c + new Vector2(-0.12f, -0.12f), c + new Vector2(0.12f, -0.12f), c + new Vector2(0, 0.18f), col); break;
                case "assembler": // plus
                    m.Rect(new Rect(c.x - 0.15f, c.y - 0.04f, 0.3f, 0.08f), col);
                    m.Rect(new Rect(c.x - 0.04f, c.y - 0.15f, 0.08f, 0.3f), col); break;
                default: m.Circle(c, 0.1f, col, 12); break;
            }
        }

        static Rect Inset(Vector2 c, float inset) => new Rect(c.x - 0.5f + inset, c.y - 0.5f + inset, 1 - 2 * inset, 1 - 2 * inset);
        static Color Lighten(Color c, float t) => Color.Lerp(c, Color.white, t);

        void PlaceSprite(Sprite sprite, Vector2 c, float alpha)
        {
            if (spritesUsed >= spritePool.Count)
            {
                var go = new GameObject("BlockSprite");
                go.transform.SetParent(root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 10;
                spritePool.Add(sr);
            }
            var r = spritePool[spritesUsed++];
            r.enabled = true;
            r.sprite = sprite;
            r.color = new Color(1, 1, 1, alpha * baseMesh.Alpha);
            r.transform.localPosition = c;
            var size = sprite.bounds.size;
            float s = 0.9f / Mathf.Max(0.01f, Mathf.Max(size.x, size.y));
            r.transform.localScale = new Vector3(s, s, 1);
        }

        // ---------- overlays ----------
        void DrawOverlays(Dimension dim)
        {
            var g = game;
            var w = g.World;
            var m = overMesh;

            if (g.SelectedEntity != 0 && w.Get(g.SelectedEntity) is Entity sel && sel.Dim == dim.Id)
            {
                var c = TileCenter(sel.X, sel.Y);
                m.RectOutline(new Rect(c.x - 0.5f, c.y - 0.5f, 1, 1), 0.04f, Palette.Accent);
            }

            if (g.DestroyMode)
            {
                if (g.DragRect is RectInt r)
                {
                    var a = TileCenter(r.xMin, r.yMax - 1);
                    var rect = new Rect(a.x - 0.5f, a.y - 0.5f, r.width, r.height);
                    m.Rect(rect, Palette.Danger.A(0.22f));
                    m.RectOutline(rect, 0.04f, Palette.Danger);
                }
                else if (g.Hover is Cell h && dim.InBounds(h.X, h.Y))
                {
                    var c = TileCenter(h.X, h.Y);
                    m.RectOutline(new Rect(c.x - 0.47f, c.y - 0.47f, 0.94f, 0.94f), 0.04f, Palette.Danger);
                }
                return;
            }

            var def = g.SelectedDef;
            if (def != null && def.Placement == Placement.Wall)
            {
                for (int y = 0; y < dim.Size; y++)
                for (int x = 0; x < dim.Size; x++)
                {
                    if (!dim.TouchesWall(x, y) || w.At(dim.Id, x, y) != null) continue;
                    var c = TileCenter(x, y);
                    m.Rect(new Rect(c.x - 0.48f, c.y - 0.48f, 0.96f, 0.96f), Palette.Accent.A(0.07f));
                }
            }

            if (!(g.Hover is Cell hov) || !dim.InBounds(hov.X, hov.Y)) return;
            var hc = TileCenter(hov.X, hov.Y);
            if (def != null)
            {
                bool ok = w.CanPlace(def.Id, dim.Id, hov.X, hov.Y).Ok;
                float prev = m.Alpha;
                m.Alpha = prev * 0.55f;
                DrawBlock(m, def, def.Rotates ? g.PreviewDir : Dir.N, hc, null);
                m.Alpha = prev;
                m.RectOutline(new Rect(hc.x - 0.47f, hc.y - 0.47f, 0.94f, 0.94f), 0.035f, ok ? Palette.Accent : Palette.Danger);
            }
            else if (w.At(dim.Id, hov.X, hov.Y) != null)
            {
                m.RectOutline(new Rect(hc.x - 0.47f, hc.y - 0.47f, 0.94f, 0.94f), 0.025f, new Color(1, 1, 1, 0.35f));
            }
        }
    }
}
