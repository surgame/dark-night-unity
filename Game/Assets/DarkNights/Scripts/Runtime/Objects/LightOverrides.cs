using System;
using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// Definition 作者的单灯参数覆盖；未启用项读取可选预设或内置默认值，已启用项保留作者值。
    /// 该对象只在制作入口编辑，Entry 冻结后交给 View，不成为运行开关或装备状态。
    /// </summary>
    [Serializable]
    public sealed class LightOverrides
    {
        public LightOverrideMask Mask;
        public bool EnvironmentEnabled = LightProfileDefaults.EnvironmentEnabled;
        public bool LocalFillEnabled = LightProfileDefaults.LocalFillEnabled;
        public bool Directional = LightProfileDefaults.Directional;
        public float Range = LightProfileDefaults.Range;
        public float Cone = LightProfileDefaults.Cone;
        public float ApertureWidth = LightProfileDefaults.ApertureWidth;
        public float Intensity = LightProfileDefaults.Intensity;
        public Color Color = new Color(LightProfileDefaults.Red, LightProfileDefaults.Green, LightProfileDefaults.Blue, 1);
        public bool SoftShadows = LightProfileDefaults.SoftShadows;
        public float Softness = LightProfileDefaults.Softness;
        public float ConeFeather = LightProfileDefaults.ConeFeather;
        public float NearRange = LightProfileDefaults.NearRange;
        public float NearIntensity = LightProfileDefaults.NearIntensity;
        public float FillRadius = LightProfileDefaults.FillRadius;
        public float FillIntensity = LightProfileDefaults.FillIntensity;
        public Color FillColor = new Color(LightProfileDefaults.Red, LightProfileDefaults.Green, LightProfileDefaults.Blue, 1);

        public bool Has(LightOverrideMask parameter) => (Mask & parameter) != 0;
    }
}
