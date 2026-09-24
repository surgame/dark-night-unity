using System;
using System.Collections.Generic;
using static DarkNights.Core.Logic.Terrain.BackgroundPixelMath;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>有界依赖的世界坐标圆簇算法；资格和胜负均为局部确定函数，不依赖全图候选排序或异步遍历次序。</summary>
    public static class LocalRoundedClusterV2
    {
        private const int VerticalConflict = 8;
        private const int ConflictBucketSize = 128;
        private const double SmoothWidth = .65;

        /// <summary>按样式参数返回可证明覆盖形状、冲突、资格采样的最大依赖半径。</summary>
        public static int DependencyRadius(int depth, int size, int petal, int density)
        {
            double den = density / 100.0, radius = size * .5 * 1.325;
            double shapeX = radius + petal + 3, shapeY = radius + depth + petal + 3;
            double conflictX = Math.Max(size * (1.45 + (1 - den) * 1.2), radius * 1.15);
            int maxFree = (int)Math.Ceiling(depth / .42) + 1;
            return (int)Math.Ceiling(Math.Max(shapeX + conflictX + 6, shapeY + VerticalConflict + maxFree + 4));
        }

        /// <summary>在目标像素矩形内重算局部输出；source 必须含目标外 DependencyRadius 像素的完整基础轮廓 Halo。</summary>
        public static CaveMaskRegion Apply(CaveMaskRegion source, int left, int top, int width, int height,
            string worldSeed, int depth, int size, int petal, int density, int variation, bool grain, string modifierSeed,
            int candidateTop, Action checkpoint = null)
        {
            if (source == null || left < 0 || top < 0 || width < 1 || height < 1 || left + width > source.WorldWidth ||
                top + height > source.WorldHeight || depth < 0 || depth > 12 || size < 8 || size > 30 || petal < 2 || petal > 6 ||
                density < 0 || density > 100 || variation < 0 || variation > 100) throw new ArgumentException("LocalV2 参数或目标区域无效。");

            int dependency = DependencyRadius(depth, size, petal, density);
            if (source.Left > Math.Max(0, left - dependency) || source.Top > Math.Max(0, top - dependency) ||
                source.Left + source.Width < Math.Min(source.WorldWidth, left + width + dependency) ||
                source.Top + source.Height < Math.Min(source.WorldHeight, top + height + dependency))
                throw new InvalidOperationException("LocalV2 输入区域没有覆盖声明的依赖 Halo。");

            var basePixels = source.CopyPixels();
            var signed = CaveChamferField.Signed(basePixels, source.Width, source.Height, checkpoint);
            var candidates = FindCandidates(source, left, top, width, height, worldSeed, depth, size, petal,
                density, variation, modifierSeed, candidateTop, checkpoint);
            var winners = SelectLocalWinners(candidates, density);
            uint seed = Seed((string.IsNullOrEmpty(modifierSeed) ? worldSeed : modifierSeed) + "|rounded-local-v2");
            var accepted = new List<Cluster>(winners.Count);
            foreach (var winner in winners) accepted.Add(new Cluster(winner, CreateLobes(winner, petal, variation, seed)));
            var result = new byte[checked(width * height)];
            var weights = grain ? new float[result.Length] : null;
            var tones = grain ? new byte[result.Length] : null;
            for (int y = 0; y < height; y++)
            {
                checkpoint?.Invoke();
                int worldY = top + y;
                for (int x = 0; x < width; x++)
                {
                    int worldX = left + x, sourceIndex = (worldY - source.Top) * source.Width + worldX - source.Left;
                    double distance = signed[sourceIndex];
                    foreach (var cluster in accepted)
                    {
                        var candidate = cluster.Anchor;
                        if (Math.Abs(worldX - candidate.X) > candidate.Radius + petal + 3 ||
                            worldY < candidate.Y - candidate.Extent - candidate.Radius - 3 ||
                            worldY > candidate.Y + candidate.Extent + candidate.Radius + 3) continue;
                        double closest = double.PositiveInfinity; int owner = -1;
                        for (int i = 0; i < cluster.Lobes.Length; i++)
                        {
                            var lobe = cluster.Lobes[i]; double nx = (worldX - lobe.X) / lobe.Rx, ny = (worldY - lobe.Y) / lobe.Ry;
                            double shape = (Math.Sqrt(nx * nx + ny * ny) - 1) * Math.Min(lobe.Rx, lobe.Ry);
                            if (shape < closest) { closest = shape; owner = i; }
                            double blend = Clamp(.5 + .5 * (shape - distance) / SmoothWidth);
                            distance = shape * (1 - blend) + distance * blend - SmoothWidth * blend * (1 - blend);
                        }
                        if (grain && closest < 2 && owner >= 0)
                        {
                            var lobe = cluster.Lobes[owner]; double nx = (worldX - lobe.X) / lobe.Rx, ny = (worldY - lobe.Y) / lobe.Ry;
                            double influence = Clamp((worldY - (candidate.Y - 8)) / 5.0) *
                                Clamp((candidate.Radius + 2 - Math.Abs(worldX - candidate.X)) / 3.0);
                            int outputIndex = y * width + x;
                            if (influence > weights[outputIndex])
                            {
                                weights[outputIndex] = (float)influence;
                                int face = 2 + (int)Math.Floor(Hash(R(lobe.X), R(lobe.Y), seed + 313) * 4);
                                tones[outputIndex] = (byte)(face + (nx < -.25 ? 1 : 0) - (nx > .45 || ny > .60 ? 1 : 0) + 1);
                            }
                        }
                    }
                    result[y * width + x] = distance <= 0 ? (byte)1 : (byte)0;
                }
            }
            if (grain) return new CaveMaskRegion(result, source.WorldWidth, source.WorldHeight, left, top, width, height,
                weights, tones, Math.Max(2.5, Math.Min(4, petal * 1.1)));
            return new CaveMaskRegion(result, source.WorldWidth, source.WorldHeight, left, top, width, height);
        }

        private static List<Candidate> FindCandidates(CaveMaskRegion source, int left, int top, int width, int height,
            string worldSeed, int depth, int size, int petal, int density, int variation, string modifierSeed, int candidateTop, Action checkpoint)
        {
            var result = new List<Candidate>(); if (depth == 0 || density == 0) return result;
            double den = density / 100.0, variationScale = variation / 100.0;
            uint seed = Seed((string.IsNullOrEmpty(modifierSeed) ? worldSeed : modifierSeed) + "|rounded-local-v2");
            int step = Math.Max(6, (int)Math.Floor(size * .62));
            double maxRadius = size * .5 * 1.325;
            int influenceX = (int)Math.Ceiling(maxRadius + petal + 3 + Math.Max(size * (1.45 + (1 - den) * 1.2), maxRadius * 1.15) + 3);
            int influenceY = (int)Math.Ceiling(maxRadius + depth + petal + 3 + VerticalConflict + 3);
            int minX = Math.Max(3, left - influenceX), maxX = Math.Min(source.WorldWidth - 4, left + width + influenceX);
            int minY = Math.Max(candidateTop + 3, top - influenceY), maxY = Math.Min(source.WorldHeight - 4, top + height + influenceY);
            int firstColumn = (int)Math.Floor((minX - step) / (double)step), lastColumn = (int)Math.Ceiling((maxX + step) / (double)step);
            for (int column = firstColumn; column <= lastColumn; column++)
            {
                checkpoint?.Invoke();
                int x = column * step + R((Hash(column, 0, seed + 5) - .5) * step * .34);
                if (x < minX || x > maxX) continue;
                for (int y = minY; y <= maxY; y++)
                {
                    if (!source.Solid(x, y) || source.Solid(x, y + 1) || !source.Solid(x, y - 2)) continue;
                    int above = 0, below = 0;
                    for (int dx = -2; dx <= 2; dx++) for (int dy = 1; dy <= 3; dy++)
                    { if (source.Solid(x + dx, y - dy)) above++; if (source.Solid(x + dx, y + dy)) below++; }
                    if (above < 10 || below > 6 || Hash(x, y, seed + 17) > den) continue;
                    double radius = size * .5 * (1 + (Hash(x, y, seed + 31) - .5) * .65 * variationScale);
                    int free = 0, maxFree = (int)Math.Ceiling(depth / .42) + 1;
                    while (free < maxFree && y + free + 1 < source.WorldHeight && !source.Solid(x, y + free + 1)) free++;
                    double extent = Math.Min(depth * (1 - .4 * variationScale * Hash(x, y, seed + 41)), Math.Floor(free * .42));
                    if (extent < 1) continue;
                    result.Add(new Candidate(x, y, radius, extent, Hash(x, y, seed + 29)));
                }
            }
            result.Sort(Compare); return result;
        }

        private static List<Candidate> SelectLocalWinners(List<Candidate> candidates, int density)
        {
            var result = new List<Candidate>(); double den = density / 100.0;
            var buckets = new Dictionary<int, List<int>>();
            for (int i = 0; i < candidates.Count; i++)
            {
                int key = candidates[i].Y / VerticalConflict * 64 + candidates[i].X / ConflictBucketSize;
                if (!buckets.TryGetValue(key, out var list)) buckets.Add(key, list = new List<int>());
                list.Add(i);
            }
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i]; bool wins = true;
                double ownRange = candidate.Radius * (1.45 + (1 - den) * 1.2);
                int bucketX = candidate.X / ConflictBucketSize, bucketY = candidate.Y / VerticalConflict;
                for (int by = Math.Max(0, bucketY - 1); by <= bucketY + 1 && wins; by++)
                for (int bx = Math.Max(0, bucketX - 1); bx <= bucketX + 1 && wins; bx++)
                {
                    if (!buckets.TryGetValue(by * 64 + bx, out var neighbors)) continue;
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int j = neighbors[n]; if (i == j) continue; var other = candidates[j];
                        double range = Math.Max(ownRange, other.Radius * 1.15);
                        if (Math.Abs(other.X - candidate.X) < range && Math.Abs(other.Y - candidate.Y) < VerticalConflict && Compare(other, candidate) < 0)
                        { wins = false; break; }
                    }
                }
                if (wins) result.Add(candidate);
            }
            result.Sort(Compare); return result;
        }

        private static List<Lobe> CreateLobes(Candidate candidate, int petal, int variation, uint seed)
        {
            double variationScale = variation / 100.0; var lobes = new List<Lobe>();
            lobes.Add(new Lobe(candidate.X, candidate.Y - candidate.Extent * .35, candidate.Radius * .90, Math.Max(2, candidate.Extent * .65)));
            int count = Math.Max(3, Math.Min(7, R(candidate.Radius * 1.65 / (petal * 1.7))));
            for (int i = 0; i < count; i++)
            {
                double u = i / (double)(count - 1) * 2 - 1, jitter = (Hash(candidate.X + i, candidate.Y, seed + 53) - .5) * variationScale;
                double rx = Math.Max(petal, candidate.Extent * .32) * (1 + jitter * .60);
                double ry = rx * (.84 + Hash(candidate.X + i, candidate.Y, seed + 61) * .23);
                double bottom = candidate.Y + candidate.Extent * (.98 - .20 * Math.Abs(u)) - Hash(candidate.X + i, candidate.Y, seed + 67) * variationScale * .8;
                lobes.Add(new Lobe(candidate.X + u * candidate.Radius * .74 + jitter * 1.2, bottom - ry, rx, ry));
            }
            return lobes;
        }

        private static int Compare(Candidate left, Candidate right) => left.Priority != right.Priority ? left.Priority.CompareTo(right.Priority) :
            left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X);
        private static int R(double value) => (int)Math.Floor(value + .5);

        /// <summary>已按世界像素稳定生成的一个表面候选；胜负只比较固定冲突域中的候选。</summary>
        private readonly struct Candidate
        {
            internal readonly int X, Y; internal readonly double Radius, Extent, Priority;
            internal Candidate(int x, int y, double radius, double extent, double priority)
            { X = x; Y = y; Radius = radius; Extent = extent; Priority = priority; }
        }

        /// <summary>一个有确定椭圆支撑域的圆簇瓣，用于按固定次序平滑并入基础岩壁。</summary>
        private readonly struct Lobe
        {
            internal readonly double X, Y, Rx, Ry;
            internal Lobe(double x, double y, double rx, double ry) { X = x; Y = y; Rx = rx; Ry = ry; }
        }

        /// <summary>一个按锚点优先级稳定生成的簇及其预计算瓣形，避免输出像素循环重复分配形状数组。</summary>
        private readonly struct Cluster
        {
            internal readonly Candidate Anchor;
            internal readonly Lobe[] Lobes;
            internal Cluster(Candidate anchor, List<Lobe> lobes) { Anchor = anchor; Lobes = lobes.ToArray(); }
        }
    }
}
