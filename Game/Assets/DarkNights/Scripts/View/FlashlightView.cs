using System;
using UnityEngine;
using DarkNights.View.Lighting;
using YY.Features.Players.View;

namespace DarkNights.View
{
    /// <summary>手电 Prefab 唯一主视图，外观和灯口均为作者绑定；只接受展示值，不拥有角色业务状态。</summary>
    public sealed class FlashlightView : ObjectView
    {
        [SerializeField] private Transform emitter;
        [SerializeField] private SpriteRenderer body;
        private readonly LightEffectBinding light = new LightEffectBinding();
        private LightProfileSnapshot applied;
        private UnityEngine.Object boundContext;
        private UnityEngine.Object debugOwner;
        private LightProfileSnapshot debugProfile;
        private UnityEngine.Object debugMountOwner;
        private bool debugMounted;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;
        public LightProfileSnapshot BaseLightProfile { get; private set; }
        public LightEffect Effect => light.Effect;
        public Transform Emitter => emitter;
        private bool angleKnown;
        public float DisplayedAngle { get; private set; }
        public Vector3 EmitterPosition => emitter != null ? emitter.position :
            throw new InvalidOperationException("手电缺少显式灯口绑定。");

        public void BindLight(LightProfileSnapshot profile, UnityEngine.Object context, Transform mount, Transform fillAnchor, SpriteRenderer[] receivers)
        {
            BaseLightProfile = profile;
            if (debugOwner != null && debugProfile != null) profile = debugProfile;
            if (ReferenceEquals(applied, profile) && boundContext == context && light.Effect != null) return;
            if (emitter == null || body == null) throw new InvalidOperationException("手电必须绑定灯口与道具外观。");
            var all = new SpriteRenderer[receivers.Length + 1];
            Array.Copy(receivers, all, receivers.Length); all[receivers.Length] = body;
            light.Bind(profile, context, emitter, mount, fillAnchor, all);
            applied = profile; boundContext = context;
        }

        public void SetDebugLighting(UnityEngine.Object owner, LightProfileSnapshot profile)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (profile == null)
            {
                if (debugOwner != owner) return;
                debugOwner = null; debugProfile = null;
            }
            else
            {
                if (debugOwner != null && debugOwner != owner) throw new InvalidOperationException("该手电已被其他调试草稿使用。");
                debugOwner = owner; debugProfile = profile;
            }
        }

        public void SetDebugMount(UnityEngine.Object owner, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            if (owner == null || emitter == null) throw new InvalidOperationException("临时灯口调整缺少草稿或明确绑定。");
            if (debugMounted && debugMountOwner != owner) throw new InvalidOperationException("该灯口已被其他草稿使用。");
            if (!debugMounted)
            {
                originalPosition = emitter.localPosition; originalRotation = emitter.localRotation; originalScale = emitter.localScale;
                debugMounted = true; debugMountOwner = owner;
            }
            emitter.localPosition = position; emitter.localEulerAngles = rotation; emitter.localScale = scale;
        }

        public void ClearDebugMount(UnityEngine.Object owner) { if (debugMountOwner == owner) RestoreMount(); }
        public void AcceptDebugMount(UnityEngine.Object owner)
        {
            if (debugMountOwner != owner) return;
            debugMounted = false; debugMountOwner = null;
        }
        private void RestoreMount()
        {
            if (debugMounted && emitter != null)
            { emitter.localPosition = originalPosition; emitter.localRotation = originalRotation; emitter.localScale = originalScale; }
            debugMounted = false; debugMountOwner = null;
        }

        public void SetLight(bool enabled) => light.SetOn(enabled);

        public void Present(float angle, bool visible, bool immediate = true)
        {
            if (emitter == null || body == null) throw new InvalidOperationException("手电视图绑定不完整。");
            DisplayedAngle = !angleKnown || immediate ? angle : Mathf.LerpAngle(DisplayedAngle,angle,
                1-Mathf.Exp(-20*Time.unscaledDeltaTime));
            angleKnown = true;
            transform.rotation = Quaternion.Euler(0, 0, DisplayedAngle);
            body.enabled = visible;
        }
        protected override void OnDisable()
        {
            RestoreMount();
            angleKnown = false;
            light.Dispose();
            applied = null; boundContext = null;
            BaseLightProfile = null; debugOwner = null; debugProfile = null;
            base.OnDisable();
        }
    }
}
