namespace DarkNights.Core.ViewData
{
    /// <summary>脱离池状态的地质事件展示副本；阶段0待触发、1前兆、2落石、3等待占位、4瓦砾、5清障完成、6退休。只用于提示与动画，不结算地形或伤害。</summary>
    public readonly struct GeologyEventViewData
    {
        public readonly int Phase;
        public readonly double Clock;
        public readonly string Reason;
        public readonly uint SoilMask;
        public GeologyEventViewData(int phase, double clock, string reason, uint soilMask = 0)
        { Phase = phase; Clock = clock; Reason = reason ?? ""; SoilMask = soilMask; }
    }
}
