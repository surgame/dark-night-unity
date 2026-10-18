using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>单个塌方事件脱离池后的保存值；阶段和时钟由所属GeologyEventBehaviour唯一写入，地图格状态另随同一快照冻结。</summary>
    public sealed class TerrainGeologyProgress
    {
        public string SiteId { get; }
        public int Phase { get; }
        public double Clock { get; }
        public string Reason { get; }

        public TerrainGeologyProgress(string siteId, int phase, double clock, string reason)
        {
            if (string.IsNullOrWhiteSpace(siteId) || siteId.Length > 64 || phase < 0 || phase > 6 ||
                double.IsNaN(clock) || double.IsInfinity(clock) || clock < 0 || clock > 1000000 ||
                reason == null || reason.Length > 256 || phase != 3 && phase != 6 && reason.Length != 0 ||
                (phase == 3 || phase == 6) && string.IsNullOrWhiteSpace(reason) || (phase == 0 || phase >= 4) && clock != 0)
                throw new ArgumentException("塌方保存阶段、时钟或退休原因无效。");
            SiteId = siteId; Phase = phase; Clock = clock; Reason = reason;
        }
    }
}
