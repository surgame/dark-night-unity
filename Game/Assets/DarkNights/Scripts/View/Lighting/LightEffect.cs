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
        private bool isOn;
        private LightProfileSnapshot profile;
        private static readonly HashSet<LightEffect> active = new HashSet<LightEffect>();
        private UnityEngine.Object owner;
        public LightEnvironmentEmitter Environment => environment;
        public LocalLightFill LocalFill => localFill;
        public bool IsOn => isOn;
        public void SetOn(bool value) { isOn = value; }
        public bool EnvironmentEnabled => profile?.EnvironmentEnabled == true;
        public bool LocalFillEnabled => profile?.LocalFillEnabled == true;

        public void Apply(LightProfileSnapshot value)
        {
            ValidateStructure(); profile = value ?? throw new ArgumentNullException(nameof(value));
            environment.Apply(value.Rules, value.Directional);
            localFill.Apply(value.FillRadius, value.FillIntensity, value.FillColor);
        }

        public void Bind(UnityEngine.Object context, Transform mount, Transform fillAnchor, SpriteRenderer[] receivers)
        {
            ValidateStructure();
            if (profile == null || context == null) throw new InvalidOperationException("光效必须先应用配置，再绑定当前上下文。");
            owner = context;
            environment.Bind(mount); localFill.Bind(fillAnchor, receivers);
            if (isActiveAndEnabled) active.Add(this);
        }

        public void Unbind()
        {
            active.Remove(this); owner = null; isOn = false;
            environment?.Unbind(); localFill?.Unbind();
        }

        public void Validate() => ValidateStructure();
        public void ValidateStructure()
        {
            if (environment == null || localFill == null || environment.Emitter == null)
                throw new InvalidOperationException("LightEffect 缺少两个分支或明确资源绑定。");
            localFill.Validate();
        }
        public static void Collect(UnityEngine.Object context, Camera camera, List<LightEffect> result)
        {
            result.Clear();
            foreach (var effect in active)
                if (effect != null && effect.profile != null && effect.owner != null && effect.owner == context)
                    result.Add(effect);
            result.Sort((a, b) => a.GetEntityId().CompareTo(b.GetEntityId()));
        }
        private void OnEnable() { if (owner != null && profile != null) active.Add(this); }
        private void OnDisable() => Unbind();
    }
}
