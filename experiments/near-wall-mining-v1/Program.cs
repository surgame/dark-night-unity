using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Experiments.MiningExploration;

namespace DarkNights.Experiments.NearWallMining
{
    /// <summary>
    /// 近壁盲盒的离线冻结导出入口；只读本地保存配置、真实Core地形和旧R1矿层。
    /// 同一实岩露头比较三种规模，使用正式矿床蓝图核对容量，不写游戏或重抽运行内容。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3) throw new ArgumentException("需要项目根、R1快照、独立输出目录。");
            string project = Path.GetFullPath(args[0]), input = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(output);
            using (var document = JsonDocument.Parse(File.ReadAllText(input)))
            {
                var source = document.RootElement;
                var local = new LocalTerrainInput(project, source.GetProperty("mapId").GetString());
                string materialHash = Hash(local.Terrain.CopyMaterials()); var identity = source.GetProperty("identity");
                if (materialHash != identity.GetProperty("finalMaterialSha256").GetString() ||
                    Hash(File.ReadAllBytes(local.ConfigPath)) != identity.GetProperty("configSha256").GetString() ||
                    Hash(File.ReadAllBytes(local.MapPath)) != identity.GetProperty("authorSha256").GetString() ||
                    Hash(File.ReadAllBytes(typeof(PlayableTerrain).Assembly.Location)) != identity.GetProperty("coreSha256").GetString())
                    throw new InvalidOperationException("真实输入身份已变化，不能沿用R1。");
                var site = new NearWallSite(source, local.Terrain); var variants = new List<object>();
                var checks = new List<object>(); var errors = new List<string>();
                foreach (string id in new[] { "shallow", "bulge", "double" })
                {
                    try
                    {
                        var ore = new CompactOreVariant(site, id);
                        var nest = id == "shallow" ? null : new CompactNest(site, ore);
                        var deposits = new List<TerrainDepositBlueprint>();
                        var groups = Components(ore.Cells);
                        foreach (int[] group in groups)
                        {
                            var cells = group.Select(i => new TerrainMineralCell(i % 320, -i / 320,
                                ore.Amounts[Array.IndexOf(ore.Cells, i)])).ToArray();
                            deposits.Add(new TerrainDepositBlueprint("near-wall-" + id + "-" + deposits.Count, "gallery",
                                site.Head % 320, site.Head / 320, "common", cells.Sum(c => c.Capacity), cells));
                        }
                        byte[] materials = site.Materials();
                        if (nest != null) foreach (int index in nest.Pocket) materials[index] = 0;
                        byte[] shapes = nest == null ? site.Shapes() : TerrainShapeGeometry.Build(materials, site.Protection(), 320, 192);
                        variants.Add(new { id, name = ore.Name, label = ore.Label, head = site.Head,
                            cells = ore.Cells.Select((index, n) => new { index, amount = ore.Amounts[n], kind = "iron" }).ToArray(),
                            excavation = ore.Excavation, bridges = ore.Bridges, components = ore.Components,
                            stock = ore.Stock, bodyDepth = ore.BodyDepth, nest, shapes = Runs(shapes), blueprintCount = deposits.Count });
                        checks.Add(new { id, cells = ore.Cells.Length, stock = ore.Stock, depth = ore.BodyDepth,
                            components = ore.Components, bridgeCells = ore.Bridges.Length, initialVisible = 1,
                            capacityConserved = ore.Amounts.Sum() == ore.Stock, mineralsInRock = ore.Cells.All(site.Rock),
                            prefixReachable = PrefixReachable(site, ore.Excavation), sourceKept = Hash(site.Materials()) == materialHash,
                            contactAction = nest?.ContactAction, sideWall = nest?.Gate.Length, remainingStock = nest?.RemainingStock,
                            pocketClosed = nest == null || nest.Pocket.All(i => !NearWallSite.Neighbors(i).Any(site.Air)),
                            floorKept = nest == null || nest.Floor.All(i => site.Rock(i) && !nest.Pocket.Contains(i)),
                            offExact = site.Shapes().SequenceEqual(local.Terrain.CopyShapes()), blueprintCount = deposits.Count });
                    }
                    catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
                    { errors.Add(id + ": " + error.Message); }
                }
                var result = new { schema = 1, version = "near-wall-mining-v1", mining = source,
                    replacedVein = site.ReplacedVein, head = site.Head, variants, sprites = Sprites(project), rejections = errors,
                    inputSha256 = Hash(File.ReadAllBytes(input)), materialSha256 = materialHash,
                    viewBounds = new { x = 264, y = 139, width = 14, height = 15 } };
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                File.WriteAllBytes(Path.Combine(output, "near-wall-mining-snapshot.json"), bytes);
                var report = new { variants = variants.Count, checks, errors, sha256 = Hash(bytes), bytes = bytes.Length,
                    materialHash, inputSha256 = Hash(File.ReadAllBytes(input)), exportedUtc = DateTimeOffset.UtcNow };
                File.WriteAllText(Path.Combine(output, "generation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(JsonSerializer.Serialize(new { variants = variants.Count, checks, errors, bytes = bytes.Length }));
                return errors.Count == 0 ? 0 : 1;
            }
        }

        private static bool PrefixReachable(NearWallSite site, int[] order)
        {
            var cleared = new HashSet<int>();
            foreach (int index in order)
            {
                if (!NearWallSite.Neighbors(index).Any(i => site.Air(i) || cleared.Contains(i))) return false;
                cleared.Add(index);
            }
            return true;
        }

        private static List<int[]> Components(int[] cells)
        {
            var remaining = new HashSet<int>(cells); var groups = new List<int[]>();
            while (remaining.Count > 0)
            {
                int first = remaining.First(); var group = new List<int>(); var queue = new Queue<int>();
                remaining.Remove(first); queue.Enqueue(first);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue(); group.Add(current);
                    foreach (int next in NearWallSite.Neighbors(current)) if (remaining.Remove(next)) queue.Enqueue(next);
                }
                groups.Add(group.ToArray());
            }
            return groups;
        }

        private static Dictionary<string, object> Sprites(string project)
        {
            var result = new Dictionary<string, object>();
            foreach (string name in new[] { "burrow/occupied", "egg-clutch/intact" })
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(project, "experiments/cl04-creature-art-v1/native/" + name + ".png"));
                bool eggs = name.StartsWith("egg", StringComparison.Ordinal);
                result[name] = new { data = "data:image/png;base64," + Convert.ToBase64String(bytes), width = eggs ? 24 : 32,
                    height = 24, pivot = new[] { eggs ? 12 : 16, 23 }, sha256 = Hash(bytes) };
            }
            return result;
        }

        private static string Runs(IEnumerable<byte> values)
        {
            var result = new List<string>(); int last = -1, count = 0;
            foreach (byte value in values)
            {
                if (count > 0 && last != value) { result.Add(last + ":" + count); count = 0; }
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
