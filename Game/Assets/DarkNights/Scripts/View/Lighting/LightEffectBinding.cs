using System;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>一个物体视图的光效实例所有者；从已加载预设模板同步挂载，重复绑定不叠灯，退役前撤除所有受光和上下文引用。</summary>
    public sealed class LightEffectBinding : IDisposable
    {
        private LightEffect effect;
        private LightEffect template;
        private LightProfileSnapshot applied;
        public LightEffect Effect => effect;

        public void Bind(LightProfileSnapshot profile, UnityEngine.Object context, Transform parent,
            Transform occlusionAnchor, Transform fillAnchor, SpriteRenderer[] receivers)
        {
            if (profile == null || context == null || parent == null)
                throw new ArgumentException("光效挂载必须具有配置、上下文和明确挂点。");
            if (effect == null || template != profile.Template)
            {
                Dispose(); template = profile.Template;
                effect = UnityEngine.Object.Instantiate(template, parent, false);
                effect.name = "LightEffect (配置驱动)";
                effect.transform.localPosition = Vector3.zero;
                effect.transform.localRotation = Quaternion.identity;
                effect.transform.localScale = Vector3.one;
            }
            else if (effect.transform.parent != parent) effect.transform.SetParent(parent, false);
            if (!ReferenceEquals(applied, profile)) { effect.Apply(profile); applied = profile; }
            effect.Bind(context, occlusionAnchor, fillAnchor, receivers);
            effect.Environment.Bind(occlusionAnchor, parent);
            effect.gameObject.SetActive(true);
        }

        public void SetOn(bool enabled) { if (effect != null) effect.SetOn(enabled); }

        public void Dispose()
        {
            if (effect != null)
            {
                effect.SetOn(false); effect.Unbind(); effect.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(effect.gameObject);
                else UnityEngine.Object.DestroyImmediate(effect.gameObject);
            }
            effect = null; template = null; applied = null;
        }
    }
}
