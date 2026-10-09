using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>一帧通用光源的冻结展示参数；位置来自真实挂点，规则来自效果 Prefab，不持有角色或池状态。</summary>
    public readonly struct LightEmitterData
    {
        public readonly Vector3 Position;
        public readonly Vector3 NearPosition;
        public readonly float Angle;
        public readonly LightEmissionRules Rules;
        public readonly bool Directional;
        public LightEmitterData(Vector3 position, float angle, LightEmissionRules rules, bool directional = true, Vector3? nearPosition = null)
        { Position = position; NearPosition = nearPosition ?? position; Angle = angle; Rules = rules; Directional = directional; }
    }
}
