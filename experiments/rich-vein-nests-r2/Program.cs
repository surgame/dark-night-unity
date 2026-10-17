using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Experiments.MiningExploration;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// R2离线生成和导出边界，消费保留的R1快照，单独输出可开关巢位步骤与已有像素素材。
    /// 不回写R1，不启动Unity；来源身份、关闭还原、空间互斥及冻结结果一致性随导出记录。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 3) throw new ArgumentException("需要项目根目录、R1快照及R2输出目录。");
            string project = Path.GetFullPath(args[0]), r1Path = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(output);
            string sourceHash = Hash(File.ReadAllBytes(r1Path));
            using (var document = JsonDocument.Parse(File.ReadAllText(r1Path)))
            {
                var source = document.RootElement;
                var terrain = new LocalTerrainInput(project, source.GetProperty("mapId").GetString()).Terrain;
                string geometryHash = Hash(terrain.CopyMaterials());
                if (geometryHash != source.GetProperty("identity").GetProperty("finalMaterialSha256").GetString())
                    throw new InvalidOperationException("当前正式Core地形与保留R1不同；拒绝沿用旧演示身份。");
                var enabled = new NestPlacementSettings(true); var disabled = new NestPlacementSettings(false);
                var pass = new RichVeinNestPass(); var scenes = new List<object>();
                var requests = new List<(string Room, string Role)> { ("deep-relay", "ordinary") };
                requests.AddRange(source.GetProperty("variants").EnumerateArray().Select(v =>
                    (v.GetProperty("roomId").GetString(), v.GetProperty("role").GetString())));
                bool offIdentity = true, mineralUntouched = true, originalAirUntouched = true;
                bool pocketsClosed = true, nonOverlap = true, floorsValid = true, stableRepeat = true, lateral = true;
                foreach (var request in requests)
                {
                    var input = new MiningInput(source, terrain.CopyProtection(), request.Room, request.Role);
                    byte[] before = input.CopyMaterials();
                    var off = pass.Apply(input, disabled); var on = pass.Apply(input, enabled);
                    offIdentity &= off.Placements.Count == 0 && off.CarveCells.Count == 0 &&
                        off.Shapes.SequenceEqual(input.CopyShapes()) && before.SequenceEqual(input.CopyMaterials());
                    mineralUntouched &= on.CarveCells.All(i => !input.Mineral(i));
                    originalAirUntouched &= on.CarveCells.All(i => input.Carvable(i));
                    pocketsClosed &= on.CarveCells.All(i => !NestPocketPlanner.Neighbors(i).Any(input.NaturalAir));
                    nonOverlap &= on.Placements.SelectMany(p => p.Pocket.Concat(p.Gate)).Distinct().Count() ==
                        on.Placements.Sum(p => p.Pocket.Count + p.Gate.Count);
                    floorsValid &= on.Placements.All(p => p.Floor.All(input.Carvable));
                    lateral &= on.Placements.All(p =>
                    {
                        var vein = input.Veins.Single(v => v.Id == p.VeinId);
                        return new VeinSideProfile(vein).Beside(p.Contact, p.Center) &&
                            p.RevealCount <= Math.Floor(vein.Cells.Count * .6) && p.RemainingReserves > 0;
                    });
                    if (request.Role == "ordinary")
                    {
                        var repeat = pass.Apply(input, enabled);
                        stableRepeat &= JsonSerializer.Serialize(on).Equals(JsonSerializer.Serialize(repeat), StringComparison.Ordinal);
                    }
                    scenes.Add(new
                    {
                        roomId = request.Room, role = request.Role,
                        placements = on.Placements.Select(Placement).ToArray(),
                        decisions = on.Decisions.Select(d => new { veinId = d.VeinId, tier = d.Tier,
                            probability = d.Probability, roll = d.Roll, reason = d.Reason }).ToArray(),
                        carve = on.CarveCells, shapes = Runs(on.Shapes), stats = Stats(on)
                    });
                }
                bool preserved = Hash(File.ReadAllBytes(r1Path)) == sourceHash;
                if (!offIdentity || !mineralUntouched || !originalAirUntouched || !pocketsClosed ||
                    !nonOverlap || !floorsValid || !stableRepeat || !preserved || !lateral)
                    throw new InvalidOperationException("R2输出未满足关闭还原、矿格／通路保全或模板静态约束。");
                var sprites = Sprites(project);
                var result = new
                {
                    schema = 2, version = "rich-nest-side-v2", mining = source,
                    inputSha256 = sourceHash, geometrySha256 = geometryHash, scenes, sprites,
                    parameters = new { probabilities = new[] { .6, .75, .9 }, maximumNests = enabled.MaximumNests,
                        minimumReserves = enabled.MinimumReserves, minimumWallCells = enabled.MinimumWallCells,
                        spacing = enabled.MinimumSpacing, spawnQuota = enabled.SpawnQuota,
                        pocketSizes = new[] { new[] { 5, 3 }, new[] { 3, 5 } }, maximumRevealFraction = .6 },
                    disabled = new { placements = Array.Empty<object>(), carve = Array.Empty<int>(), shapes = source.GetProperty("shapes").GetString() }
                };
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(result);
                File.WriteAllBytes(Path.Combine(output, "rich-vein-nests-snapshot.json"), bytes);
                var report = new
                {
                    sourceHash, geometryHash, preserved, offIdentity, mineralUntouched, originalAirUntouched,
                    pocketsClosed, nonOverlap, floorsValid, stableRepeat, lateral, scenes = scenes.Count,
                    spriteCount = sprites.Count, bytes = bytes.Length, snapshotSha256 = Hash(bytes),
                    defaultScene = scenes[0], exportedUtc = DateTimeOffset.UtcNow
                };
                File.WriteAllText(Path.Combine(output, "rich-vein-nests-generation.json"),
                    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(JsonSerializer.Serialize(new { scenes = scenes.Count, preserved, offIdentity, mineralUntouched,
                    originalAirUntouched, pocketsClosed, nonOverlap, floorsValid, stableRepeat, lateral, bytes = bytes.Length,
                    defaultStats = Stats(pass.Apply(new MiningInput(source, terrain.CopyProtection(), "deep-relay", "ordinary"), enabled)) }));
            }
            return 0;
        }

        private static object Placement(NestPlacement value) => new
        {
            id = value.Id, veinId = value.VeinId, anchor = value.Anchor, eggAnchor = value.EggAnchor,
            tier = value.Tier, pocket = value.Pocket, gate = value.Gate, floor = value.Floor,
            contact = value.Contact, center = value.Center, revealCount = value.RevealCount,
            remainingCells = value.RemainingCells, remainingReserves = value.RemainingReserves,
            spawnQuota = value.SpawnQuota, trigger = "uncover-eggs-proposal"
        };

        private static object[] Stats(NestPassResult result) => Enumerable.Range(0, 3).Select(tier =>
        {
            var values = result.Decisions.Where(d => d.Tier == tier).ToArray();
            return (object)new { tier, eligible = values.Length, rolled = values.Count(d => d.Roll < d.Probability),
                placed = result.Placements.Count(p => p.Tier == tier), probability = new NestPlacementSettings(true).Probability(tier) };
        }).ToArray();

        private static Dictionary<string, object> Sprites(string project)
        {
            var result = new Dictionary<string, object>();
            foreach (string relative in new[] { "burrow/occupied", "burrow/empty", "egg-clutch/intact", "egg-clutch/pulse",
                "egg-clutch/cracked", "egg-clutch/empty", "ambush-grub/idle_a", "ambush-grub/alert", "ambush-grub/crawl_a" })
            {
                string path = Path.Combine(project, "experiments/cl04-creature-art-v1/native/" + relative + ".png");
                byte[] bytes = File.ReadAllBytes(path);
                bool eggs = relative.StartsWith("egg", StringComparison.Ordinal);
                result[relative] = new { data = "data:image/png;base64," + Convert.ToBase64String(bytes),
                    width = eggs ? 24 : 32, height = 24, pivot = new[] { eggs ? 12 : 16, 23 }, sha256 = Hash(bytes) };
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
