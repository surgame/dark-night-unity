using System;
using System.Globalization;

namespace DarkNights.Core.Config
{
    /// <summary>每个环境光源的冻结返光合同；预设和逐项覆盖合成后唯一持有，不读取场景参数或装备状态，更新时整体替换。</summary>
    public sealed class LightBounceRules
    {
        public LightBounceMode BounceMode { get; }
        public float Bounce { get; }
        public WallDiffuseShape DiffuseShape { get; }
        public bool DiffuseMatchIncident { get; }
        public float DiffuseCornerRadius { get; }
        public float DiffuseGain { get; }
        public float DiffuseNearDistance { get; }
        public float DiffuseFarDistance { get; }
        public float DiffuseMaxReach { get; }
        public float DiffuseLateralSpread { get; }
        public int DiffuseVisibilityProbes { get; }
        public bool Continuous => BounceMode == LightBounceMode.Continuous && DiffuseGain > 0 && DiffuseMaxReach > 0;

        public LightBounceRules(
            LightBounceMode bounceMode = LightProfileDefaults.BounceMode,
            float bounce = LightProfileDefaults.Bounce,
            WallDiffuseShape diffuseShape = LightProfileDefaults.DiffuseShape,
            bool diffuseMatchIncident = LightProfileDefaults.DiffuseMatchIncident,
            float diffuseCornerRadius = LightProfileDefaults.DiffuseCornerRadius,
            float diffuseGain = LightProfileDefaults.DiffuseGain,
            float diffuseNearDistance = LightProfileDefaults.DiffuseNearDistance,
            float diffuseFarDistance = LightProfileDefaults.DiffuseFarDistance,
            float diffuseMaxReach = LightProfileDefaults.DiffuseMaxReach,
            float diffuseLateralSpread = LightProfileDefaults.DiffuseLateralSpread,
            int diffuseVisibilityProbes = LightProfileDefaults.DiffuseVisibilityProbes)
        {
            if (!Enum.IsDefined(typeof(LightBounceMode), bounceMode) || !Enum.IsDefined(typeof(WallDiffuseShape), diffuseShape) ||
                !Valid(bounce, 0, .4f) || !Valid(diffuseCornerRadius, 0, 2) || !Valid(diffuseGain, 0, 2) ||
                !Valid(diffuseNearDistance, 0, 6) || !Valid(diffuseFarDistance, .1f, 14) || diffuseFarDistance <= diffuseNearDistance ||
                !Valid(diffuseMaxReach, 0, 6) || !Valid(diffuseLateralSpread, 0, 2) || diffuseVisibilityProbes < 2 || diffuseVisibilityProbes > 4)
                throw new ArgumentException("单灯返光参数无效；关闭距离必须大于近距离。");
            BounceMode = bounceMode;
            Bounce = bounce;
            DiffuseShape = diffuseShape;
            DiffuseMatchIncident = diffuseMatchIncident;
            DiffuseCornerRadius = diffuseCornerRadius;
            DiffuseGain = diffuseGain;
            DiffuseNearDistance = diffuseNearDistance;
            DiffuseFarDistance = diffuseFarDistance;
            DiffuseMaxReach = diffuseMaxReach;
            DiffuseLateralSpread = diffuseLateralSpread;
            DiffuseVisibilityProbes = diffuseVisibilityProbes;
        }

        public string Fingerprint => string.Join(",", Array.ConvertAll(new[]
        { Bounce, DiffuseCornerRadius, DiffuseGain, DiffuseNearDistance, DiffuseFarDistance, DiffuseMaxReach, DiffuseLateralSpread },
            value => value.ToString("R", CultureInfo.InvariantCulture))) + "," + (int)BounceMode + "," + (int)DiffuseShape +
            "," + DiffuseMatchIncident + "," + DiffuseVisibilityProbes;
        private static bool Valid(float value, float low, float high) => float.IsFinite(value) && value >= low && value <= high;
    }
}
