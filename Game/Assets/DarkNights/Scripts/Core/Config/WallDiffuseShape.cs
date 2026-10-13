namespace DarkNights.Core.Config
{
    /// <summary>连续墙面返光的本地光形；沿墙模式保留法向扩散，柔光团按受光线段的径向软核积分，不拥有业务状态。</summary>
    public enum WallDiffuseShape
    {
        WallBand = 0,
        SoftBlob = 1
    }
}
