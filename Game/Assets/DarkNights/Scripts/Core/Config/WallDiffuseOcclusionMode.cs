namespace DarkNights.Core.Config
{
    /// <summary>
    /// 单灯连续返光的遮挡规则；仅控制返光合成中的可见性，不改变直射、径向光形或权威状态。
    /// 零值保留既有投影阴影；墙体遮罩放开空气光路，外扩羽化仅放宽墙段端部的阴影切边。
    /// </summary>
    public enum WallDiffuseOcclusionMode
    {
        ProjectedShadow = 0,
        WallMask = 1,
        OutwardFeather = 2
    }
}
