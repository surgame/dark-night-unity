using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>无航程远征的兼容调用口；只转交完整星球生成器，不再维护独立泊位、通路或矿床改写。</summary>
    public static class ExpeditionTerrainGenerator
    {
        public const int DockLeft = 20, DockRight = 52, DockRow = 40;
        public const float ShipX = 568;
        public static PlayableTerrain Generate(string seed, string worldId, TerrainGenerationSettings template = null)
            => PlanetTerrainGenerator.GenerateCandidate(new PlanetDefinition("grey-pine", "灰松星"), seed, worldId,
                template ?? new TerrainGenerationSettings { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile });
    }
}
