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
        [SerializeField] private LightEffect effect;
        public LightEffect Effect => effect;
        private bool angleKnown;
        public float DisplayedAngle { get; private set; }
        public Vector3 EmitterPosition => emitter != null ? emitter.position :
            throw new InvalidOperationException("手电缺少显式灯口绑定。");

        public void BindLight(UnityEngine.Object context, Transform mount, Transform fillAnchor, SpriteRenderer[] receivers)
        {
            if (effect == null || emitter == null || body == null) throw new InvalidOperationException("手电必须绑定通用光效与道具外观。");
            var all = new SpriteRenderer[receivers.Length + 1];
            Array.Copy(receivers, all, receivers.Length); all[receivers.Length] = body;
            effect.Bind(context, mount, fillAnchor, all); effect.Environment.Bind(mount, emitter);
        }

        public void SetLight(bool enabled) { effect.SetOn(enabled); }

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
            angleKnown = false;
            base.OnDisable();
        }
    }
}
