using System;
using System.Globalization;

namespace DarkNights.Core.Config
{
    /// <summary>通用环境光源的冻结视觉参数，距离以地图格计；不拥有装备、开关或渲染资源。</summary>
    public sealed class LightEmissionRules
    {
        public float Range { get; }
        public float Cone { get; }
        public float Intensity { get; }
        public float NearRange { get; }
        public float NearIntensity { get; }
        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public float ApertureWidth { get; }

        public LightEmissionRules(float range, float cone, float intensity, float nearRange,
            float nearIntensity, float red, float green, float blue, float apertureWidth = 0)
        {
            if (!Valid(range, 2, 24) || !Valid(cone, 20, 150) || !Valid(intensity, 0, 4) ||
                !Valid(nearRange, .25f, 5) || !Valid(nearIntensity, 0, 2) ||
                !Valid(red, 0, 1) || !Valid(green, 0, 1) || !Valid(blue, 0, 1) || !Valid(apertureWidth, 0, 2))
                throw new ArgumentException("手电能力包含非法参数。");
            Range = range; Cone = cone; Intensity = intensity;
            NearRange = nearRange; NearIntensity = nearIntensity;
            Red = red; Green = green; Blue = blue;
            ApertureWidth = apertureWidth;
        }

        public string Fingerprint => string.Join(",", Array.ConvertAll(new[]
        { Range, Cone, Intensity, NearRange, NearIntensity, Red, Green, Blue, ApertureWidth },
            value => value.ToString("R", CultureInfo.InvariantCulture)));

        private static bool Valid(float value, float minimum, float maximum) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
    }
}
