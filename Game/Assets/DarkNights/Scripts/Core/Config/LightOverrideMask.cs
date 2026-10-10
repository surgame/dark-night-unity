using System;

namespace DarkNights.Core.Config
{
    /// <summary>单灯制作参数的覆盖位；只描述 Definition 相对光照预设的差异，不包含实例开关、角色方向或玩法状态。</summary>
    [Flags]
    public enum LightOverrideMask
    {
        None = 0,
        EnvironmentEnabled = 1 << 0,
        LocalFillEnabled = 1 << 1,
        Directional = 1 << 2,
        Range = 1 << 3,
        Cone = 1 << 4,
        ApertureWidth = 1 << 5,
        Intensity = 1 << 6,
        Color = 1 << 7,
        SoftShadows = 1 << 8,
        Softness = 1 << 9,
        ConeFeather = 1 << 10,
        NearRange = 1 << 11,
        NearIntensity = 1 << 12,
        FillRadius = 1 << 13,
        FillIntensity = 1 << 14,
        FillColor = 1 << 15
    }
}
