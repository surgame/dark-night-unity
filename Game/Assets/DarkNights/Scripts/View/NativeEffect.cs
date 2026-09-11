using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 原生箭矢、浮字和命令圈的序列化表现绑定，坐标和寿命沿用原作像素参数。
    /// 调用者传入冻结轨迹或事件及表现年龄；本组件没有伤害、命令或模拟推进能力。
    /// </summary>
    public sealed class NativeEffect : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private Image icon;
        [SerializeField] private Sprite[] resources;
        [SerializeField] private LineRenderer ring;

        public void Present(VisualCue cue, double age, float ground)
        {
            transform.position = new Vector3(cue.X / 100, (ground - cue.Y) / 100, 0);
            if (ring != null)
            {
                float radius = (5 + (float)age * 10) / 100;
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / ring.positionCount;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.35f));
                }
                var tint = new Color(0.9f, 0.84f, 0.56f, Mathf.Clamp01(1 - (float)age / 0.8f));
                ring.startColor = ring.endColor = tint;
                return;
            }
            label.text = cue.Text;
            Color color = cue.Kind == "resource" ? new Color32(198, 219, 163, 255) :
                cue.Enemy ? new Color32(232, 218, 193, 255) : new Color32(239, 149, 130, 255);
            color.a = Mathf.Clamp01((1.6f - (float)age) * 2);
            label.color = color;
            label.rectTransform.anchoredPosition = new Vector2(-5 + Mathf.Sin(cue.X * 3.7f + cue.Y) * 5, (float)age * 10);
            icon.gameObject.SetActive(cue.Kind == "resource");
            if (cue.Kind == "resource")
            {
                int index = 0;
                for (int i = 0; i < resources.Length; i++)
                    if (DarkNights.Core.Config.GameText.ResourceIds[i] == cue.ContentId) index = i;
                icon.sprite = resources[Mathf.Clamp(index, 0, resources.Length - 1)];
                icon.color = color;
                icon.rectTransform.anchoredPosition = label.rectTransform.anchoredPosition + new Vector2(10, 6);
            }
        }

        public void Present(ProjectileViewData arrow, double age, float ground)
        {
            float ratio = Mathf.Clamp01((float)(age / arrow.Duration));
            Vector2 from = new Vector2(arrow.FromX, arrow.FromY), to = new Vector2(arrow.ToX, arrow.ToY);
            Vector2 point = Vector2.Lerp(from, to, ratio) + new Vector2(0, -Mathf.Sin(Mathf.PI * ratio) * 10);
            Vector2 tangent = to - from + new Vector2(0, -Mathf.Cos(Mathf.PI * ratio) * 31.4f);
            transform.position = new Vector3(point.x / 100, (ground - point.y) / 100, 0);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(-tangent.y, tangent.x) * Mathf.Rad2Deg);
        }
    }
}
