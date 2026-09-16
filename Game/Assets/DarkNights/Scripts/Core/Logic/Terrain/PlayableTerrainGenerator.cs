using System;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>为随机灰松谷生成洞穴与地表，同时预留原营地的完整平地；不改变冻结生成器及原场景布局。</summary>
    public static class PlayableTerrainGenerator
    {
        public static PlayableTerrain Generate(string seed, string worldId)
        {
            var source = TerrainGenerator.Generate(new TerrainGenerationSettings { Seed = seed, Surface = "rolling", OrganicCaves = true });
            byte[] cells = source.CopyMaterials();
            var protection = new bool[cells.Length];
            for (int y = 0; y < source.Height; y++) for (int x = 0; x < source.Width; x++)
            {
                int index = y * source.Width + x;
                protection[index] = source.IsProtected(x, y) && cells[index] != 0;
                if (x < PlayableTerrain.CampColumns)
                {
                    if (y < PlayableTerrain.CampRow) cells[index] = 0;
                    else if (y < PlayableTerrain.CampRow + 4) cells[index] = 1;
                    protection[index] = y >= PlayableTerrain.CampRow && y < PlayableTerrain.CampRow + 4;
                }
                if (y == source.Height - 1) { cells[index] = 8; protection[index] = true; }
            }
            return new PlayableTerrain(worldId, seed, cells, protection);
        }

        public static LevelLayout Layout(LevelLayout original) => new LevelLayout(
            TerrainGenerationSettings.Width * PlayableTerrain.CellPixels, original.GroundY, original.BuildMinX,
            original.BuildMaxX, original.SpawnX, original.CameraX, original.Buildings, original.Worksites, original.Actors, Array.Empty<PlatformDefinition>(), randomTerrain: true);
    }
}
