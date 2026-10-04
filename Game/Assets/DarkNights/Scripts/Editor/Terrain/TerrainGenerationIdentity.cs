using System.Text;
using DarkNights.Runtime.Objects;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>地图重建身份；只包含天然洞穴、复现种子、泊位及权威步骤，航程与目录名称不触发几何重建。</summary>
    internal static class TerrainGenerationIdentity
    {
        internal static string For(ExpeditionFlowConfig config, PlanetPreset planet)
        {
            var text = new StringBuilder(JsonUtility.ToJson(config.CaveMap));
            if (planet == null) return text.Append("|no-planet").ToString();
            text.Append('|').Append(planet.DockColumn).Append('|').Append(planet.DockRow).Append('|').Append(planet.LandingWidth);
            foreach (var item in config.CopyModifiers())
                text.Append('|').Append(item?.GetType().FullName ?? "missing").Append(':').Append(item?.CanonicalSettings ?? "missing");
            return text.ToString();
        }
    }
}
