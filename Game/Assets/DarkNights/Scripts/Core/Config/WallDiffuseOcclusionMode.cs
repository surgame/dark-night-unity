namespace DarkNights.Core.Config
{
    /// <summary>
    /// 单灯连续返光的遮挡及羽化规则；只作用于本地返光，不改变直射或权威状态。
    /// 零值保留投影阴影；墙体遮罩放开空气光路，值2对完整空气返光做几何约束羽化。
    /// OutwardFeather保留既有序列化身份，宽度0走原始切边；墙体表层沿用原接收合同。
    /// </summary>
    public enum WallDiffuseOcclusionMode
    {
        ProjectedShadow = 0,
        WallMask = 1,
        OutwardFeather = 2
    }
}
