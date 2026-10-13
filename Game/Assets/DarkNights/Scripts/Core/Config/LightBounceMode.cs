namespace DarkNights.Core.Config
{
    /// <summary>私有光场的互斥返光方式；零值保留旧作者资产的单点反射，URP 后端不读取连续漫反射参数。</summary>
    public enum LightBounceMode
    {
        LegacyPoint = 0,
        Off = 1,
        Continuous = 2
    }
}
