using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Experiments.MiningExploration;

namespace DarkNights.Experiments.NestOrientations
{
    /// <summary>
    /// 多方向与共生区离线导出入口；只读真实保存配置、Core、近壁输入及CL04源图。
    /// 每种情境独立验证，集中记录失败；结果冻结供浏览器有限回放，不改游戏／原演示。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3) throw new ArgumentException("需要项目根、近壁快照和独立输出目录。");
            string project = Path.GetFullPath(args[0]), input = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
            using (var document = JsonDocument.Parse(File.ReadAllText(input)))
            {
                var source = document.RootElement; var mining = source.GetProperty("mining"); var identity = mining.GetProperty("identity");
                var local = new LocalTerrainInput(project, mining.GetProperty("mapId").GetString());
                if (Hash(local.Terrain.CopyMaterials()) != identity.GetProperty("finalMaterialSha256").GetString() ||
                    Hash(File.ReadAllBytes(local.ConfigPath)) != identity.GetProperty("configSha256").GetString() ||
                    Hash(File.ReadAllBytes(local.MapPath)) != identity.GetProperty("authorSha256").GetString() ||
                    Hash(File.ReadAllBytes(typeof(PlayableTerrain).Assembly.Location)) != identity.GetProperty("coreSha256").GetString())
                    throw new InvalidOperationException("真实输入身份已变化，不能继续沿用旧近壁样板。");
                var other = mining.GetProperty("veins").EnumerateArray().Where(v => v.GetProperty("id").GetString() != "r1-vein-45395")
                    .SelectMany(v => v.GetProperty("cells").EnumerateArray().Select(c => c.GetInt32())).ToArray();
                var validator = new DirectionalNestValidation(local.Terrain, other); var cases = new List<DirectionalNestCase>(); var errors = new List<string>();
                foreach (string id in new[] { "roof", "low", "turn", "side", "coexist", "none" })
                {
                    try { var candidate = DirectionalNestLayouts.Create(id, source); validator.Validate(candidate); cases.Add(candidate); }
                    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { errors.Add(e.Message); }
                }
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var result = new { schema = 1, version = "nest-orientations-v1", identity, inputSha256 = Hash(File.ReadAllBytes(input)),
                    materials = DirectionalNestValidation.Runs(local.Terrain.CopyMaterials()), shapes = DirectionalNestValidation.Runs(local.Terrain.CopyShapes()),
                    cases, sprites = Sprites(project), errors,
                    timing = new { strikes = new[] { .45,.95,1.45,1.95,2.45,2.95 }, warning = 2.65, breach = 2.95, emergence = 3.3, landing = 4.0, end = 6.2 } };
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(result, options); File.WriteAllBytes(Path.Combine(output, "nest-orientations-snapshot.json"), bytes);
                var report = new { cases = cases.Count, errors, sha256 = Hash(bytes), bytes = bytes.Length,
                    checks = cases.Select(c => new { c.Id, c.Mode, c.Head, c.HasNest, oreCells = c.Ore.Length, c.Stock, c.Remaining,
                        c.Groups, shell = c.Shell.Length, pocket = c.Pocket.Length, c.BlueprintCount, pointPathsValidated = true,
                        bodyAndToolReachValidated = false, runtimeImplemented = false }), exportedUtc = DateTimeOffset.UtcNow };
                File.WriteAllText(Path.Combine(output, "generation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                Console.WriteLine(JsonSerializer.Serialize(report, options)); return errors.Count == 0 ? 0 : 1;
            }
        }

        private static Dictionary<string, object> Sprites(string project)
        {
            var result = new Dictionary<string, object>();
            foreach (string name in new[] { "burrow/occupied", "burrow/empty", "ambush-grub/alert", "ambush-grub/crawl_a", "ambush-grub/crawl_b", "ambush-grub/lunge" })
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(project, "experiments/cl04-creature-art-v1/native/" + name + ".png"));
                result[name] = new { data = "data:image/png;base64," + Convert.ToBase64String(bytes), width = 32, height = 24, pivot = new[] { 16,23 }, sha256 = Hash(bytes) };
            }
            return result;
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return Convert.ToHexString(hash.ComputeHash(bytes)).ToLowerInvariant();
        }
    }
}
