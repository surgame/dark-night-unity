using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Tools.TerrainRegression
{
    /// <summary>天然洞穴的多种子离线验收；只读取纯生成结果，导出计数，不创建游戏运行状态。</summary>
    internal static class CaveRegression
    {
        internal static void Run()
        {
            var rows = new List<object>();
            for (int seed = 0; seed < 100; seed++)
            {
                var input = new TerrainGenerationSettings
                { Seed = "CAVE-CHECK-" + seed, ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile };
                var map = TerrainGenerator.Generate(input);
                if (!map.CopyMaterials().SequenceEqual(TerrainGenerator.Generate(input).CopyMaterials()))
                    throw new Exception("Cave seed is not deterministic: " + seed);
                var reached = new HashSet<int> { 0 };
                for (int n = 0; n < map.Rooms.Count; n++) foreach (var e in map.Passages)
                {
                    if (reached.Contains(e.From)) reached.Add(e.To);
                    if (reached.Contains(e.To)) reached.Add(e.From);
                }
                if (reached.Count != map.Rooms.Count || map.Passages.Count < map.Rooms.Count || map.SoftRockCount == 0)
                    throw new Exception("Cave topology failed: " + seed);
                foreach (var room in map.Rooms) if (map.MaterialAt(room.X, room.Y) != 0)
                    throw new Exception("Cave room center blocked: " + seed + "/" + room.Kind);
                rows.Add(new { seed, rooms = map.Rooms.Count, passages = map.Passages.Count, soft = map.SoftRockCount,
                    open = map.Passages.Count(e => e.Kind == CavePassageKind.Open),
                    thin = map.Passages.Count(e => e.Kind == CavePassageKind.ThinRock),
                    deep = map.Passages.Count(e => e.Kind == CavePassageKind.DeepRock) });
            }
            Directory.CreateDirectory("artifacts/cave-workshop");
            File.WriteAllText("artifacts/cave-workshop/generation.json", JsonSerializer.Serialize(new
            { seeds = rows.Count, passed = true, scope = "determinism and hidden topology; not player traversal", rows },
                new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Cave exploration: 100 deterministic connected hidden graphs with buried passages passed.");
        }
    }
}
