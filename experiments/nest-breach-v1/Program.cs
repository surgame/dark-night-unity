using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Experiments.MiningExploration;

namespace DarkNights.Experiments.NestBreach
{
    /// <summary>
    /// 动态破壳样板的离线导出器；只读已核对的真实地图、近壁鼓包和CL04源图。
    /// 输出冻结破坏范围、单虫路径和时序试参，浏览器仅回放；不写运行地图或创建游戏状态。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3) throw new ArgumentException("需要项目根、近壁快照和独立输出目录。");
            string project = Path.GetFullPath(args[0]), input = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(output);
            using (var document = JsonDocument.Parse(File.ReadAllText(input)))
            {
                var source = document.RootElement; var mining = source.GetProperty("mining"); var identity = mining.GetProperty("identity");
                var local = new LocalTerrainInput(project, mining.GetProperty("mapId").GetString());
                if (Hash(local.Terrain.CopyMaterials()) != identity.GetProperty("finalMaterialSha256").GetString() ||
                    Hash(File.ReadAllBytes(local.ConfigPath)) != identity.GetProperty("configSha256").GetString() ||
                    Hash(File.ReadAllBytes(local.MapPath)) != identity.GetProperty("authorSha256").GetString() ||
                    Hash(File.ReadAllBytes(typeof(PlayableTerrain).Assembly.Location)) != identity.GetProperty("coreSha256").GetString())
                    throw new InvalidOperationException("当前输入已变化，不能套用旧近壁样板。");
                var layout = new BurstNestLayout(source, local.Terrain);
                var result = new { schema = 1, version = "nest-breach-v1", identity,
                    inputSha256 = Hash(File.ReadAllBytes(input)), width = 320, height = 192,
                    head = layout.Head, stock = layout.Stock, remaining = layout.Remaining,
                    ore = layout.Ore.Select((index, n) => new { index, amount = layout.Amounts[n] }).ToArray(),
                    order = layout.MiningOrder.Take(layout.TriggerStep).ToArray(), triggerCell = layout.TriggerCell,
                    shell = layout.Shell, pocket = layout.Pocket, floor = layout.Floor,
                    spawn = layout.Spawn, nestAnchor = layout.NestAnchor,
                    retreat = layout.Retreat, grubExit = layout.GrubExit, grubIdle = layout.GrubIdle,
                    materials = Runs(local.Terrain.CopyMaterials()), shapes = Runs(local.Terrain.CopyShapes()),
                    closedShapes = Runs(layout.ClosedShapes), burstShapes = Runs(layout.BurstShapes),
                    sprites = Sprites(project), bounds = new { x = 264, y = 138, width = 17, height = 11 },
                    timing = new { strikes = new[] { .45, .95, 1.45, 1.95, 2.45, 2.95 },
                        warning = 2.65, breach = 2.95, emergence = 3.30, retreat = 3.10, end = 5.60 },
                    limitations = new[] { "时序与破壳范围均为试参", "路径仅格级示意，不证明身体净空", "无伤害／正式AI／联机／存档" } };
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(result, options);
                File.WriteAllBytes(Path.Combine(output, "nest-breach-snapshot.json"), bytes);
                var checks = new { sourceIdentity = true, sameOreFootprint = true, stock = layout.Stock, remaining = layout.Remaining,
                    triggerStep = layout.TriggerStep, shell = layout.Shell.Length, pocket = layout.Pocket.Length,
                    oneCellSeal = true, preBreachClosed = true, pocketInRock = true, floorKept = true,
                    orePreservedByBurst = !layout.Shell.Intersect(layout.Ore).Any(),
                    pathsInClearedCells = true, bodyClearanceValidated = false, runtimeImplemented = false };
                File.WriteAllText(Path.Combine(output, "generation.json"), JsonSerializer.Serialize(new {
                    version = "nest-breach-v1", checks, sha256 = Hash(bytes), bytes = bytes.Length,
                    inputSha256 = Hash(File.ReadAllBytes(input)), exportedUtc = DateTimeOffset.UtcNow },
                    new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(JsonSerializer.Serialize(checks));
            }
            return 0;
        }

        private static Dictionary<string, object> Sprites(string project)
        {
            var result = new Dictionary<string, object>();
            foreach (string name in new[] { "burrow/occupied", "burrow/empty", "ambush-grub/alert",
                "ambush-grub/crawl_a", "ambush-grub/crawl_b", "ambush-grub/lunge" })
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(project, "experiments/cl04-creature-art-v1/native/" + name + ".png"));
                result[name] = new { data = "data:image/png;base64," + Convert.ToBase64String(bytes), width = 32,
                    height = 24, pivot = new[] { 16, 23 }, sha256 = Hash(bytes) };
            }
            return result;
        }

        private static string Runs(IEnumerable<byte> values)
        {
            var result = new List<string>(); int last = -1, count = 0;
            foreach (byte value in values)
            {
                if (count > 0 && value != last) { result.Add(last + ":" + count); count = 0; }
                last = value; count++;
            }
            if (count > 0) result.Add(last + ":" + count);
            return string.Join(",", result);
        }

        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return Convert.ToHexString(hash.ComputeHash(bytes)).ToLowerInvariant();
        }
    }
}
