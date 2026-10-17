using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Experiments.MiningExploration
{
    /// <summary>
    /// R1离线样板导出入口；只读取本地作者资产／Core，输出可追溯静态数据和逐洞室用途差分。
    /// 不启动Unity，不执行测试或改写正式图；浏览器消费结果，不能自行重抽地形或矿脉。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 2) throw new ArgumentException("需要项目根目录和本次演示输出目录。");
            string project = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
            Directory.CreateDirectory(output);
            var input = new LocalTerrainInput(project, "rift-expanded");
            var field = new MiningRouteField(input.Terrain, input.Map, 43);
            var generator = new MiningExplorationGenerator(field, input.Map, input.Seed + "|r1-v1");
            var baseline = generator.Generate();
            var installed = generator.Install(input.Terrain, baseline);
            var variants = new List<object>();
            bool allVariantDepthBudgetsMonotonic = true;
            int fulfilled = 0;
            for (int room = 1; room < input.Map.Rooms.Length; room++)
                foreach (var role in new[] { MiningRoomRole.Rich, MiningRoomRole.Quiet })
                {
                    var veins = generator.AssignRoom(baseline, room, role);
                    generator.Install(input.Terrain, veins);
                    int[] totals = Enumerable.Range(0, 3).Select(t => veins.Where(v => v.Tier == t).Sum(v => v.Capacity)).ToArray();
                    allVariantDepthBudgetsMonotonic &= totals[0] < totals[1] && totals[1] < totals[2];
                    var originalIds = new HashSet<string>(baseline.Select(v => v.Id));
                    var nextIds = new HashSet<string>(veins.Select(v => v.Id));
                    var changed = veins.Where(v => !originalIds.Contains(v.Id)).ToArray();
                    bool success = role != MiningRoomRole.Rich || changed.Length > 0;
                    if (success) fulfilled++;
                    variants.Add(new
                    {
                        roomId = input.Map.Rooms[room].Id,
                        role = role.ToString().ToLowerInvariant(), fulfilled = success,
                        added = changed.Select(Vein).ToArray(), removed = originalIds.Except(nextIds).ToArray(),
                        summary = Summary(veins)
                    });
                }
            var rooms = input.Map.Rooms.Select((room, index) => new
            {
                id = room.Id, name = room.Name, x = room.X + input.Map.OffsetX,
                floor = room.Floor + input.Map.OffsetY, radius = room.Radius, height = room.Height,
                distance = field.RoomDistance(index), baseline = room.Base
            }).ToArray();
            var original = input.Terrain.Deposits.Select(d => new
            {
                id = d.Id, kind = d.MineralKind, capacity = d.Capacity,
                cells = d.Cells.Select(c => new[] { c.U, -c.V, c.Capacity }).ToArray()
            }).ToArray();
            string corePath = typeof(PlayableTerrain).Assembly.Location;
            var snapshot = new
            {
                schema = 1, sampleVersion = "r1-v1", mapId = input.Map.Id, mapTitle = input.Map.Title,
                seed = input.Seed, width = 320, height = 192, cellPixels = 16,
                entry = new[] { field.Entry % 320, field.Entry / 320 }, offset = new[] { input.Map.OffsetX, input.Map.OffsetY },
                materials = Runs(input.Terrain.CopyMaterials().Select(v => (int)v)),
                shapes = Runs(input.Terrain.CopyShapes().Select(v => (int)v)), distances = Runs(field.CopyDistances()),
                thresholds = new[] { field.FirstThreshold, field.SecondThreshold, field.MaximumDistance },
                rooms, paths = input.Map.Paths, shafts = input.Map.Shafts, baselineDeposits = original,
                veins = baseline.Select(Vein).ToArray(), summary = Summary(baseline), variants,
                diagnostics = generator.Diagnostics,
                identity = new
                {
                    coreSha256 = Hash(corePath), configSha256 = Hash(input.ConfigPath), authorSha256 = Hash(input.MapPath),
                    finalMaterialSha256 = Hash(input.Terrain.CopyMaterials()), exportedUtc = DateTimeOffset.UtcNow,
                    miningFootprintSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(baseline.Select(Vein).ToArray()))
                }
            };
            string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { IncludeFields = true });
            File.WriteAllText(Path.Combine(output, "mining-exploration-snapshot.json"), json);
            var report = new
            {
                map = input.Map.Id, rooms = rooms.Length, veins = baseline.Length,
                roadmapSmall = baseline.Count(v => !v.Interior), interiorVeins = baseline.Count(v => v.Interior),
                mineralCells = baseline.Sum(v => v.Cells.Count), variants = variants.Count, fulfilled,
                foregroundUnchanged = installed.CopyMaterials().SequenceEqual(input.Terrain.CopyMaterials()),
                geometryUnchanged = installed.CopyShapes().SequenceEqual(input.Terrain.CopyShapes()),
                noOverlap = baseline.SelectMany(v => v.Cells).Distinct().Count() == baseline.Sum(v => v.Cells.Count),
                allHeadsExposed = baseline.All(v => field.Materials[v.Head] == 0),
                allBodiesInWall = baseline.All(v => v.Cells.Skip(1).All(field.IsWall)),
                allBodiesConnected = baseline.All(Connected),
                shallowBelowDeep = baseline.Where(v => v.Tier == 0).Sum(v => v.Capacity) < baseline.Where(v => v.Tier == 2).Sum(v => v.Capacity),
                allVariantDepthBudgetsMonotonic,
                dimensions = 320 * 192, bytes = System.Text.Encoding.UTF8.GetByteCount(json),
                summary = Summary(baseline), diagnostics = generator.Diagnostics, snapshot.identity
            };
            File.WriteAllText(Path.Combine(output, "mining-exploration-generation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(report));
            return 0;
        }

        private static object Vein(MiningVein vein) => new
        {
            id = vein.Id, head = vein.Head, room = vein.Room, distance = vein.Distance, tier = vein.Tier,
            interior = vein.Interior, kind = vein.Kind, capacity = vein.Capacity,
            cells = vein.Cells.ToArray()
        };

        private static object[] Summary(IEnumerable<MiningVein> veins)
        {
            return Enumerable.Range(0, 3).Select(tier =>
            {
                var items = veins.Where(v => v.Tier == tier).ToArray();
                return (object)new
                {
                    tier, roadside = items.Count(v => !v.Interior), interior = items.Count(v => v.Interior),
                    iron = items.Where(v => v.Kind == "iron").Sum(v => v.Capacity),
                    gold = items.Where(v => v.Kind == "gold").Sum(v => v.Capacity)
                };
            }).ToArray();
        }

        private static bool Connected(MiningVein vein)
        {
            var seen = new HashSet<int> { vein.Head };
            foreach (int cell in vein.Cells.Skip(1))
            {
                if (!MiningRouteField.Neighbors(cell).Any(seen.Contains)) return false;
                seen.Add(cell);
            }
            return true;
        }

        private static string Runs(IEnumerable<int> input)
        {
            var runs = new List<string>(); int previous = int.MinValue, count = 0;
            foreach (int value in input)
            {
                if (count > 0 && value != previous) { runs.Add(previous + ":" + count); count = 0; }
                previous = value; count++;
            }
            if (count > 0) runs.Add(previous + ":" + count);
            return string.Join(",", runs);
        }

        private static string Hash(string path) => Hash(File.ReadAllBytes(path));
        private static string Hash(byte[] bytes)
        {
            using (var hash = SHA256.Create()) return Convert.ToHexString(hash.ComputeHash(bytes)).ToLowerInvariant();
        }
    }
}
