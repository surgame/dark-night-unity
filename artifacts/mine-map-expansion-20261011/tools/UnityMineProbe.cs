using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Logic.Terrain;

/// <summary>正式 Unity Core 生成代码的离线探针；检查五图候选，不模拟角色、不证明 Editor 或 Player 画面。</summary>
internal static class UnityMineProbe
{
    private static void Main(string[] args)
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        foreach (string file in Directory.GetFiles(args[0], "*.minemap").OrderBy(x => x))
        {
            var map = JsonSerializer.Deserialize<MineMapDefinition>(File.ReadAllText(file), options).CopyValidated();
            var settings = new TerrainGenerationSettings { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile, MineMap = map };
            var source = TerrainGenerator.GenerateCave(settings, "UNITY-MINE-20261011");
            var result = PlanetTerrainGenerator.GenerateCandidate(new PlanetDefinition("grey-pine", "灰松星", seed: "UNITY-MINE-20261011"),
                "UNITY-MINE-20261011", "00000000000000000000000000000001", settings);
            byte[] cells = result.CopyMaterials();
            if (!cells.SequenceEqual(result.Background.CopyMaterials())) throw new Exception("Background differs");
            int maxX = 0, maxY = 0;
            for (int y = 110; y < 188; y++) for (int x = 40; x < 304; x++)
                if (cells[y * 320 + x] == 0) { maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            if (maxX < 280 || maxY < 170) throw new Exception("Expansion missing");
            Console.WriteLine(JsonSerializer.Serialize(new { id = map.Id, rooms = result.Rooms.Count,
                deposits = result.Deposits.Count, maxX, maxY, soft = source.SoftRockCount,
                shafts = map.Shafts.Length, baselineOres = map.BaseOres.Length }));
        }
    }
}
