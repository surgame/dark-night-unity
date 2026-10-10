using System;
using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>一个物体光效的冻结制作参数；只保存模板与不可变数值，不持有可写配置、角色或池状态，直到配置更新才替换。</summary>
    public sealed class LightProfileSnapshot
    {
        public LightEffect Template { get; }
        public bool EnvironmentEnabled { get; }
        public bool LocalFillEnabled { get; }
        public bool Directional { get; }
        public LightEmissionRules Rules { get; }
        public float FillRadius { get; }
        public float FillIntensity { get; }
        public Color FillColor { get; }

        public LightProfileSnapshot(LightEffect template, bool environmentEnabled, bool localFillEnabled,
            bool directional, LightEmissionRules rules, float fillRadius, float fillIntensity, Color fillColor)
        {
            if (template == null || rules == null || !float.IsFinite(fillRadius) || fillRadius < .25f || fillRadius > 5 ||
                !float.IsFinite(fillIntensity) || fillIntensity < 0 || fillIntensity > 2 ||
                !ValidColor(fillColor)) throw new ArgumentException("单灯配置包含无效的模板或补光参数。");
            Template = template; EnvironmentEnabled = environmentEnabled; LocalFillEnabled = localFillEnabled;
            Directional = directional; Rules = rules; FillRadius = fillRadius; FillIntensity = fillIntensity; FillColor = fillColor;
        }

        private static bool ValidColor(Color color) => Valid(color.r) && Valid(color.g) && Valid(color.b) && Valid(color.a);
        private static bool Valid(float value) => float.IsFinite(value) && value >= 0 && value <= 1;

        public static LightProfileSnapshot FromDefaults(LightEffect template) => new LightProfileSnapshot(template,
            LightProfileDefaults.EnvironmentEnabled, LightProfileDefaults.LocalFillEnabled, LightProfileDefaults.Directional,
            LightProfileDefaults.Freeze(), LightProfileDefaults.FillRadius, LightProfileDefaults.FillIntensity,
            new Color(LightProfileDefaults.Red, LightProfileDefaults.Green, LightProfileDefaults.Blue, 1));
    }
}
