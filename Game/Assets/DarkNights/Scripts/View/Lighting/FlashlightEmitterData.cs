using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>一帧光源的冻结展示参数；位置来自真实展示锚点，规则来自工具定义，不持有角色或池状态。</summary>
    public readonly struct FlashlightEmitterData
    {
        public readonly Vector3 Position;
        public readonly Vector3 NearPosition;
        public readonly float Angle;
        public readonly FlashlightRules Rules;
        public readonly bool Directional;
        public FlashlightEmitterData(Vector3 position, float angle, FlashlightRules rules, bool directional = true, Vector3? nearPosition = null)
        { Position = position; NearPosition = nearPosition ?? position; Angle = angle; Rules = rules; Directional = directional; }
    }
}
