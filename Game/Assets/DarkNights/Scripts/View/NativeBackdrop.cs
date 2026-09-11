using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 单层原生背景的重复精灵与视差配置；只根据本地镜头和昼夜展示量移动，不影响布局或模拟。
    /// 每层的纹理、重复范围和基础颜色在场景中保存，可由美术独立调整。
    /// </summary>
    public sealed class NativeBackdrop : MonoBehaviour
    {
        [SerializeField] private float factor;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private bool fadeWithNight;
        [SerializeField] private SpriteRenderer[] sprites;

        public void Apply(float cameraX, float night, Color ambient)
        {
            transform.localPosition = new Vector3(cameraX / 100 * (1 - factor), 0, 0);
            Color color = tint * ambient;
            if (fadeWithNight) color.a *= night;
            foreach (SpriteRenderer sprite in sprites) sprite.color = color;
        }
    }
}
