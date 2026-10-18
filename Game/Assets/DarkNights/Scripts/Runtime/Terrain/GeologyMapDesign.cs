using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>现有完整地图候选的塌方静态装配；仅附加经过空间校验的清单，关闭时返回原输入，不改变矿脉、巢穴、地形或保护结构。</summary>
    public static class GeologyMapDesign
    {
        public static PlayableTerrain Attach(PlayableTerrain source, GeologyDesignPoint design)
        {
            if (design == null || !design.Enabled) return source;
            var site = design.Freeze();
            string reason = GeologySiteValidation.Check(source, site);
            if (reason.Length != 0) throw new InvalidOperationException(reason);
            return new PlayableTerrain(source.WorldId, source.Seed, source.CopyMaterials(), source.CopyProtection(), source.CopySoftRock(),
                source.Rooms.ToArray(), source.Deposits.ToArray(), source.CopyShapes(), source.Expedition, source.Background,
                source.Damage.ToArray(), source.MiningRulesFingerprint, source.Minerals, source.Nests.ToArray(), source.NestProgress.ToArray(),
                new[] { site });
        }
    }
}
