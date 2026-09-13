using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 原生营地灯的位置、半径及闪烁参数；环境统一采样表现时间，不读取或修改玩法随机数。
    /// 光照数据只交给本地精灵材质，移动 Prefab 根节点即可调整灯位，编辑预览固定在时间零。
    /// </summary>
    public sealed class CampLight : MonoBehaviour
    {
        [SerializeField] private float radius = 0.576f;
        [SerializeField] private Color tint = new Color(1, 0.827451f, 0.6117647f, 1);
        [SerializeField] private float energy = 0.65f;
        [SerializeField] private float flicker = 0.07f;
        [SerializeField] private double frequency = 3.1;
        public Vector4 Shape => new Vector4(transform.position.x, transform.position.y, radius, 0);
        public Vector4 ColorAt(double time, float night)
        {
            float power = night * (energy + Mathf.Sin((float)(time * frequency + transform.position.x * 100)) * flicker);
            Color linear = tint.linear;
            return new Vector4(linear.r * power, linear.g * power, linear.b * power, 0);
        }
    }
}
