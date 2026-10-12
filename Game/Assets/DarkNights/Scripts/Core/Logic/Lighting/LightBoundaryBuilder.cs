using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Logic.Lighting
{
    /// <summary>从独占冻结格提取真实整格／坡面／天花板外露边；仅保留空气侧已知的边，跨空间页合并，纯算法可取消。</summary>
    public static class LightBoundaryBuilder
    {
        public static LightBoundarySnapshot Build(LightGeometrySnapshot source, Func<bool> cancelled)
        {
            var edges = new List<LightBoundarySnapshot.Segment>();
            var polygon = new List<(float X, float Y)>(6);
            for (int y = 0; y < source.Height; y++)
            {
                if (cancelled?.Invoke() == true) throw new OperationCanceledException();
                for (int x = 0; x < source.Width; x++)
                {
                    byte cell = source.Cells[y * source.Width + x];
                    if ((cell & 192) != 192) continue;
                    var shape = (TerrainCellShape)(cell & 15);
                    if ((int)shape > 12) throw new ArgumentException("未知照明坡形。");
                    Polygon(shape, polygon);
                    for (int i = 0; i < polygon.Count; i++)
                    {
                        var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                        // Polygon 为顺时针（屏幕坐标），外法线朝左。
                        float dx = b.X - a.X, dy = b.Y - a.Y, length = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (length < .0001f) continue;
                        float nx = dy / length, ny = -dx / length;
                        bool axis = Math.Abs(dx) < .0001f || Math.Abs(dy) < .0001f;
                        int parts = axis ? 2 : 1;
                        for (int part = 0; part < parts; part++)
                        {
                            float t0 = (float)part / parts, t1 = (float)(part + 1) / parts, mid = (t0 + t1) * .5f;
                            if (!Air(source, x + a.X + dx * mid + nx * .001f, y + a.Y + dy * mid + ny * .001f)) continue;
                            edges.Add(new LightBoundarySnapshot.Segment(x + a.X + dx * t0, y + a.Y + dy * t0,
                                x + a.X + dx * t1, y + a.Y + dy * t1, nx, ny, 0));
                        }
                    }
                }
            }
            edges.Sort(Compare);
            var merged = new List<LightBoundarySnapshot.Segment>();
            foreach (var edge in edges)
            {
                if (merged.Count > 0)
                {
                    var last = merged[merged.Count - 1];
                    if (Line(last) == Line(edge) && Normal(last) == Normal(edge) &&
                        Math.Abs(last.Bx - edge.Ax) < .0001f && Math.Abs(last.By - edge.Ay) < .0001f)
                    {
                        merged[merged.Count - 1] = Create(last.Ax, last.Ay, edge.Bx, edge.By, last.Nx, last.Ny);
                        continue;
                    }
                }
                merged.Add(Create(edge.Ax, edge.Ay, edge.Bx, edge.By, edge.Nx, edge.Ny));
            }
            int columns = (source.Width + 7) / 8, rows = (source.Height + 7) / 8;
            var buckets = new List<int>[columns * rows];
            for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<int>();
            for (int i = 0; i < merged.Count; i++)
            {
                var e = merged[i];
                int left = Math.Max(0, (int)Math.Floor(Math.Min(e.Ax, e.Bx) / 8));
                int top = Math.Max(0, (int)Math.Floor(Math.Min(e.Ay, e.By) / 8));
                int right = Math.Min(columns - 1, (int)Math.Floor(Math.Max(e.Ax, e.Bx) / 8));
                int bottom = Math.Min(rows - 1, (int)Math.Floor(Math.Max(e.Ay, e.By) / 8));
                for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++) buckets[y * columns + x].Add(i);
            }
            var pages = new int[buckets.Length][];
            for (int i = 0; i < pages.Length; i++) pages[i] = buckets[i].ToArray();
            return new LightBoundarySnapshot(source.Width, source.Height, merged.ToArray(), pages);
        }

        private static bool Air(LightGeometrySnapshot source, float x, float y)
        {
            int u = (int)Math.Floor(x), row = (int)Math.Floor(y);
            if (u < 0 || row < 0 || u >= source.Width || row >= source.Height) return false;
            byte cell = source.Cells[row * source.Width + u];
            return (cell & 128) != 0 && ((cell & 64) == 0 || !TerrainShapeGeometry.Contains(
                (TerrainCellShape)(cell & 15), x - u, 1 - (y - row)));
        }

        private static void Polygon(TerrainCellShape shape, List<(float X, float Y)> output)
        {
            output.Clear();
            var corners = new[] { (X: 0f, Y: 0f), (X: 1f, Y: 0f), (X: 1f, Y: 1f), (X: 0f, Y: 1f) };
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4];
                float sa = Signed(a.X, a.Y), sb = Signed(b.X, b.Y);
                if (sa >= 0) output.Add(a);
                if ((sa < 0) != (sb < 0))
                {
                    float t = sa / (sa - sb); output.Add((a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t));
                }
            }
            float Signed(float x, float row) => shape == TerrainCellShape.Full ? 1 :
                (TerrainShapeGeometry.Ceiling(shape) ? 1 : -1) * (1 - row - TerrainShapeGeometry.Edge(shape, x));
        }

        private static int Normal(LightBoundarySnapshot.Segment e) => (int)Math.Round(e.Nx * 1000) * 3000 + (int)Math.Round(e.Ny * 1000);
        private static int Line(LightBoundarySnapshot.Segment e) => (int)Math.Round((e.Nx * e.Ax + e.Ny * e.Ay) * 1000);
        private static float Along(LightBoundarySnapshot.Segment e) => -e.Ny * e.Ax + e.Nx * e.Ay;
        private static int Compare(LightBoundarySnapshot.Segment a, LightBoundarySnapshot.Segment b)
        {
            int n = Normal(a).CompareTo(Normal(b)); if (n != 0) return n;
            n = Line(a).CompareTo(Line(b)); return n != 0 ? n : Along(a).CompareTo(Along(b));
        }
        private static LightBoundarySnapshot.Segment Create(float ax, float ay, float bx, float by, float nx, float ny)
        {
            long id = 1469598103934665603;
            foreach (float value in new[] { ax, ay, bx, by, nx, ny }) id = unchecked((id ^ (int)Math.Round(value * 1000)) * 1099511628211);
            return new LightBoundarySnapshot.Segment(ax, ay, bx, by, nx, ny, id);
        }
    }
}
