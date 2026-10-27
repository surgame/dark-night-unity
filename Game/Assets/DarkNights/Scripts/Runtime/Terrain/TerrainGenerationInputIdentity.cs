using System;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>
    /// 当前洞穴生成的有效输入投影；只记录实际消费的算法与参数，不含制作抽样策略、历史载体或航程表现。
    /// 预览缓存与新地图来源共用此合同；已有地图的旧摘要由历史读取器独立核对。
    /// </summary>
    public static class TerrainGenerationInputIdentity
    {
        public static string Json(TerrainGenerationSettings input)
        {
            return JsonV3(input, false) + (input?.DisableValleySculpt == true ? "|valley-sculpt:off" : "|valley-sculpt:on");
        }

        /// <summary>已保存v3地图的只读摘要投影；冻结原字段与近壁算法有效输入，不受新生成能力扩展影响。</summary>
        internal static string JsonV3(TerrainGenerationSettings input, bool historical = true)
        {
            if (input == null) return "<missing>";
            // 草稿允许暂时不合法；执行入口负责校验，身份计算本身不能阻止继续编辑或Undo。
            var s = input;
            var content = s.Minerals ?? new TerrainMineralSettings();
            var relief = s.Relief ?? new SurfaceReliefSettings();
            var minerals = content.Algorithm == TerrainMineralSettings.BaseDeposits ?
                new TerrainMineralSettings() : content.Algorithm == TerrainMineralSettings.WallVeins ?
                new TerrainMineralSettings { Algorithm = content.Algorithm, VeinCount = content.VeinCount,
                    Nests = content.Nests, Seed = historical ? "ORE-1" : content.Seed,
                    Revision = historical ? 0 : content.Revision } : content;
            return (historical ? "terrain-generation-v3|" : "terrain-generation-v4|") + JsonUtility.ToJson(new Input
            {
                Seed = s.Seed, Layout = s.Layout, SurfaceAlgorithm = string.IsNullOrEmpty(s.SurfaceAlgorithm) ?
                    (s.UsesLandform ? TerrainGenerationSettings.LandformSurface : TerrainGenerationSettings.NoiseSurface) : s.SurfaceAlgorithm,
                Surface = s.UsesLandform ? s.Surface : "", Amplitude = s.Amplitude,
                Landform = s.UsesLandform ? new LandformInput
                {
                    BaseHeight = relief.BaseHeightCells, Height = relief.HeightCells,
                    Wavelength = relief.WavelengthCells
                } : null,
                Structure = s.Layout == TerrainGenerationSettings.ConnectedRooms ? s.Structure : null,
                Minerals = minerals, Regions = s.RegionOverrides
            });
        }

        /// <summary>有效生成字段的固定序列化顺序；不承担可编辑配置或运行状态。</summary>
        [Serializable]
        private sealed class Input
        {
            public string Seed, Layout, SurfaceAlgorithm, Surface;
            public double Amplitude;
            public LandformInput Landform;
            public CaveLayoutSettings Structure;
            public TerrainMineralSettings Minerals;
            public CaveRegionOverride[] Regions;
        }

        /// <summary>地貌算法实际消费的高度参数；排除历史启用字段。</summary>
        [Serializable]
        private sealed class LandformInput
        {
            public double BaseHeight, Height, Wavelength;
        }
    }
}
