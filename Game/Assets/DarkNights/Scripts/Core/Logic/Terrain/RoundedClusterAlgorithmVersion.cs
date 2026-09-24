namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>固定圆簇算法的视觉身份版本；LegacyV1 保留既有绑定，LocalV2 只在独立对照样式中启用。</summary>
    public enum RoundedClusterAlgorithmVersion : byte
    {
        LegacyV1 = 0,
        LocalV2 = 1
    }
}
