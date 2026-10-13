using System;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;

namespace DarkNights.Entry
{
    /// <summary>预设与 Definition 制作覆盖之间的有限适配；仅合成冻结表现参数，Runtime 和 View 无反向依赖，不修改资产。</summary>
    public static class LightProfileResolver
    {
        public static LightProfileSnapshot Resolve(LightProfile preset, LightOverrides value, LightEffect defaultTemplate = null)
        {
            if (value == null) throw new ArgumentException("物体光照覆盖配置缺失。");
            var basis = preset != null ? preset.Freeze() : LightProfileSnapshot.FromDefaults(defaultTemplate);
            return Apply(basis, value);
        }

        public static LightProfileSnapshot Apply(LightProfileSnapshot basis, LightOverrides value)
        {
            if (basis == null || value == null) throw new ArgumentException("光照基线或覆盖缺失。");
            var rules = new LightEmissionRules(
                value.Has(LightOverrideMask.Range) ? value.Range : basis.Rules.Range,
                value.Has(LightOverrideMask.Cone) ? value.Cone : basis.Rules.Cone,
                value.Has(LightOverrideMask.Intensity) ? value.Intensity : basis.Rules.Intensity,
                value.Has(LightOverrideMask.NearRange) ? value.NearRange : basis.Rules.NearRange,
                value.Has(LightOverrideMask.NearIntensity) ? value.NearIntensity : basis.Rules.NearIntensity,
                value.Has(LightOverrideMask.Color) ? value.Color.r : basis.Rules.Red,
                value.Has(LightOverrideMask.Color) ? value.Color.g : basis.Rules.Green,
                value.Has(LightOverrideMask.Color) ? value.Color.b : basis.Rules.Blue,
                value.Has(LightOverrideMask.ApertureWidth) ? value.ApertureWidth : basis.Rules.ApertureWidth,
                value.Has(LightOverrideMask.SoftShadows) ? value.SoftShadows : basis.Rules.SoftShadows,
                value.Has(LightOverrideMask.Softness) ? value.Softness : basis.Rules.Softness,
                value.Has(LightOverrideMask.ConeFeather) ? value.ConeFeather : basis.Rules.ConeFeather,
                new LightBounceRules(
                    value.Has(LightOverrideMask.BounceMode) ? value.BounceMode : basis.Rules.Bounce.BounceMode,
                    value.Has(LightOverrideMask.Bounce) ? value.Bounce : basis.Rules.Bounce.Bounce,
                    value.Has(LightOverrideMask.DiffuseShape) ? value.DiffuseShape : basis.Rules.Bounce.DiffuseShape,
                    value.Has(LightOverrideMask.DiffuseMatchIncident) ? value.DiffuseMatchIncident : basis.Rules.Bounce.DiffuseMatchIncident,
                    value.Has(LightOverrideMask.DiffuseCornerRadius) ? value.DiffuseCornerRadius : basis.Rules.Bounce.DiffuseCornerRadius,
                    value.Has(LightOverrideMask.DiffuseGain) ? value.DiffuseGain : basis.Rules.Bounce.DiffuseGain,
                    value.Has(LightOverrideMask.DiffuseNearDistance) ? value.DiffuseNearDistance : basis.Rules.Bounce.DiffuseNearDistance,
                    value.Has(LightOverrideMask.DiffuseFarDistance) ? value.DiffuseFarDistance : basis.Rules.Bounce.DiffuseFarDistance,
                    value.Has(LightOverrideMask.DiffuseMaxReach) ? value.DiffuseMaxReach : basis.Rules.Bounce.DiffuseMaxReach,
                    value.Has(LightOverrideMask.DiffuseLateralSpread) ? value.DiffuseLateralSpread : basis.Rules.Bounce.DiffuseLateralSpread,
                    value.Has(LightOverrideMask.DiffuseVisibilityProbes) ? value.DiffuseVisibilityProbes : basis.Rules.Bounce.DiffuseVisibilityProbes));
            return new LightProfileSnapshot(basis.Template,
                value.Has(LightOverrideMask.EnvironmentEnabled) ? value.EnvironmentEnabled : basis.EnvironmentEnabled,
                value.Has(LightOverrideMask.LocalFillEnabled) ? value.LocalFillEnabled : basis.LocalFillEnabled,
                value.Has(LightOverrideMask.Directional) ? value.Directional : basis.Directional, rules,
                value.Has(LightOverrideMask.FillRadius) ? value.FillRadius : basis.FillRadius,
                value.Has(LightOverrideMask.FillIntensity) ? value.FillIntensity : basis.FillIntensity,
                value.Has(LightOverrideMask.FillColor) ? value.FillColor : basis.FillColor);
        }

        public static LightOverrides Values(LightProfileSnapshot value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var rules = value.Rules;
            return new LightOverrides
            {
                BounceMode = rules.Bounce.BounceMode,
                Bounce = rules.Bounce.Bounce,
                DiffuseShape = rules.Bounce.DiffuseShape,
                DiffuseMatchIncident = rules.Bounce.DiffuseMatchIncident,
                DiffuseCornerRadius = rules.Bounce.DiffuseCornerRadius,
                DiffuseGain = rules.Bounce.DiffuseGain,
                DiffuseNearDistance = rules.Bounce.DiffuseNearDistance,
                DiffuseFarDistance = rules.Bounce.DiffuseFarDistance,
                DiffuseMaxReach = rules.Bounce.DiffuseMaxReach,
                DiffuseLateralSpread = rules.Bounce.DiffuseLateralSpread,
                DiffuseVisibilityProbes = rules.Bounce.DiffuseVisibilityProbes,
                EnvironmentEnabled = value.EnvironmentEnabled, LocalFillEnabled = value.LocalFillEnabled,
                Directional = value.Directional, Range = rules.Range, Cone = rules.Cone,
                ApertureWidth = rules.ApertureWidth, Intensity = rules.Intensity,
                Color = new UnityEngine.Color(rules.Red, rules.Green, rules.Blue, 1),
                SoftShadows = rules.SoftShadows, Softness = rules.Softness, ConeFeather = rules.ConeFeather,
                NearRange = rules.NearRange, NearIntensity = rules.NearIntensity,
                FillRadius = value.FillRadius, FillIntensity = value.FillIntensity, FillColor = value.FillColor
            };
        }
    }
}
