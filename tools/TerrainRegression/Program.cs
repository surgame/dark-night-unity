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
        Surface = v.GetProperty("surface").GetString(), OrganicCaves = v.GetProperty("caves").GetString() == "organic" };
    var map = TerrainGenerator.Generate(input);
    string actual = Convert.ToHexString(SHA256.HashData(map.CopyMaterials())).ToLowerInvariant();
    if (actual != v.GetProperty("sha256").GetString()) throw new Exception(input.Seed + "/" + input.Surface + "/" + input.OrganicCaves + ": " + actual);
    count++;
}
Console.WriteLine("H5 generation vectors passed: " + count);
