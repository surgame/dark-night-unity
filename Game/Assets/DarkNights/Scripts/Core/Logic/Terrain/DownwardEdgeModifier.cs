using System;
using System.Collections.Generic;
using static DarkNights.Core.Logic.Terrain.BackgroundPixelMath;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>H5 v3 下坠岩齿规则；仅沿输入下边缘增补，100% 密度解除随机禁生区域，参数与种子在构造时冻结。</summary>
    public sealed class DownwardEdgeModifier : ICaveMaskModifier
    {
        private readonly int length, density, width, sharpness, variation;
        private readonly string seed;
        public string Identity => string.Join(",", "downward-v3", seed.Length + ":" + seed, length, density, width, sharpness, variation);
        public DownwardEdgeModifier(int length = 7, int density = 100, int width = 5, int sharpness = 0, int variation = 89, string seed = "")
        {
            if (length < 0 || length > 24 || density < 0 || density > 100 || width < 3 || width > 19 ||
                sharpness < 0 || sharpness > 100 || variation < 0 || variation > 100 || seed == null || seed.Length > 80)
                throw new ArgumentException("下坠参数超出原生像素合同。");
            this.length = length; this.density = density; this.width = width;
            this.sharpness = sharpness; this.variation = variation; this.seed = seed;
        }
        public CaveMaskField Apply(CaveMaskField source, string worldSeed, Action checkpoint = null)
        {
            if (length == 0 || density == 0) return source;
            uint s = Seed((seed.Length == 0 ? worldSeed : seed) + "|downward-only-v1");
            return ApplySeed(source, s, checkpoint);
        }
        public CaveMaskField ApplySeed(CaveMaskField source, uint s, Action checkpoint = null)
        {
            if (length == 0 || density == 0) return source;
            int w = source.Width, h = source.Height;
            double v = variation / 100.0, sharp = sharpness / 100.0, den = density / 100.0;
            var mask = source.CopyPixels(); var accepted = new CaveEdgeSpacing();
            var candidates = new List<(int X, int Y, bool Supplemental, double Priority)>();
            for (int y = source.Top + 3; y < h - 7; y++)
            {
                checkpoint?.Invoke();
                for (int x = 5; x < w - 5; x++)
                {
                    if (!source.Solid(x, y) || source.Solid(x, y + 1) || !source.Solid(x, y - 3)) continue;
                    int above = 0, below = 0;
                    for (int dx = -3; dx <= 3; dx++) for (int dy = 1; dy <= 3; dy++)
                    { if (source.Solid(x + dx, y - dy)) above++; if (source.Solid(x + dx, y + dy)) below++; }
                    if (above < 17 || below > 7) continue;
                    double grouping = Noise(x / 39.0, y / 31.0, s + 11), random = Hash(x, y, s + 17), chance = .10 + grouping * .17;
                    double coverage = Math.Max(0, (den - .65) / .35);
                    if (grouping < .31 * (1 - coverage) || random > chance * den + (1 - chance * den) * coverage) continue;
                    candidates.Add((x, y, grouping < .31 || random > chance, Hash(x, y, s + 29)));
                }
            }
            candidates.Sort((a, b) => a.Supplemental != b.Supplemental ? a.Supplemental.CompareTo(b.Supplemental) :
                a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            foreach (var a in candidates)
            {
                checkpoint?.Invoke(); int x = a.X, y = a.Y;
                int radius = Math.Max(1, R((width - 1) / 2.0 * (1 + (Hash(x, y, s + 31) - .5) * .55 * v)));
                if (x - radius < 0 || x + radius >= w) continue;
                double spacing = (7 + Math.Floor(Hash(x, y, s + 37) * 13)) * (1.25 - .25 * den);
                if (accepted.Overlaps(x, y, spacing, 12, false)) continue;
                int free = 0;
                while (y + free + 1 < h && !source.Solid(x, y + free + 1) && free < 40) free++;
                int wanted = Math.Max(1, R(length * (1 - v * .72 * (1 - Math.Pow(Hash(x, y, s + 41), 1.4)))));
                int extent = Math.Min(wanted, (int)Math.Floor(free * .48));
                if (extent < 1) continue;
                int tip = R((Hash(x, y, s + 43) - .5) * radius * v);
                var columns = new List<(int X, int Root, int Reach)>();
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int xx = x + dx, root = -1;
                    for (int yy = y - 3; yy <= y + 3; yy++)
                        if (source.Solid(xx, yy) && !source.Solid(xx, yy + 1) && source.Solid(xx, yy - 2)) root = yy;
                    if (root < 0) continue;
                    int side = dx <= tip ? radius + tip + 1 : radius - tip + 1, flat = R((1 - sharp) * radius * .60);
                    double taper = Math.Max(0, 1 - Math.Max(0, Math.Abs(dx - tip) - flat) / (double)Math.Max(1, side - flat));
                    double exponent = .35 + sharp * .90 + (Hash(x, y, s + 47) - .5) * .25 * v;
                    int reach = R(extent * Math.Pow(taper, exponent));
                    if (extent > 4 && Math.Abs(dx - tip) > flat + 1) reach = reach / 2 * 2;
                    free = 0;
                    while (root + free + 1 < h && !source.Solid(xx, root + free + 1) && free < reach + 3) free++;
                    reach = Math.Min(reach, free - 3);
                    if (reach > 0) columns.Add((xx, root, reach));
                }
                if (columns.Count < Math.Max(2, Math.Min(5, radius + 1))) continue;
                int count = 0;
                foreach (var col in columns) for (int dy = 1; dy <= col.Reach; dy++)
                {
                    int p = (col.Root + dy) * w + col.X;
                    if (mask[p] != 0) continue;
                    mask[p] = 1; count++;
                }
                if (count > 0) accepted.Add(x, y, radius);
            }
            return source.With(mask);
        }
        private static int R(double v) => (int)Math.Floor(v + .5);
    }
}
