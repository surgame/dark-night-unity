using System;
using DarkNights.Core.Config;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DarkNights.View.Lighting
{
    /// <summary>任意挂点的环境光源；视觉参数归组件，冻结采样交给相机共享光场，不拥有道具或角色状态。</summary>
    public sealed class LightEnvironmentEmitter : MonoBehaviour
    {
        [SerializeField] private Transform emitter;
        [SerializeField] private ComputeShader lightingShader;
        [SerializeField] private Light2D urpLightTemplate;
        [SerializeField] private ShadowCaster2D urpShadowTemplate;
        [SerializeField] private bool directional = true;
        [SerializeField, Range(2, 24)] private float range = 14;
        [SerializeField, Range(20, 150)] private float cone = 90;
        [SerializeField, Range(0, 2)] private float apertureWidth = .375f;
        [SerializeField, Range(0, 4)] private float intensity = 1.35f;
        [SerializeField, Range(.25f, 5)] private float nearRange = 2.2f;
        [SerializeField, Range(0, 2)] private float nearIntensity;
        [SerializeField] private Color color = new Color(1, .88f, .69f, 1);
        private Transform mount;
        private Transform boundEmitter;
        private LightEmissionRules rules;
        public ComputeShader LightingShader => lightingShader;
        public Light2D UrpLightTemplate => urpLightTemplate;
        public ShadowCaster2D UrpShadowTemplate => urpShadowTemplate;
        public Transform Emitter => emitter;

        public void Bind(Transform occlusionAnchor, Transform mouth = null) { mount = occlusionAnchor; boundEmitter = mouth; }
        public void Unbind() { mount = null; boundEmitter = null; }
        public LightEmissionRules Freeze() => new LightEmissionRules(range, cone, intensity,
            nearRange, nearIntensity, color.r, color.g, color.b, apertureWidth);
        private void OnEnable() { rules = Freeze(); }
        private void OnValidate() { rules = null; }

        public LightEmitterData Sample(Terrain.TerrainPreview preview)
        {
            if (emitter == null || lightingShader == null) throw new InvalidOperationException("环境光源缺少明确灯口或计算资源绑定。");
            rules = rules ?? Freeze();
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
