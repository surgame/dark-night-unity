using System;
using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>
    /// 可保存的单灯预设；拥有模板引用和默认制作参数，多个 Definition 可引用并按项覆盖。
    /// 不保存挂点、受光对象、开关、瞄准或业务状态，运行实例只消费冻结结果。
    /// </summary>
    [CreateAssetMenu(menuName = "Dark Nights/Lighting/光照预设", fileName = "LightProfile")]
    public sealed class LightProfile : ScriptableObject
    {
        public LightEffect Template;
        public LightBounceMode BounceMode = LightProfileDefaults.BounceMode;
        [Range(0, .4f)] public float Bounce = LightProfileDefaults.Bounce;
        public WallDiffuseShape DiffuseShape = LightProfileDefaults.DiffuseShape;
        public bool DiffuseMatchIncident = LightProfileDefaults.DiffuseMatchIncident;
        [Range(0, 2)] public float DiffuseCornerRadius = LightProfileDefaults.DiffuseCornerRadius;
        [Range(0, 2)] public float DiffuseGain = LightProfileDefaults.DiffuseGain;
        [Range(0, 6)] public float DiffuseNearDistance = LightProfileDefaults.DiffuseNearDistance;
        [Range(.1f, 14)] public float DiffuseFarDistance = LightProfileDefaults.DiffuseFarDistance;
        [Range(0, 6)] public float DiffuseMaxReach = LightProfileDefaults.DiffuseMaxReach;
        [Range(0, 2)] public float DiffuseLateralSpread = LightProfileDefaults.DiffuseLateralSpread;
        [Range(2, 4)] public int DiffuseVisibilityProbes = LightProfileDefaults.DiffuseVisibilityProbes;
        public bool EnvironmentEnabled = LightProfileDefaults.EnvironmentEnabled;
        public bool LocalFillEnabled = LightProfileDefaults.LocalFillEnabled;
        public bool Directional = LightProfileDefaults.Directional;
        [Range(2, 24)] public float Range = LightProfileDefaults.Range;
        [Range(20, 150)] public float Cone = LightProfileDefaults.Cone;
        [Range(0, 2)] public float ApertureWidth = LightProfileDefaults.ApertureWidth;
        [Range(0, 4)] public float Intensity = LightProfileDefaults.Intensity;
        public Color Color = new Color(LightProfileDefaults.Red, LightProfileDefaults.Green, LightProfileDefaults.Blue, 1);
        public bool SoftShadows = LightProfileDefaults.SoftShadows;
        [Range(0, .75f)] public float Softness = LightProfileDefaults.Softness;
        [Range(0, .25f)] public float ConeFeather = LightProfileDefaults.ConeFeather;
        [Range(.25f, 5)] public float NearRange = LightProfileDefaults.NearRange;
        [Range(0, 2)] public float NearIntensity = LightProfileDefaults.NearIntensity;
        [Range(.25f, 5)] public float FillRadius = LightProfileDefaults.FillRadius;
        [Range(0, 2)] public float FillIntensity = LightProfileDefaults.FillIntensity;
        public Color FillColor = new Color(LightProfileDefaults.Red, LightProfileDefaults.Green, LightProfileDefaults.Blue, 1);
        public int Revision { get; private set; }
        private void OnValidate() => Revision++;

        public LightProfileSnapshot Freeze()
        {
            if (Template == null) throw new InvalidOperationException("光照预设缺少光效模板：" + name);
            Template.ValidateStructure();
            return new LightProfileSnapshot(Template, EnvironmentEnabled, LocalFillEnabled, Directional,
                new LightEmissionRules(Range, Cone, Intensity, NearRange, NearIntensity,
                    Color.r, Color.g, Color.b, ApertureWidth, SoftShadows, Softness, ConeFeather,
                    new LightBounceRules(BounceMode, Bounce, DiffuseShape, DiffuseMatchIncident, DiffuseCornerRadius, DiffuseGain, DiffuseNearDistance, DiffuseFarDistance, DiffuseMaxReach, DiffuseLateralSpread, DiffuseVisibilityProbes)),
                FillRadius, FillIntensity, FillColor);
        }
    }
}
