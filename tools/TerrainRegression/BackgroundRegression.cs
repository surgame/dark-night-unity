using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tools.TerrainRegression
{
    /// <summary>以实际浏览器 H5 输出验证背景移植、页缝及冻结语义；离线通过不替代 Unity 画面验收。</summary>
    internal static class BackgroundRegression
    {
        internal static void Run()
        {
            RockRegression.Run();
            var meta = JsonDocument.Parse(File.ReadAllText("tools/contour-reference/golden.json")).RootElement;
            int w = meta.GetProperty("width").GetInt32(), h = meta.GetProperty("height").GetInt32();
            var baker = new BackgroundContourBaker(File.ReadAllBytes("tools/contour-reference/golden-source.bin"),
                w, h, meta.GetProperty("seed").GetString(), 56);
            foreach (int softness in new[] { 0, 2, 4 })
            {
                var actual = BackgroundPageBaker.Bake(baker, 0, 0, w, h, softness);
                for (int layer = 0; layer < 3; layer++)
                {
                    byte[] expected = File.ReadAllBytes(layer == 1 ? "tools/contour-reference/golden-soft-" + softness + ".rgba" :
                        "tools/contour-reference/golden-layer-" + layer + ".rgba");
                    int alphaErrors = 0, maxColorError = 0, colorErrors = 0;
                    for (int k = 0; k < expected.Length; k += 4)
                    {
                        if (actual[layer][k + 3] != expected[k + 3]) alphaErrors++;
                        if (expected[k + 3] == 0) continue;
                        for (int c = 0; c < 3; c++)
                        {
                            int error = Math.Abs(actual[layer][k + c] - expected[k + c]);
                            maxColorError = Math.Max(maxColorError, error);
                            // Canvas stores premultiplied bytes: unpremultiplication magnifies half-byte rounding at low alpha.
                            if (error > Math.Ceiling(127.5 / expected[k + 3]) + 1) colorErrors++;
                        }
                    }
                    if (alphaErrors != 0 || colorErrors != 0)
                        throw new Exception("H5 mismatch: softness=" + softness + " layer=" + layer + " alpha=" + alphaErrors + " rgbMax=" + maxColorError);
                }
                foreach (var r in new[] { new[] { 0, 0, 51, 73 }, new[] { 137, 55, 173, 121 }, new[] { w - 67, h - 79, 67, 79 } })
                {
                    var page = BackgroundPageBaker.Bake(baker, r[0], r[1], r[2], r[3], softness);
                    for (int l = 0; l < 3; l++) for (int y = 0; y < r[3]; y++) for (int x = 0; x < r[2] * 4; x++)
                        if (page[l][y * r[2] * 4 + x] != actual[l][((r[1] + y) * w + r[0]) * 4 + x])
                            throw new Exception("Background page seam mismatch");
                }
            }
            var watch = Stopwatch.StartNew();
            var terrain = ExpeditionTerrainGenerator.Generate("CONTOUR-0922", "00000000000000000000000000000001");
            var reference = new BackgroundBakeDescriptor(terrain.WorldId, terrain.Seed, terrain.CopyMaterials(), terrain.CopyShapes());
            byte[] copy = reference.CopyMaterials(); copy[0] ^= 1;
            if (reference.Material(0, 0) == copy[0]) throw new Exception("Mutable reference escaped");
            var a = BackgroundPageBaker.Bake(BackgroundContourBaker.Build(reference), 128, 352, 256, 256);
            var b = BackgroundPageBaker.Bake(BackgroundContourBaker.Build(reference), 128, 352, 256, 256);
            for (int l = 0; l < 3; l++) if (!a[l].SequenceEqual(b[l])) throw new Exception("Non-deterministic background");
            for (int shape = 1; shape <= 12; shape++)
            {
                var material = terrain.CopyMaterials(); var shapes = terrain.CopyShapes(); material[50 * 320 + 50] = 1; shapes[50 * 320 + 50] = (byte)shape;
                var pixels = TerrainVisualCoordinates.Rasterize(new BackgroundBakeDescriptor(terrain.WorldId, terrain.Seed, material, shapes));
                for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                    if ((pixels[(400 + y) * 2560 + 400 + x] != 0) != TerrainShapeGeometry.Contains((TerrainCellShape)shape, (x + .5f) / 8, 1 - (y + .5f) / 8))
                        throw new Exception("Slope raster mismatch");
            }
            Console.WriteLine("Background: H5 layers/softness 9/9, page slices 9/9, immutable/deterministic/slope-12 passed; formal ms=" + watch.ElapsedMilliseconds);
        }
    }
}
