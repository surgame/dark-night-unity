using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tools.TerrainRegression
{
    /// <summary>独立外轮廓六方案与 H5 字节对照、分页和破坏回归；同时测量完整材质烘焙并量化固定十六图块的信息损失。</summary>
    internal static class OutlineRegression
    {
        internal static void Run()
        {
            const int w = 504, h = 312;
            string root = "tools/contour-reference/";
            var raw = File.ReadAllBytes(root + "profile-raw.bin");
            bool Solid(int x, int y) => raw[y * w + x] != 0;
            foreach (CaveOutlineMode mode in Enum.GetValues(typeof(CaveOutlineMode)))
            {
                var p = new CaveOutlineSettings(mode, "OUTLINE-0921", 3, 20, 2);
                var full = CaveOutlineBaker.Bake(Solid, w, h, 0, 0, w, h, p);
                Require(full.SequenceEqual(File.ReadAllBytes(root + "outline-" + p.ModeKey + ".bin")), "H5 outline " + mode);
                foreach (var area in new[] { new[] { 0, 0, 91, 77 }, new[] { 199, 139, 85, 79 }, new[] { 419, 233, 85, 79 } })
                    CheckSlice(full, CaveOutlineBaker.Bake(Solid, w, h, area[0], area[1], area[2], area[3], p), w, area, 1);
            }
            var profile = CaveOutlineSettings.Current;
            var shaped = CaveOutlineBaker.Bake(Solid, w, h, 0, 0, w, h, profile);
            var toAir = BackgroundPixelMath.Distance(raw, w, h, 0); var toRock = BackgroundPixelMath.Distance(raw, w, h, 1);
            int changed = 0, maxIn = 0, maxOut = 0;
            for (int i = 0; i < raw.Length; i++) if (raw[i] != shaped[i])
            {
                changed++;
                if (raw[i] == 0) maxOut = Math.Max(maxOut, (int)toRock[i]); else maxIn = Math.Max(maxIn, (int)toAir[i]);
            }
            Console.WriteLine($"Contour silhouette: changed={changed} pixels; maximum added distance={maxOut}px removed depth={maxIn}px; collision retains authoritative slopes.");
            var watch = Stopwatch.StartNew();
            var actual = CaveRockBaker.Bake(Solid, w, h, "DN-MATERIAL-0921", 0, 0, w, h, null, 4, profile);
            double fullMs = watch.Elapsed.TotalMilliseconds;
            Require(actual.SequenceEqual(File.ReadAllBytes(root + "profile-foreground.bin")), "H5 complete 4/3/20/2 rock");
            // 每个局部规则只选一张 8px 图；同一掩码在不同世界坐标具有不同轮廓、距离和分面。
            var masks = File.ReadAllBytes(root + "profile-masks.bin");
            var tiles = new Dictionary<int, HashSet<string>>();
            for (int cy = 0; cy < 39; cy++) for (int cx = 0; cx < 63; cx++)
            {
                var tile = new byte[256];
                for (int y = 0; y < 8; y++) Array.Copy(actual, ((cy * 8 + y) * w + cx * 8) * 4, tile, y * 32, 32);
                int mask = masks[cy * 63 + cx];
                if (!tiles.ContainsKey(mask)) tiles[mask] = new HashSet<string>();
                tiles[mask].Add(Convert.ToBase64String(tile));
            }
            int variants = tiles.Values.Sum(v => v.Count);
            Require(variants > 16, "full material cannot be encoded by one tile per local mask");
            var pageTimes = new List<double>(); var a = new[] { 117, 32, 256, 256 };
            for (int i = 0; i < 7; i++)
            {
                watch.Restart();
                var page = CaveRockBaker.Bake(Solid, w, h, "DN-MATERIAL-0921", a[0], a[1], a[2], a[3], null, 4, profile);
                pageTimes.Add(watch.Elapsed.TotalMilliseconds); CheckSlice(actual, page, w, a, 4);
            }
            for (int y = 115; y < 128; y++) for (int x = 200; x < 217; x++) raw[y * w + x] = 0;
            actual = CaveRockBaker.Bake(Solid, w, h, "DN-MATERIAL-0921", 0, 0, w, h, null, 4, profile);
            CheckSlice(actual, CaveRockBaker.Bake(Solid, w, h, "DN-MATERIAL-0921", 117, 32, 256, 256, null, 4, profile), w, a, 4);
            pageTimes.Sort();
            Console.WriteLine($"Outline: 6 exact H5 modes, 18 arbitrary pages, full RGBA and edited page exact; profile=4/3/20/2 HybridB.");
            Console.WriteLine($"Bake CPU: full {w}x{h}={fullMs:F2} ms; 256px page median={pageTimes[3]:F2} ms max={pageTimes[6]:F2} ms; unique material tiles={variants} across {tiles.Count} masks.");
        }
        private static void CheckSlice(byte[] full, byte[] page, int width, int[] a, int channels)
        {
            for (int y = 0; y < a[3]; y++) for (int x = 0; x < a[2] * channels; x++)
                Require(page[y * a[2] * channels + x] == full[((a[1] + y) * width + a[0]) * channels + x], "outline page seam");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
