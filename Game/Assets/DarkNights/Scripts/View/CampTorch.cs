using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 火把 Prefab 的原始七帧火焰及显式灯杆绑定，按根节点横坐标保持原版位置相位。
    /// 环境提供未缩放表现时间和环境色；不建立独立时钟、碰撞或游戏状态。
    /// </summary>
    public sealed class CampTorch : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer flame;
        [SerializeField] private SpriteRenderer pole;
        [SerializeField] private Sprite[] frames;
        [SerializeField] private double framesPerSecond = 10;
        [SerializeField] private Color poleColor = new Color(0.32549f, 0.254902f, 0.184314f, 1);
        public void Present(double time, Color ambient)
        {
            int frame = (int)(time * framesPerSecond + transform.position.x * 100) % frames.Length;
            flame.sprite = frames[(frame + frames.Length) % frames.Length];
            flame.color = ambient;
            pole.color = poleColor * ambient;
        }
    }
}
