using Compacted.Data;
using Compacted.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Compacted.View
{
    /// <summary>Small abstract icon of a block for the toolbar and block browser (same language as the map).</summary>
    public sealed class BlockIcon : VisualElement
    {
        BuildingDefinition def;

        public BlockIcon(BuildingDefinition def, float size)
        {
            this.def = def;
            style.width = size;
            style.height = size;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetDefinition(BuildingDefinition d) { def = d; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext ctx)
        {
            if (def == null) return;
            var p = ctx.painter2D;
            float s = contentRect.width;
            var col = def.visual.color;
            void Box(float inset, Color c, float radius)
            {
                p.fillColor = c;
                p.BeginPath();
                RoundRect(p, new Rect(inset, inset, s - 2 * inset, s - 2 * inset), radius);
                p.Fill();
            }
            switch (def.kind)
            {
                case BuildingKind.Belt:
                    Box(s * 0.04f, Palette.BeltBase, 3);
                    p.strokeColor = Palette.BeltMark; p.lineWidth = s * 0.07f;
                    foreach (var off in new[] { -0.14f, 0.14f })
                    {
                        p.BeginPath();
                        p.MoveTo(new Vector2(s * (0.5f + off - 0.09f), s * 0.32f));
                        p.LineTo(new Vector2(s * (0.5f + off + 0.09f), s * 0.5f));
                        p.LineTo(new Vector2(s * (0.5f + off - 0.09f), s * 0.68f));
                        p.Stroke();
                    }
                    break;
                case BuildingKind.Pipe:
                    p.strokeColor = Palette.PipeOuter; p.lineWidth = s * 0.3f;
                    p.BeginPath(); p.MoveTo(new Vector2(0, s / 2)); p.LineTo(new Vector2(s, s / 2)); p.Stroke();
                    p.strokeColor = Palette.PipeInner; p.lineWidth = s * 0.16f;
                    p.BeginPath(); p.MoveTo(new Vector2(0, s / 2)); p.LineTo(new Vector2(s, s / 2)); p.Stroke();
                    break;
                case BuildingKind.Controller:
                    Box(s * 0.06f, Palette.ControllerEdge, 5);
                    Box(s * 0.11f, Palette.Controller, 4);
                    p.fillColor = Palette.ControllerEdge;
                    for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                    {
                        p.BeginPath();
                        var c = new Vector2(s * (0.32f + x * 0.18f), s * (0.32f + y * 0.18f));
                        p.Arc(c, s * 0.035f, 0, 360);
                        p.Fill();
                    }
                    break;
                case BuildingKind.Hatch:
                    Box(s * 0.07f, def.hatchIo == HatchIo.In ? Palette.In : Palette.Out, 4);
                    Box(s * 0.12f, col, 3);
                    break;
                case BuildingKind.Core:
                    Box(s * 0.06f, Palette.Formed, 5);
                    Box(s * 0.11f, col, 4);
                    p.fillColor = Palette.Formed;
                    p.BeginPath();
                    p.MoveTo(new Vector2(s / 2, s * 0.32f)); p.LineTo(new Vector2(s * 0.68f, s / 2));
                    p.LineTo(new Vector2(s / 2, s * 0.68f)); p.LineTo(new Vector2(s * 0.32f, s / 2)); p.ClosePath();
                    p.Fill();
                    break;
                case BuildingKind.Part:
                    Box(s * 0.08f, col, 3);
                    p.fillColor = Color.Lerp(col, Color.white, 0.4f);
                    for (int i = -1; i <= 1; i++) { p.BeginPath(); RoundRect(p, new Rect(s * 0.18f, s * (0.47f + i * 0.2f), s * 0.64f, s * 0.06f), 0); p.Fill(); }
                    break;
                default:
                    Box(s * 0.08f, col, 6);
                    p.fillColor = Color.Lerp(col, Color.white, 0.35f);
                    p.BeginPath(); p.Arc(new Vector2(s / 2, s / 2), s * 0.12f, 0, 360); p.Fill();
                    p.fillColor = Palette.Out;
                    p.BeginPath();
                    p.MoveTo(new Vector2(s * 0.94f, s / 2)); p.LineTo(new Vector2(s * 0.82f, s * 0.42f)); p.LineTo(new Vector2(s * 0.82f, s * 0.58f)); p.ClosePath();
                    p.Fill();
                    break;
            }
            if (def.visual.sprite != null) { }   // sprite icons come with per-block art
        }

        static void RoundRect(Painter2D p, Rect r, float radius)
        {
            radius = Mathf.Min(radius, r.width / 2, r.height / 2);
            p.MoveTo(new Vector2(r.xMin + radius, r.yMin));
            p.LineTo(new Vector2(r.xMax - radius, r.yMin));
            if (radius > 0) p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMin + radius), radius);
            p.LineTo(new Vector2(r.xMax, r.yMax - radius));
            if (radius > 0) p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - radius, r.yMax), radius);
            p.LineTo(new Vector2(r.xMin + radius, r.yMax));
            if (radius > 0) p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMax - radius), radius);
            p.LineTo(new Vector2(r.xMin, r.yMin + radius));
            if (radius > 0) p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMin + radius, r.yMin), radius);
            p.ClosePath();
        }
    }
}
