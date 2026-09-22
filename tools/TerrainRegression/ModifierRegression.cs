using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tools.TerrainRegression
{
    /// <summary>两种 modifier 的独立 H5 金样、原轮廓保留、分页、破坏差异及可替换背景接口回归；不替代实际 Player 验收。</summary>
    internal static class ModifierRegression
    {
        internal static void Run()
        {
            var cases = JsonDocument.Parse(File.ReadAllText("tools/terrain-modifiers/reference/golden.json")).RootElement.GetProperty("cases");
            int count = 0, pages = 0;
            foreach (var c in cases.EnumerateArray())
            {
                int w = c.GetProperty("width").GetInt32(), h = c.GetProperty("height").GetInt32(), top = c.GetProperty("top").GetInt32();
                var raw = c.GetProperty("source").GetString() == "profile" ? File.ReadAllBytes("tools/contour-reference/outline-hybridB.bin") : Wide();
                var original = new CaveMaskField(raw, w, h, top); var cfg = c.GetProperty("cfg");
                uint seed = c.GetProperty("seed").GetUInt32(); bool down = c.GetProperty("mode").GetString() == "downward";
                int P(string key) => cfg.GetProperty(key).GetInt32();
                var result = down ? new DownwardEdgeModifier(P("length"), P("density"), P("width"), P("sharpness"), P("variation")).ApplySeed(original, seed) :
                    new RoundedClusterModifier(P("depth"), P("size"), P("petal"), P("density"), P("variation")).ApplySeed(original, seed);
                var mask = result.CopyPixels();
                Require(Hash(mask) == c.GetProperty("sha256").GetString(), "H5 exact " + c);
                Require(raw.SequenceEqual(original.CopyPixels()), "input mutated");
                for (int i = 0; i < raw.Length; i++)
                {
                    if (raw[i] != 0) Require(mask[i] != 0, "removed original solid");
                    if (mask[i] == 0 || raw[i] != 0) continue;
                    Require(i / w >= top, "above underground boundary");
                    int root = i, reach = 0;
                    while (root >= 0 && raw[root] == 0) { Require(mask[root] != 0, "floating pixel"); root -= w; reach++; }
                    Require(root >= 0 && reach <= P(down ? "length" : "depth"), "attachment/depth cap");
                }
                var full = CaveRockBaker.Bake(original.Solid, w, h, "DN-MATERIAL-0921", 0, 0, w, h, stoneSize: 4, modified: result);
                foreach (var a in new[] { new[] { 0, 0, 101, 101 }, new[] { 217, 81, 149, 145 }, new[] { w - 130, h - 129, 130, 129 } })
                {
                    var page = CaveRockBaker.Bake(original.Solid, w, h, "DN-MATERIAL-0921", a[0], a[1], a[2], a[3], stoneSize: 4, modified: result);
                    for (int y = 0; y < a[3]; y++) for (int x = 0; x < a[2] * 4; x++)
                        Require(page[y * a[2] * 4 + x] == full[((a[1] + y) * w + a[0]) * 4 + x], "modifier RGBA seam");
                    pages++;
                }
                Require(!CaveMaskChanges.DirtyPages(result, result).Any(v => v), "unchanged geometry dirties pages"); count++;
            }
            var field = new CaveMaskField(Wide(), 1024, 384);
            Require(ReferenceEquals(field, new DownwardEdgeModifier(length: 0).Apply(field, "test")), "zero length");
            Require(ReferenceEquals(field, new RoundedClusterModifier(density: 0).Apply(field, "test")), "zero density");
            var stack = new CaveModifierStack(new ICaveMaskModifier[] { new RoundedClusterModifier(), new DownwardEdgeModifier() });
            Require(stack.Apply(field, "A").CopyPixels().SequenceEqual(stack.Apply(field, "A").CopyPixels()), "stack determinism");
            var before = stack.Apply(field, "A"); var edited = field.CopyPixels();
            for (int y = 75; y < 135; y++) for (int x = 246; x < 275; x++) edited[y * 1024 + x] = 0;
            var after = stack.Apply(new CaveMaskField(edited, 1024, 384), "A");
            var dirty = CaveMaskChanges.DirtyPages(before, after);
            var rgbaBefore = CaveRockBaker.Bake(field.Solid, 1024, 384, "A", 0, 0, 1024, 384, modified: before);
            var rgbaAfter = CaveRockBaker.Bake(field.Solid, 1024, 384, "A", 0, 0, 1024, 384, modified: after);
            for (int i = 0; i < rgbaBefore.Length; i++) if (rgbaBefore[i] != rgbaAfter[i])
                Require(dirty[(i / 4 / 1024 / 256) * 4 + i / 4 % 1024 / 256], "edited RGBA escaped invalidation");
            Background();
            Console.WriteLine($"Modifiers: {count} exact H5 masks, {pages} exact RGBA slices; immutable/attachment/depth/stack/edit invalidation passed.");
        }
        private static void Background()
        {
            var raw = File.ReadAllBytes("tools/contour-reference/golden-source.bin");
            var layout = new BackgroundContourBaker(raw, 504, 312, "DN-MATERIAL-0921", 56);
            var none = CaveModifierStack.Empty; var round = new CaveModifierStack(new[] { new RoundedClusterModifier(grain: false) });
            var modified = new ModifiedBackgroundLayout(layout, new[] { round, none, round }, "DN-MATERIAL-0921");
            var a = BackgroundPageBaker.Bake(layout, 0, 0, 504, 312); var b = BackgroundPageBaker.Bake(modified, 0, 0, 504, 312);
            Require(a[1].SequenceEqual(b[1]) && !a[0].SequenceEqual(b[0]) && !a[2].SequenceEqual(b[2]), "independent background stacks");
            var off = new BackgroundContourBaker(raw, 504, 312, "DN-MATERIAL-0921", 56, settings:
                new BackgroundContourSettings(nearAmount: 0, middleAmount: 0, deepAmount: 0));
            Require(BackgroundPageBaker.Bake(off, 0, 0, 504, 312).All(l => l.All(v => v == 0)), "disabled generator layers");
            var page = BackgroundPageBaker.Bake(modified, 205, 80, 256, 200);
            for (int l = 0; l < 3; l++) for (int y = 0; y < 200; y++) for (int x = 0; x < 1024; x++)
                Require(page[l][y * 1024 + x] == b[l][((y + 80) * 504 + 205) * 4 + x], "background modifier page seam");
            var terrain = ExpeditionTerrainGenerator.Generate("MODIFIER-0922", "00000000000000000000000000000002");
            var reference = new BackgroundBakeDescriptor(terrain.WorldId, terrain.Seed, terrain.CopyMaterials(), terrain.CopyShapes());
            var watch = Stopwatch.StartNew(); var pixels = TerrainVisualCoordinates.Rasterize(reference);
            var result = CaveModifiedTerrain.Bake((x,y) => pixels[y * 2560 + x] != 0, 2560, 1536,
                terrain.Seed, CaveOutlineSettings.Current, round, 344);
            Console.WriteLine("Modifier full formal-map CPU (.NET, not Unity/frontend): " + watch.ElapsedMilliseconds + "ms; " + Hash(result.CopyPixels()));
        }
        private static byte[] Wide()
        {
            var mask = new byte[1024 * 384];
            for (int y = 0; y < 384; y++) for (int x = 0; x < 1024; x++) mask[y * 1024 + x] =
                y < 100 + (int)Math.Floor(4 * Math.Sin(x / 37.0)) || y > 280 ? (byte)1 : (byte)0;
            return mask;
        }
        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
