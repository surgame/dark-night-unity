using System;
using System.IO;
using System.Linq;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tools.TerrainRegression
{
    /// <summary>对照浏览器真实岩壁字节，校验同源分面、页边和修改后局部距离更新；不以风格算法通过代替游戏画面认可。</summary>
    internal static class RockRegression
    {
        internal static void Run()
        {
            OutlineRegression.Run();
            const int w = 504, h = 312;
            var source = File.ReadAllBytes("tools/contour-reference/golden-occupancy.bin");
            var expected = File.ReadAllBytes("tools/contour-reference/golden-foreground.bin");
            bool Solid(int x, int y) => source[y * w + x] != 0;
            var actual = CaveRockBaker.Bake(Solid, w, h, "DN-MATERIAL-0921", 0, 0, w, h);
            int mismatches = actual.Where((v, i) => v != expected[i]).Count();
            if (mismatches != 0) throw new Exception("H5 rock mismatch bytes=" + mismatches);
            foreach (var area in new[] { new[] { 0, 0, 128, 120 }, new[] { 231, 101, 129, 109 }, new[] { 410, 245, 94, 67 } }) Check(area);
            for (int y = 110; y < 119; y++) for (int x = 198; x < 219; x++) source[y * w + x] = 0;
            actual = CaveRockBaker.Bake(Solid, w, h, "DN-MATERIAL-0921", 0, 0, w, h);
            Check(new[] { 170, 80, 80, 80 });
            Console.WriteLine("Rock: exact H5 RGBA, 3 unaligned page seams, edited local distance passed; native 8 px/cell.");
            void Check(int[] a)
            {
                var page = CaveRockBaker.Bake(Solid, w, h, "DN-MATERIAL-0921", a[0], a[1], a[2], a[3]);
                for (int y = 0; y < a[3]; y++) for (int x = 0; x < a[2] * 4; x++)
                    if (page[y * a[2] * 4 + x] != actual[((a[1] + y) * w + a[0]) * 4 + x]) throw new Exception("Rock local seam mismatch");
            }
        }
    }
}
