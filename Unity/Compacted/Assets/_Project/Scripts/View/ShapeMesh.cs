using System.Collections.Generic;
using UnityEngine;

namespace Compacted.View
{
    /// <summary>
    /// Builds one vertex-colored mesh of simple flat shapes per frame. Shapes draw in the order they're added.
    /// </summary>
    public sealed class ShapeMesh
    {
        readonly List<Vector3> verts = new List<Vector3>(4096);
        readonly List<Color> colors = new List<Color>(4096);
        readonly List<int> tris = new List<int>(8192);
        public readonly Mesh Mesh;
        /// <summary>Multiplies every color's alpha (used by the enter/leave transition).</summary>
        public float Alpha = 1;

        public ShapeMesh(string name)
        {
            Mesh = new Mesh { name = name };
            Mesh.MarkDynamic();
        }

        public void Clear() { verts.Clear(); colors.Clear(); tris.Clear(); }

        public void Apply()
        {
            Mesh.Clear();
            Mesh.SetVertices(verts);
            Mesh.SetColors(colors);
            Mesh.SetTriangles(tris, 0);
            Mesh.RecalculateBounds();
        }

        static readonly bool Linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        int V(Vector2 p, Color c)
        {
            verts.Add(p);
            // Palette colors are sRGB; vertex colors aren't converted by the shader in a linear project.
            if (Linear) { float a = c.a; c = c.linear; c.a = a; }
            c.a *= Alpha;
            colors.Add(c);
            return verts.Count - 1;
        }

        public void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int i = V(a, col); V(b, col); V(c, col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int i = V(a, col); V(b, col); V(c, col); V(d, col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>Axis-aligned rect from its min corner and size.</summary>
        public void Rect(Rect r, Color col) =>
            Quad(new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), col);

        public void RectOutline(Rect r, float w, Color col)
        {
            Rect(new Rect(r.xMin, r.yMin, r.width, w), col);
            Rect(new Rect(r.xMin, r.yMax - w, r.width, w), col);
            Rect(new Rect(r.xMin, r.yMin + w, w, r.height - 2 * w), col);
            Rect(new Rect(r.xMax - w, r.yMin + w, w, r.height - 2 * w), col);
        }

        public void Circle(Vector2 c, float r, Color col, int seg = 16)
        {
            int center = V(c, col);
            for (int k = 0; k <= seg; k++)
            {
                float a = k * Mathf.PI * 2 / seg;
                V(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col);
                if (k > 0) { tris.Add(center); tris.Add(center + k + 1); tris.Add(center + k); }
            }
        }

        public void RoundRect(Rect r, float radius, Color col, int seg = 4)
        {
            radius = Mathf.Min(radius, r.width / 2, r.height / 2);
            Rect(new Rect(r.xMin + radius, r.yMin, r.width - 2 * radius, r.height), col);
            Rect(new Rect(r.xMin, r.yMin + radius, radius, r.height - 2 * radius), col);
            Rect(new Rect(r.xMax - radius, r.yMin + radius, radius, r.height - 2 * radius), col);
            Corner(new Vector2(r.xMax - radius, r.yMax - radius), radius, 0, col, seg);
            Corner(new Vector2(r.xMin + radius, r.yMax - radius), radius, 90, col, seg);
            Corner(new Vector2(r.xMin + radius, r.yMin + radius), radius, 180, col, seg);
            Corner(new Vector2(r.xMax - radius, r.yMin + radius), radius, 270, col, seg);
        }

        void Corner(Vector2 c, float r, float startDeg, Color col, int seg)
        {
            int center = V(c, col);
            for (int k = 0; k <= seg; k++)
            {
                float a = (startDeg + 90f * k / seg) * Mathf.Deg2Rad;
                V(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, col);
                if (k > 0) { tris.Add(center); tris.Add(center + k + 1); tris.Add(center + k); }
            }
        }

        /// <summary>Thick line segment.</summary>
        public void Line(Vector2 a, Vector2 b, float w, Color col)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * (w / 2);
            Quad(a - n, a + n, b + n, b - n, col);
        }

        /// <summary>Triangle pointing along `dir` (unit vector), centred at c.</summary>
        public void Arrow(Vector2 c, Vector2 dir, float s, Color col)
        {
            var n = new Vector2(-dir.y, dir.x);
            Tri(c + dir * s, c - dir * s * 0.6f + n * s * 0.9f, c - dir * s * 0.6f - n * s * 0.9f, col);
        }

        /// <summary>Teardrop pointing up: the Fluid I/O port icon.</summary>
        public void Droplet(Vector2 c, float r, Color col)
        {
            Circle(c - new Vector2(0, r * 0.25f), r, col, 14);
            Tri(c + new Vector2(0, r * 1.35f), c + new Vector2(r * 0.86f, r * 0.15f), c + new Vector2(-r * 0.86f, r * 0.15f), col);
        }
    }
}
