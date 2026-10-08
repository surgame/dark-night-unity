using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>可复用照明效果 Prefab 的入口；组合环境光与指定对象补光，登记仅持有本地视图引用，不拥有业务状态。</summary>
    [ExecuteAlways]
    public sealed class LightEffect : MonoBehaviour
    {
        [SerializeField] private LightEnvironmentEmitter environment;
        [SerializeField] private LocalLightFill localFill;
        [SerializeField] private bool isOn = true;
        private static readonly HashSet<LightEffect> active = new HashSet<LightEffect>();
        private UnityEngine.Object owner;
        public LightEnvironmentEmitter Environment => environment;
        public LocalLightFill LocalFill => localFill;
        public bool IsOn => isOn;
        public void SetOn(bool value) { isOn = value; }

        public void Bind(UnityEngine.Object context, Transform mount, Transform fillAnchor, SpriteRenderer[] receivers)
        {
            Validate(); owner = context;
            environment.Bind(mount); localFill.Bind(fillAnchor, receivers);
        }
        public void Validate()
        {
            if (environment == null || localFill == null || environment.Emitter == null || environment.LightingShader == null)
                throw new InvalidOperationException("LightEffect 缺少两个分支或明确资源绑定。");
            environment.Freeze(); localFill.Validate();
        }
        public static void Collect(UnityEngine.Object context, Camera camera, List<LightEffect> result)
        {
            result.Clear();
            foreach (var effect in active)
                if (effect != null && (effect.owner != null && effect.owner == context || effect.owner == null && effect.gameObject.scene == camera.gameObject.scene))
                    result.Add(effect);
            result.Sort((a, b) => a.GetEntityId().CompareTo(b.GetEntityId()));
        }
        private void OnEnable() { active.Add(this); }
        private void OnDisable()
        {
            active.Remove(this); owner = null;
            environment?.Unbind(); localFill?.Unbind();
        }
    }
}
