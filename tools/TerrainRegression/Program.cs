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
