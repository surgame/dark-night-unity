using System.Text;
using DarkNights.Runtime.Objects;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>地图重建身份；包含有效规则、种子、版本与地质点，船位和航程表现不触发几何重建。</summary>
    internal static class TerrainGenerationIdentity
    {
        internal static string For(ExpeditionFlowConfig config, PlanetPreset planet)
        {
            var text = new StringBuilder(DarkNights.Runtime.Terrain.TerrainGenerationInputIdentity.Json(config.CaveMap));
            text.Append("|planet-generation-v").Append(DarkNights.Core.Logic.Terrain.PlanetTerrainGenerator.Version);
            text.Append("|geology-point-v1|").Append(config.Geology?.Identity ?? "missing");
            if (planet == null) return text.Append("|no-planet").ToString();
            return text.ToString();
        }
    }
}
