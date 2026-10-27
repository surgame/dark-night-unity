using System;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>完整制作配置的规范身份；包含暂未选中算法的制作参数及抽样策略，用于草稿与外部冲突检查，不作生成缓存键。</summary>
    public static class TerrainAuthoringIdentity
    {
        public static string Json(TerrainGenerationSettings settings)
        {
            if (settings == null) return "<missing>";
            var s = settings;
            var value = s.Relief ?? new SurfaceReliefSettings();
            var relief = new SurfaceReliefSettings
            {
                BaseHeightCells = value.BaseHeightCells, HeightCells = value.HeightCells,
                WavelengthCells = value.WavelengthCells, DockTransitionCells = value.DockTransitionCells
            };
            return "terrain-authoring-v2|" + JsonUtility.ToJson(new Input
            {
                Seed = s.Seed, Surface = s.Surface, SurfaceAlgorithm = string.IsNullOrEmpty(s.SurfaceAlgorithm) ?
                    (s.UsesLandform ? TerrainGenerationSettings.LandformSurface : TerrainGenerationSettings.NoiseSurface) : s.SurfaceAlgorithm,
                Amplitude = s.Amplitude, Layout = s.Layout, Structure = s.Structure, Minerals = s.Minerals,
                Relief = relief, Regions = s.RegionOverrides, Selection = s.SeedSelection ?? new TerrainSeedSettings()
            }) + (s.DisableValleySculpt ? "|valley-sculpt:off" : "");
        }

        /// <summary>当前制作字段的固定投影；排除旧资源方案、矿脉密度与导入载体。</summary>
        [Serializable]
        private sealed class Input
        {
            public string Seed, Surface, SurfaceAlgorithm, Layout;
            public double Amplitude;
            public CaveLayoutSettings Structure;
            public TerrainMineralSettings Minerals;
            public SurfaceReliefSettings Relief;
            public CaveRegionOverride[] Regions;
            public TerrainSeedSettings Selection;
        }
    }
}
