using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 池化子弹、黏弹和爆炸原生视图；由冻结位置进行最多一帧同步窗口的外推，绝不触发游戏伤害。
    /// 租用时覆盖全部精灵、缩放和朝向，归还时关闭显示，下一颗投射物不继承上一颗姿态。
    /// </summary>
    public sealed class BallisticView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer surface;
        [SerializeField] private Sprite bullet;
        [SerializeField] private Sprite bomb;
        [SerializeField] private Sprite[] explosion = Array.Empty<Sprite>();

        public void Present(ProjectileViewData shot, double seconds, float ground, Color ambient)
        {
            float t = shot.Stuck ? 0 : (float)Math.Min(.1, Math.Max(0, seconds));
            float x = shot.FromX + shot.VelocityX * t;
            float h = ground - shot.FromY + shot.VelocityY * t - shot.Gravity * t * t * .5f;
            transform.position = new Vector3(x / 100, h / 100, 0);
            float degrees = shot.Kind == 1 ? Mathf.Atan2(shot.VelocityY, shot.VelocityX) * Mathf.Rad2Deg :
                shot.Kind == 2 && !shot.Stuck ? (float)(shot.Age * 400) : 0;
            transform.rotation = Quaternion.Euler(0, 0, degrees);
            transform.localScale = Vector3.one;
            surface.color = shot.Kind == 3 ? Color.white : ambient;
            surface.sprite = shot.Kind == 1 ? bullet : bomb;
            if (shot.Kind == 3)
            {
                float progress = Mathf.Clamp01((float)((shot.Age + seconds) / shot.Duration));
                surface.sprite = explosion[Math.Min(explosion.Length - 1, (int)(progress * explosion.Length))];
                transform.localScale = Vector3.one * (shot.Radius * 2 / 24);
            }
        }
    }
}
