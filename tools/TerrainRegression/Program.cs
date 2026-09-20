using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

var vectors = JsonDocument.Parse(File.ReadAllText("tools/terrain-reference/generation-vectors.json"));
int count = 0;
foreach (var v in vectors.RootElement.GetProperty("vectors").EnumerateArray())
{
    var input = new TerrainGenerationSettings { Seed = v.GetProperty("seed").GetString(),
        Surface = v.GetProperty("surface").GetString(), OrganicCaves = v.GetProperty("caves").GetString() == "organic" }.AsReferenceProfile();
    var map = TerrainGenerator.Generate(input);
    string actual = Convert.ToHexString(SHA256.HashData(map.CopyMaterials())).ToLowerInvariant();
    if (actual != v.GetProperty("sha256").GetString()) throw new Exception(input.Seed + "/" + input.Surface + "/" + input.OrganicCaves + ": " + actual);
    count++;
}
Console.WriteLine("H5 generation vectors passed: " + count);

var gameplay = TerrainGenerator.Generate(new TerrainGenerationSettings { Seed = "MAP-PLAN-M1", Surface = "rolling", OrganicCaves = true });
int scattered = 0;
for (int y = 0; y < gameplay.Height; y++) for (int x = 0; x < gameplay.Width; x++)
    if (gameplay.MaterialAt(x, y) >= 4 && gameplay.MaterialAt(x, y) <= 6) scattered++;
if (gameplay.Deposits.Count != 11 || gameplay.SoftRockCount == 0 || scattered <= 0 || scattered >= 220)
    throw new Exception("M1 gameplay resource distribution failed: scattered=" + scattered + ", deposits=" + gameplay.Deposits.Count + ", softRock=" + gameplay.SoftRockCount);
Console.WriteLine("M1 gameplay distribution passed: scattered=" + scattered + ", deposits=" + gameplay.Deposits.Count + ", softRock=" + gameplay.SoftRockCount);

for (int seed = 0; seed < 100; seed++)
{
    var map = TerrainGenerator.Generate(new TerrainGenerationSettings { Seed = "MAP-PLAN-M1-" + seed, Surface = "rolling", OrganicCaves = true });
    if (map.Deposits.Count != 11) throw new Exception("deposit count changed for seed " + seed);
    for (int i = 0; i < map.Deposits.Count; i++)
    {
        var deposit = map.Deposits[i];
        if (map.MaterialAt(deposit.X, deposit.Y) != 0 || !Supported(map, deposit.X, deposit.Y) ||
            (Math.Abs(deposit.X - 94) <= 7 && deposit.Y < map.Surface[deposit.X] + 18) ||
            (Math.Abs(deposit.X - 291) <= 7 && deposit.Y < map.Surface[deposit.X] + 18))
            throw new Exception("invalid deposit candidate for seed " + seed + ": " + deposit.Id);
        for (int j = 0; j < i; j++)
        {
            int dx = deposit.X - map.Deposits[j].X, dy = deposit.Y - map.Deposits[j].Y;
            if (dx * dx + dy * dy < 25) throw new Exception("overlapping deposits for seed " + seed);
        }
    }
}
Console.WriteLine("M1 deposit candidate batch passed: seeds=100");

static bool Supported(TerrainBlueprint map, int x, int y)
{
    return Solid(map, x, y + 1) || Solid(map, x - 1, y) || Solid(map, x + 1, y);
}

static bool Solid(TerrainBlueprint map, int x, int y)
{
    return x >= 0 && x < map.Width && y >= 0 && y < map.Height && map.MaterialAt(x, y) > 0 && map.MaterialAt(x, y) != 8;
}

DarkNights.Tools.TerrainRegression.CaveRegression.Run();
