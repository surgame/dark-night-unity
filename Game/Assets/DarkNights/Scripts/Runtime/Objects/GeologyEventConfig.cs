using System;
using AnyRules.Next;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>一个地质样板的作者配置；坐标为前景逻辑格，运行前复制冻结。落点限定为至多八格单层瓦砾，不定义伤害、经济或全图通行保证。</summary>
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
        public double WarningSeconds = 2, FallSeconds = .6, OccupancyWaitSeconds = 8;
        public string Material = "slate";

        public CellCoord Support => new CellCoord(SupportU, SupportV);
        public GridBounds Region => new GridBounds(RegionU, RegionV, RegionWidth, RegionHeight);
        public CellCoord Source(int offset) => new CellCoord(SourceU + offset, SourceV);
        public CellCoord Landing(int offset) => new CellCoord(LandingU + offset, LandingV);

        public GeologyEventConfig Freeze()
        {
            if (string.IsNullOrWhiteSpace(EventId) || EventId.Length > 64 || Material != "slate" ||
                Width < 1 || Width > 8 || RegionWidth < 1 || RegionWidth > 16 || RegionHeight < 1 || RegionHeight > 16 ||
                SourceU != LandingU || SourceV <= LandingV || !Finite(WarningSeconds, .5, 10) || !Finite(FallSeconds, .1, 3) ||
                !Finite(OccupancyWaitSeconds, .5, 30))
                throw new ArgumentException("地质事件身份、区域、瓦砾或时长配置无效。");
            return (GeologyEventConfig)MemberwiseClone();
        }

        private static bool Finite(double value, double low, double high) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= low && value <= high;
    }
}
