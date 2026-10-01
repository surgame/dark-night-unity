namespace DarkNights.Core.ViewData
{
    /// <summary>采集意图的明确层语义；前景格与背景矿床分别验证，None 表示没有目标，不授予任何采集权限。</summary>
    public enum HeroMiningTargetKind : byte
    {
        None,
        Foreground,
        MineralDeposit
    }
}
