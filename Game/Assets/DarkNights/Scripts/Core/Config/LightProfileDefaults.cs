namespace DarkNights.Core.Config
{
    /// <summary>无预设时的内置光照制作基线；只有只读数值，预设和物体覆盖共用这些初始值，不拥有实例状态或引擎资源。</summary>
    public static class LightProfileDefaults
    {
        public const bool EnvironmentEnabled = true;
        public const bool LocalFillEnabled = true;
        public const bool Directional = true;
        public const float Range = 14;
        public const float Cone = 90;
        public const float ApertureWidth = .375f;
        public const float Intensity = 1.35f;
        public const float Red = 1;
        public const float Green = .88f;
        public const float Blue = .69f;
        public const bool SoftShadows = true;
        public const float Softness = .22f;
        public const float ConeFeather = .045f;
        public const float NearRange = 2.2f;
        public const float NearIntensity = 0;
        public const float FillRadius = 2.2f;
        public const float FillIntensity = .55f;

        public static LightEmissionRules Freeze() => new LightEmissionRules(Range, Cone, Intensity,
            NearRange, NearIntensity, Red, Green, Blue, ApertureWidth, SoftShadows, Softness, ConeFeather);
    }
}
