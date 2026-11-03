using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.MapModule;

namespace DarkNights.Core.Logic.MapModule
{
    /// <summary>
    /// 模块多边形的确定性三角分解及凸裁剪；仅消费冻结坐标，不使用Unity、文件系统或业务随机数。
    /// 自交、零长边和无法分解的轮廓拒绝；裁剪结果用于精确面积及既有坡形匹配。
    /// </summary>
    public static class MapModuleGeometry
    {
        public const double Epsilon = 0.0000001;
        public static double Cross(MapModuleVertex a, MapModuleVertex b, MapModuleVertex c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        public static double SignedArea(IReadOnlyList<MapModuleVertex> points)
        {
            double result = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count];
                result += a.X * b.Y - b.X * a.Y;
            }
            return result * .5;
        }

        public static IReadOnlyList<MapModuleVertex[]> Triangulate(IReadOnlyList<MapModuleVertex> source)
        {
            var vertices = source.ToList();
            for (int i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i]; var b = vertices[(i + 1) % vertices.Count];
                if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < Epsilon)
                    throw new InvalidOperationException("轮廓存在零长边。");
                for (int j = i + 2; j < vertices.Count; j++)
                {
                    if (i == 0 && j == vertices.Count - 1) continue;
                    if (Intersects(a, b, vertices[j], vertices[(j + 1) % vertices.Count]))
                        throw new InvalidOperationException("轮廓自交或非相邻边接触。");
                }
            }
            if (Math.Abs(SignedArea(vertices)) < Epsilon) throw new InvalidOperationException("实体面积为零。");
            if (SignedArea(vertices) < 0) vertices.Reverse();
            var result = new List<MapModuleVertex[]>();
            while (vertices.Count > 3)
            {
                bool removed = false;
                for (int i = 0; i < vertices.Count; i++)
                {
                    var a = vertices[(i + vertices.Count - 1) % vertices.Count];
                    var b = vertices[i]; var c = vertices[(i + 1) % vertices.Count];
                    double turn = Cross(a, b, c);
                    if (Math.Abs(turn) < Epsilon) { vertices.RemoveAt(i); removed = true; break; }
                    if (turn < 0) continue;
                    bool occupied = vertices.Where((_, j) => j != i &&
                        j != (i + vertices.Count - 1) % vertices.Count && j != (i + 1) % vertices.Count)
                        .Any(p => Cross(a, b, p) >= -Epsilon && Cross(b, c, p) >= -Epsilon && Cross(c, a, p) >= -Epsilon);
                    if (occupied) continue;
                    result.Add(new[] { a, b, c }); vertices.RemoveAt(i); removed = true; break;
                }
                if (!removed) throw new InvalidOperationException("轮廓无法分解，请检查重复点或接触边。");
            }
            if (vertices.Count == 3 && Math.Abs(SignedArea(vertices)) > Epsilon) result.Add(vertices.ToArray());
            return result.AsReadOnly();
        }

        public static List<MapModuleVertex> Clip(IReadOnlyList<MapModuleVertex> source, IReadOnlyList<MapModuleVertex> convex)
        {
            var output = source.ToList();
            double sign = SignedArea(convex) >= 0 ? 1 : -1;
            for (int edge = 0; edge < convex.Count && output.Count > 0; edge++)
            {
                var a = convex[edge]; var b = convex[(edge + 1) % convex.Count];
                var input = output; output = new List<MapModuleVertex>();
                var previous = input[input.Count - 1];
                double before = Cross(a, b, previous) * sign;
                foreach (var point in input)
                {
                    double after = Cross(a, b, point) * sign;
                    if ((after >= -Epsilon) != (before >= -Epsilon))
                    {
                        double t = before / (before - after);
                        output.Add(new MapModuleVertex(previous.X + (point.X - previous.X) * t,
                            previous.Y + (point.Y - previous.Y) * t));
                    }
                    if (after >= -Epsilon) output.Add(point);
                    previous = point; before = after;
                }
            }
            return output;
        }

        private static bool Intersects(MapModuleVertex a, MapModuleVertex b, MapModuleVertex c, MapModuleVertex d)
        {
            double abC = Cross(a, b, c), abD = Cross(a, b, d), cdA = Cross(c, d, a), cdB = Cross(c, d, b);
            if (abC * abD < -Epsilon && cdA * cdB < -Epsilon) return true;
            return Math.Abs(abC) < Epsilon && OnSegment(a, b, c) ||
                Math.Abs(abD) < Epsilon && OnSegment(a, b, d) ||
                Math.Abs(cdA) < Epsilon && OnSegment(c, d, a) ||
                Math.Abs(cdB) < Epsilon && OnSegment(c, d, b);
        }
        private static bool OnSegment(MapModuleVertex a, MapModuleVertex b, MapModuleVertex p) =>
            p.X >= Math.Min(a.X, b.X) - Epsilon && p.X <= Math.Max(a.X, b.X) + Epsilon &&
            p.Y >= Math.Min(a.Y, b.Y) - Epsilon && p.Y <= Math.Max(a.Y, b.Y) + Epsilon;
    }
}
