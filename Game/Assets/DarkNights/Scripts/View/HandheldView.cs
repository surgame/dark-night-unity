using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 人物原生 Prefab 的手持挂点，显式绑定道具、手臂和枪口；只采样冻结动作，不结算碰撞。
    /// 动作来源为独立像素部件和时间曲线，角色本体与原职业动画资源保持可编辑。
    /// </summary>
    public sealed class HandheldView : MonoBehaviour
    {
        [SerializeField] private Transform pivot;
        [SerializeField] private SpriteRenderer item;
        [SerializeField] private SpriteRenderer arm;
        [SerializeField] private SpriteRenderer flash;
        [SerializeField] private Sprite[] items = Array.Empty<Sprite>();
        [SerializeField] private SpriteRenderer[] occupationalParts = Array.Empty<SpriteRenderer>();

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer clothing;
        [SerializeField] private Sprite[] idle = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] walking = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] shirtIdle = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] shirtWalking = Array.Empty<Sprite>();
        private bool presented;
        private Vector3 bodyPosition, clothingPosition;

        public void RestorePose()
        {
            if (!presented) return;
            body.transform.localPosition = bodyPosition;
            if (clothing != null) clothing.transform.localPosition = clothingPosition;
            foreach (var part in occupationalParts) part.enabled = true;
            presented = false;
        }

        public void Present(ActorViewData actor, double elapsed, Color ambient)
        {
            bool visible = actor.ManualControl && actor.Hp > 0 && actor.SelectedItem < 3;
            item.enabled = arm.enabled = visible;
            flash.enabled = false;
            if (!visible) return;
            foreach (var part in occupationalParts) part.enabled = false;
            bodyPosition = body.transform.localPosition;
            if (clothing != null) clothingPosition = clothing.transform.localPosition;
            presented = true;
            Sprite[] frames = actor.Walking ? walking : idle;
            Sprite[] shirts = actor.Walking ? shirtWalking : shirtIdle;
            int frame = (int)((actor.ActionTime + elapsed) * 8) % frames.Length;
            body.sprite = frames[frame]; body.transform.localPosition = new Vector3(-.06f, .06f, 0);
            if (clothing != null)
            { clothing.sprite = shirts[frame % shirts.Length]; clothing.transform.localPosition = new Vector3(-.06f, .06f, 0); }
            item.sprite = items[actor.SelectedItem];
            float face = actor.Face < 0 ? -1 : 1;
            float angle = actor.AimAngle;
            if (face < 0) angle = 180 - angle;
            double remaining = Math.Max(0, actor.EquipmentAction - elapsed);
            float progress = actor.EquipmentActionDuration <= 0 ? 1 : Mathf.Clamp01(1 - (float)(remaining / actor.EquipmentActionDuration));
            float recoil = actor.SelectedItem == 0 && remaining > 0 ? (float)(remaining / .12) : 0;
            if (actor.SelectedItem == 1)
                angle = remaining > 0 ? Mathf.Lerp(105, -55, Mathf.SmoothStep(0, 1, progress)) : -20;
            else if (actor.SelectedItem == 2)
                angle = actor.Charging ? 115 : remaining > 0 ? -25 : -40;
            pivot.localRotation = Quaternion.Euler(0, 0, angle);
            pivot.localPosition = new Vector3(0.015f - recoil * .02f, .09f, 0);
            item.color = arm.color = ambient;
            flash.enabled = actor.SelectedItem == 0 && remaining > .07;
            flash.color = Color.white;
        }

        public void Hide()
        {
            RestorePose();
            item.enabled = arm.enabled = flash.enabled = false;
        }
    }
}
