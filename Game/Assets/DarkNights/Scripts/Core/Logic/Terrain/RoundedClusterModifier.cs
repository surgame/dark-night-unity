using System;
using System.Collections.Generic;
using static DarkNights.Core.Logic.Terrain.BackgroundPixelMath;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>H5 花菜圆簇规则；宽肩和多瓣椭圆平滑并入原下缘，同列附着且保留净空，可独立输出向墙内渐退的岩粒着色层。</summary>
    public sealed class RoundedClusterModifier : ICaveMaskModifier
    {
        private readonly int depth, size, petal, density, variation;
        private readonly bool grain;
        private readonly string seed;
        private readonly RoundedClusterAlgorithmVersion algorithmVersion;
        public string Identity => string.Join(",", algorithmVersion == RoundedClusterAlgorithmVersion.LocalV2 ? "rounded-local-v2" : "rounded-v1",
            seed.Length + ":" + seed, depth, size, petal, density, variation, grain);
        public int Depth => depth;
        public int Size => size;
        public int Petal => petal;
        public int Density => density;
        public int Variation => variation;
        public bool Grain => grain;
        public string ModifierSeed => seed;
        public RoundedClusterAlgorithmVersion AlgorithmVersion => algorithmVersion;
        public int DependencyRadiusPixels => LocalRoundedClusterV2.DependencyRadius(depth, size, petal, density);
        public RoundedClusterModifier(int depth = 8, int size = 20, int petal = 3, int density = 90, int variation = 65,
            bool grain = true, string seed = "", RoundedClusterAlgorithmVersion algorithmVersion = RoundedClusterAlgorithmVersion.LegacyV1)
        {
            if (depth < 0 || depth > 12 || size < 8 || size > 30 || petal < 2 || petal > 6 || density < 0 || density > 100 ||
                variation < 0 || variation > 100 || seed == null || seed.Length > 80 ||
                !System.Enum.IsDefined(typeof(RoundedClusterAlgorithmVersion), algorithmVersion)) throw new ArgumentException("圆簇参数超出原生像素合同。");
            this.depth = depth; this.size = size; this.petal = petal; this.density = density;
            this.variation = variation; this.grain = grain; this.seed = seed; this.algorithmVersion = algorithmVersion;
        }
        public CaveMaskField Apply(CaveMaskField source, string worldSeed, Action checkpoint = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (algorithmVersion == RoundedClusterAlgorithmVersion.LegacyV1)
                return ApplySeed(source, Seed((seed.Length == 0 ? worldSeed : seed) + "|rounded-native-v1"), checkpoint);
            if (depth == 0 || density == 0) return source;
            var input = new CaveMaskRegion(source.CopyPixels(), source.Width, source.Height, 0, 0, source.Width, source.Height);
            var output = LocalRoundedClusterV2.Apply(input, 0, 0, source.Width, source.Height, worldSeed,
                depth, size, petal, density, variation, grain, seed, source.Top, checkpoint);
            var layer = output.HasGrain ? new CaveGrainLayer(output.CopyGrainWeights(), output.CopyGrainTones(), output.GrainStoneSize) : null;
            return source.With(output.CopyPixels(), layer);
        }

        /// <summary>用固定样式参数重算目标局部像素；输入基础轮廓需包含声明的冲突与资格采样 Halo。</summary>
        public CaveMaskRegion ApplyRegion(CaveMaskRegion source, int left, int top, int width, int height,
            string worldSeed, int candidateTop, Action checkpoint = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (depth == 0 || density == 0) return source.Slice(left, top, width, height);
            return LocalRoundedClusterV2.Apply(source, left, top, width, height, worldSeed,
                depth, size, petal, density, variation, grain, seed, candidateTop, checkpoint);
        }
        public CaveMaskField ApplySeed(CaveMaskField source, uint s, Action checkpoint = null)
        {
            if (depth == 0 || density == 0) return source;
            int w = source.Width, h = source.Height;
            double variationScale = variation / 100.0, den = density / 100.0;
            var mask = source.CopyPixels(); var field = CaveChamferField.Signed(mask, w, h, checkpoint);
            var influence = grain ? new float[mask.Length] : null; var tones = grain ? new byte[mask.Length] : null;
            var candidates = new List<(int X, int Y, double Priority)>(); var accepted = new CaveEdgeSpacing();
            for (int y = source.Top + 3; y < h - 4; y++)
            {
                checkpoint?.Invoke();
                for (int x = 3; x < w - 3; x++)
                {
                    if (!source.Solid(x, y) || source.Solid(x, y + 1) || !source.Solid(x, y - 2)) continue;
                    int above = 0, below = 0;
                    for (int dx = -2; dx <= 2; dx++) for (int dy = 1; dy <= 3; dy++)
                    { if (source.Solid(x + dx, y - dy)) above++; if (source.Solid(x + dx, y + dy)) below++; }
                    if (above < 10 || below > 6 || Hash(x, y, s + 17) > den) continue;
                    candidates.Add((x, y, Hash(x, y, s + 29)));
                }
            }
            candidates.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) :
                a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            foreach (var a in candidates)
            {
                checkpoint?.Invoke(); int x = a.X, y = a.Y;
                double radius = size * .5 * (1 + (Hash(x, y, s + 31) - .5) * .65 * variationScale);
                if (accepted.Overlaps(x, y, radius * (1.45 + (1 - den) * 1.2), 8, true)) continue;
                int free = 0;
                while (y + free + 1 < h && !source.Solid(x, y + free + 1) && free < 40) free++;
                double extent = Math.Min(depth * (1 - .4 * variationScale * Hash(x, y, s + 41)), Math.Floor(free * .42));
                if (extent < 1) continue;
                var lobes = new List<(double X, double Y, double Rx, double Ry)>();
                lobes.Add((x, y - extent * .35, radius * .90, Math.Max(2, extent * .65)));
                int count = Math.Max(3, Math.Min(7, R(radius * 1.65 / (petal * 1.7))));
                for (int i = 0; i < count; i++)
                {
                    double u = i / (double)(count - 1) * 2 - 1, jitter = (Hash(x + i, y, s + 53) - .5) * variationScale;
                    double rx = Math.Max(petal, extent * .32) * (1 + jitter * .60), ry = rx * (.84 + Hash(x + i, y, s + 61) * .23);
                    double bottom = y + extent * (.98 - .20 * Math.Abs(u)) - Hash(x + i, y, s + 67) * variationScale * .8;
                    lobes.Add((x + u * radius * .74 + jitter * 1.2, bottom - ry, rx, ry));
                }
                int start = Math.Max(1, (int)Math.Floor(x - radius - petal)), end = Math.Min(w - 2, (int)Math.Ceiling(x + radius + petal));
                for (int xx = start; xx <= end; xx++)
                {
                    int root = -1, best = int.MaxValue;
                    for (int yy = Math.Max(source.Top, y - 4); yy <= Math.Min(h - 4, y + 4); yy++)
                        if (source.Solid(xx, yy) && !source.Solid(xx, yy + 1) && source.Solid(xx, yy - 2) && Math.Abs(yy - y) < best)
                        { root = yy; best = Math.Abs(yy - y); }
                    if (root < 0) continue;
                    free = 0;
                    while (root + free + 1 < h && !source.Solid(xx, root + free + 1) && free < depth + 6) free++;
                    int bottom = Math.Min(root + depth, Math.Min(root + free - 3, h - 1));
                    if (bottom <= root) continue;
                    int reach = 0;
                    for (int yy = Math.Max(source.Top, root - 10); yy <= bottom; yy++)
                    {
                        int p = yy * w + xx, owner = -1; double d = field[p], closest = double.PositiveInfinity;
                        for (int li = 0; li < lobes.Count; li++)
                        {
                            var l = lobes[li]; double nx = (xx - l.X) / l.Rx, ny = (yy - l.Y) / l.Ry;
                            double sd = (Math.Sqrt(nx * nx + ny * ny) - 1) * Math.Min(l.Rx, l.Ry);
                            double t = Clamp(.5 + .5 * (sd - d) / .65);
                            d = sd * (1 - t) + d * t - .65 * t * (1 - t);
                            if (sd < closest) { closest = sd; owner = li; }
                        }
                        field[p] = (float)d;
                        if (grain)
                        {
                            influence[p] = (float)Math.Max(influence[p], Clamp((yy - (root - 8)) / 5.0) * Clamp((radius + 2 - Math.Abs(xx - x)) / 3));
                            if (closest < 2 && owner >= 0)
                            {
                                var l = lobes[owner]; double nx = (xx - l.X) / l.Rx, ny = (yy - l.Y) / l.Ry;
                                int face = 2 + (int)Math.Floor(Hash(R(l.X), R(l.Y), s + 313) * 4);
                                tones[p] = (byte)(face + (nx < -.25 ? 1 : 0) - (nx > .45 || ny > .60 ? 1 : 0) + 1);
                            }
                        }
                        if (yy > root && d <= 0) reach = yy - root;
                    }
                    for (int dy = 1; dy <= reach; dy++) mask[(root + dy) * w + xx] = 1;
                }
                accepted.Add(x, y, radius);
            }
            return source.With(mask, grain ? new CaveGrainLayer(influence, tones, Math.Max(2.5, Math.Min(4, petal * 1.1))) : null);
        }
        private static int R(double value) => (int)Math.Floor(value + .5);
    }
}
