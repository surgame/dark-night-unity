using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 本地瞄准及使用边沿缓冲，保留发送间隔内的短按；不持有角色权威状态或计算蓄力距离。
    /// UI 阻塞、换装和失焦后要求松开再按，取消不会被误解释为投掷释放。
    /// </summary>
    public sealed class HeroActionInput
    {
        private bool suppress;
        private float sentAim;
        public float Aim { get; private set; }
        public bool Held { get; private set; }
        public bool Pressed { get; private set; }
        public bool Released { get; private set; }
        public bool Cancelled { get; private set; }
        public bool Changed => Pressed || Released || Cancelled || Mathf.Abs(Mathf.DeltaAngle(sentAim, Aim)) > 1;

        public void Sample(GameInputActions input, Camera camera, Vector3 hand, bool allowed)
        {
            bool raw = input.UseItem.IsPressed();
            if (!allowed || !input.CanRead(input.UseItem)) { Cancel(); return; }
            if (suppress)
            {
                if (!raw) suppress = false;
                Held = false;
                return;
            }
            Vector3 aim = camera.ScreenToWorldPoint(input.Pointer) - hand;
            if (aim.sqrMagnitude > .0001f) Aim = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            Pressed |= input.UseItem.WasPressedThisFrame();
            Released |= input.UseItem.WasReleasedThisFrame();
            Held = raw;
        }

        public void Consume()
        {
            Pressed = Released = Cancelled = false;
            sentAim = Aim;
        }

        public void Cancel()
        {
            Held = Pressed = Released = false;
            Cancelled = suppress = true;
        }
    }
}
