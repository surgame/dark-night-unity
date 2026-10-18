using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.ViewData;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>局部地质事件的作者配置；冻结源岩、重力与不对称瓦砾清障区域。混合碎岩共用冻结落点，实体提交等待最后颗粒稳定，不定义伤害或全图通行保证。</summary>
    [Serializable]
    public sealed class GeologyEventConfig : IConfigData
    {
        public string Name => "地质事件样板";
        public string EventId = "support";
        public bool EnterRegion;
        public int SupportU = 50, SupportV = -41;
        public int RegionU = 68, RegionV = -41, RegionWidth = 4, RegionHeight = 4;
        public int SourceU = 52, SourceV = -36;
        public int LandingU = 52, LandingV = -41, Width = 3;
        public int SourceRows = 1, RubbleWidth = 3;
        public float Gravity = 320;
        public double WarningSeconds = 2, FragmentDelay = .045, OccupancyWaitSeconds = 8;
        public string Material = "slate";
        [NonSerialized] private RubbleCollapseViewData collapse;
        [NonSerialized] private CellCoord[] landings;

        public CellCoord Support => new CellCoord(SupportU, SupportV);
        public GridBounds Region => new GridBounds(RegionU, RegionV, RegionWidth, RegionHeight);
        public int RockCount => Width * SourceRows;
        public int RubbleCount => landings?.Length ?? RockCount;
        public RubbleCollapseViewData Collapse => collapse ?? throw new InvalidOperationException("碎岩配置尚未冻结。");
        public double FallSeconds => Collapse.Duration;
        public CellCoord Source(int offset) => new CellCoord(SourceU + offset % Width, SourceV + offset / Width);
        public CellCoord Landing(int offset)
        {
            if (landings != null) return landings[offset];
            return new CellCoord(LandingU + offset % RubbleWidth, LandingV + offset / RubbleWidth);
        }
        /// <summary>本切片使用已有整格实体包络；可见碎岩保留自然空隙，碰撞按16像素保守量化，实际边缘手感仍需场景验收。</summary>
        public TerrainCellShape PileShape(int index)
        {
            return TerrainCellShape.Full;
        }
        public FallingRockViewData Motion(int index)
        {
            var from = Source(index % RockCount); var to = Landing(index);
            return new FallingRockViewData(from.U * PlayableTerrain.CellPixels, PlayableTerrain.OriginY + from.V * PlayableTerrain.CellPixels,
                to.U * PlayableTerrain.CellPixels, PlayableTerrain.OriginY + to.V * PlayableTerrain.CellPixels,
                Gravity, index * FragmentDelay);
        }

        public GeologyEventConfig Freeze()
        {
            if (string.IsNullOrWhiteSpace(EventId) || EventId.Length > 64 || Material != "slate" ||
                Width < 1 || Width > 5 || SourceRows < 1 || RockCount > 24 || RubbleWidth < Width || RubbleWidth > 9 ||
                (RubbleWidth - Width) % 2 != 0 || !Finite(Gravity, 80, 1600) || !Finite(FragmentDelay, 0, .15) ||
                RockCount > RubbleWidth * 2 ||
                RegionWidth < 1 || RegionWidth > 16 || RegionHeight < 1 || RegionHeight > 16 ||
                SourceU != LandingU || SourceV <= LandingV || !Finite(WarningSeconds, .5, 10) ||
                !Finite(OccupancyWaitSeconds, .5, 30) ||
                SourceU < 0 || SourceU > TerrainGenerationSettings.Width - Width || SourceV <= -TerrainGenerationSettings.Height || SourceV > 1 - SourceRows ||
                LandingU < 0 || LandingU >= TerrainGenerationSettings.Width || LandingV <= -TerrainGenerationSettings.Height || LandingV > 0 ||
                SupportU < 0 || SupportU >= TerrainGenerationSettings.Width || SupportV <= -TerrainGenerationSettings.Height || SupportV > 0 ||
                RegionU < 0 || RegionU > TerrainGenerationSettings.Width - RegionWidth || RegionV <= -TerrainGenerationSettings.Height || RegionV > 1 - RegionHeight)
                throw new ArgumentException("地质事件身份、区域、瓦砾或时长配置无效。");
            var frozen = (GeologyEventConfig)MemberwiseClone();
            if (frozen.collapse == null)
            {
                var initial = new FallingRockViewData[RockCount];
                for (int i = 0; i < initial.Length; i++)
                {
                    var from = Source(i);
                    initial[i] = new FallingRockViewData(from.U * PlayableTerrain.CellPixels, PlayableTerrain.OriginY + from.V * PlayableTerrain.CellPixels,
                        LandingU * PlayableTerrain.CellPixels, PlayableTerrain.OriginY + LandingV * PlayableTerrain.CellPixels, Gravity, i * FragmentDelay);
                }
                float left = (LandingU + (Width - RubbleWidth) / 2) * PlayableTerrain.CellPixels - PlayableTerrain.CellPixels * .5f;
                frozen.collapse = RubbleMotionBake.Build(initial, PlayableTerrain.OriginY + (LandingV - .5f) * PlayableTerrain.CellPixels,
                    left, left + RubbleWidth * PlayableTerrain.CellPixels);
                frozen.landings = RubbleLandingLayout.Build(frozen.collapse, LandingV).Select(c => new CellCoord(c.U, c.V)).ToArray();
            }
            for (int i = 0; i < frozen.RubbleCount; i++)
                if (frozen.Landing(i).V >= SourceV)
                    throw new ArgumentException("瓦砾堆超出源岩下方的安全空间。");
            return frozen;
        }

        private static bool Finite(double value, double low, double high) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= low && value <= high;
    }
}
