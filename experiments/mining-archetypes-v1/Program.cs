using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Experiments.MiningExploration;

namespace DarkNights.Experiments.MiningArchetypes
{
    /// <summary>
    /// 五种同源矿脉／伴巢候选的离线导出入口；共用同一真实露头、铁金预算和来源身份。
    /// 准入失败明确报错，不改正式地图或旧演示；一次导出冻结全部候选供浏览器只读比较。
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
                var terrain = new LocalTerrainInput(project, source.GetProperty("mapId").GetString()).Terrain;
                string originalHash = Hash(terrain.CopyMaterials());
                if (originalHash != source.GetProperty("identity").GetProperty("finalMaterialSha256").GetString())
                    throw new InvalidOperationException("真实地形已变化，不能沿用旧来源。");
                var site = new ArchetypeSite(source, terrain); var planner = new ArchetypeNestPlanner(site);
                var variants = new List<object>(); var checks = new List<object>(); var errors = new List<string>();
                foreach (string id in new[] { "branch", "core", "strata", "beads", "crescent" })
                {
                    try
                    {
                    var shape = new OreArchetypeShape(site, id);
                    if (shape.Cells.Keys.Any(i => i != site.Head && !site.Rock(i)))
                        throw new InvalidOperationException(id + "矿体与原空腔、保护格或其他矿脉冲突。");
                    int[] order = Order(shape, site.Head);
                    if (order.Length != shape.Cells.Count) throw new InvalidOperationException(id + "矿体不连续。");
                    var minerals = Allocate(shape, order);
                    var nest = planner.Plan(shape, order);
                    if (nest == null) throw new InvalidOperationException(id + "未找到中段伴巢空间。");
                    byte[] materials = site.Materials();
                    foreach (int i in nest.Pocket) materials[i] = 0;
                    byte[] shapes = TerrainShapeGeometry.Build(materials, site.Protection(), 320, 192);
                    int reveal = nest.Gates[0].RevealCount;
                    int remaining = minerals.Skip(reveal).Sum(c => c.Amount);
                    var deposits = new List<TerrainDepositBlueprint>();
                    foreach (string kind in new[] { "iron", "gold" })
                    {
                        var group = minerals.Where(c => c.Kind == kind).ToArray();
                        for (int at = 0; at < group.Length; at += 64)
                        {
                            var chunk = group.Skip(at).Take(64).ToArray();
                            var cells = chunk.Select(c => new TerrainMineralCell(c.Index % 320, -c.Index / 320, c.Amount)).ToArray();
                            deposits.Add(new TerrainDepositBlueprint("archetype-" + id + "-" + kind + "-" + at, "gallery",
                                site.Head % 320, site.Head / 320, kind == "gold" ? "rare" : "common", chunk.Sum(c => c.Amount), cells));
                        }
                    }
                    variants.Add(new { id, name = shape.Name, choice = shape.Choice, head = site.Head,
                        cells = minerals, order, nest, carve = nest.Pocket, shapes = Runs(shapes),
                        firstReveal = reveal, remainingCells = order.Length - reveal, remainingReserves = remaining,
                        budget = new { iron = 120, gold = 120 }, blueprintCount = deposits.Count });
                    checks.Add(new { id, cells = order.Length, connected = true, sourceKept = true,
                        pocketClosed = nest.Pocket.All(i => !ArchetypeSite.Neighbors(i).Any(site.Air)),
                        mineralGap = nest.Pocket.All(i => !ArchetypeSite.Neighbors(i).Any(shape.Cells.ContainsKey)),
                        floorKept = nest.Floor.All(i => site.Rock(i) && !nest.Pocket.Contains(i)),
                        offExact = site.Shapes().SequenceEqual(terrain.CopyShapes()) && Hash(site.Materials()) == originalHash,
                        earlyReveal = reveal <= Math.Floor(order.Length * .6), remaining, gates = nest.Gates.Count });
                    }
                    catch (Exception error) when (error is InvalidOperationException || error is ArgumentException) { errors.Add(id + ": " + error.Message); }
                }
                var sprites = Sprites(project);
                var result = new { schema = 1, version = "mining-archetypes-v1", mining = source,
                    replacedVein = site.ReplacedVein, head = site.Head, variants, sprites, rejections = errors,
                    inputSha256 = Hash(File.ReadAllBytes(input)), materialSha256 = originalHash,
                    viewBounds = new { x = 234, y = 135, width = 48, height = 30 } };
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(result, options);
                File.WriteAllBytes(Path.Combine(output, "mining-archetypes-snapshot.json"), bytes);
                var report = new { variants = variants.Count, checks, errors, sha256 = Hash(bytes), bytes = bytes.Length,
                    originalHash, inputSha256 = Hash(File.ReadAllBytes(input)), exportedUtc = DateTimeOffset.UtcNow };
                File.WriteAllText(Path.Combine(output, "generation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(JsonSerializer.Serialize(new { variants = variants.Count, checks, errors, bytes = bytes.Length }));
                if (errors.Count > 0) return 1;
            }
            return 0;
        }

        private static int[] Order(OreArchetypeShape shape, int head)
        {
            var result = new List<int>(); var seen = new HashSet<int> { head }; var queue = new Queue<int>(); queue.Enqueue(head);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(); result.Add(current);
                foreach (int next in ArchetypeSite.Neighbors(current).Where(shape.Cells.ContainsKey).OrderBy(i => i))
                    if (seen.Add(next)) queue.Enqueue(next);
            }
            return result.ToArray();
        }

        private static ArchetypeMineralCell[] Allocate(OreArchetypeShape shape, int[] order)
        {
            var amounts = new Dictionary<int, int>();
            foreach (string kind in new[] { "iron", "gold" })
            {
                int[] cells = order.Where(i => shape.Cells[i] == kind).ToArray();
                if (cells.Length == 0 || cells.Length > 120) throw new InvalidOperationException("资源分组超出对比预算。");
                for (int n = 0; n < cells.Length; n++) amounts[cells[n]] = 120 / cells.Length + (n < 120 % cells.Length ? 1 : 0);
            }
            return order.Select(i => new ArchetypeMineralCell(i, shape.Cells[i], amounts[i])).ToArray();
        }

        private static Dictionary<string, object> Sprites(string project)
        {
            var result = new Dictionary<string, object>();
            foreach (string name in new[] { "burrow/occupied", "egg-clutch/intact", "ambush-grub/alert" })
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
