using System;
using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>任意挂点的环境光源；只缓存已合成的冻结配置，按灯口变换采样交给相机共享光场，不拥有道具或角色状态。</summary>
    public sealed class LightEnvironmentEmitter : MonoBehaviour
    {
        [SerializeField] private Transform emitter;
        private bool directional;
        private Transform mount;
        private Transform boundEmitter;
        private LightEmissionRules rules;
        public Transform Emitter => emitter;

        public void Bind(Transform occlusionAnchor, Transform mouth = null) { mount = occlusionAnchor; boundEmitter = mouth; }
        public void Unbind() { mount = null; boundEmitter = null; }
        public void Apply(LightEmissionRules value, bool isDirectional)
        {
            rules = value ?? throw new ArgumentNullException(nameof(value)); directional = isDirectional;
        }
        public LightEmissionRules Freeze() => rules ?? throw new InvalidOperationException("环境光源尚未应用光照配置。");

        public LightEmitterData Sample(Terrain.TerrainPreview preview)
        {
            if (emitter == null || rules == null) throw new InvalidOperationException("环境光源缺少明确灯口或光照配置。");
            Transform mouth = boundEmitter != null ? boundEmitter : emitter;
            Vector3 origin = mouth.position;
            if (preview != null) origin = SafeEmitter(preview, mount != null ? mount.position : transform.position, origin);
            Vector3 forward = mouth.TransformVector(Vector3.right);
            float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            return new LightEmitterData(origin, angle, rules, directional);
        }

        private static Vector3 SafeEmitter(Terrain.TerrainPreview preview, Vector3 anchor, Vector3 emitterPosition)
        {
            Vector3 previous = anchor;
            for (int n = 0; n <= 8; n++)
            {
                Vector3 local = preview.transform.InverseTransformPoint(Vector3.Lerp(anchor, emitterPosition, n / 8f));
                float x = local.x + .5f, y = -local.y + .5f;
                byte cell = preview.LightingSource.LightingCell(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
                if ((cell & 128) == 0 || (cell & 64) != 0 && Core.Logic.Terrain.TerrainShapeGeometry.Contains(
                    (Core.Config.Terrain.TerrainCellShape)(cell & 15), x - Mathf.Floor(x), 1 - (y - Mathf.Floor(y)))) return previous;
                previous = Vector3.Lerp(anchor, emitterPosition, n / 8f);
            }
            return previous;
        }
    }
}
