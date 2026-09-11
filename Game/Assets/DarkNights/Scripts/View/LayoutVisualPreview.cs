using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 场景布局标记附属的原生编辑预览；进入 Play 时关闭自身，由正式对象工厂创建客户端展示。
    /// 不启动会话、不读取存档，也不消耗任何规则随机数；预览外观保存于场景和 Prefab。
    /// </summary>
    public sealed class LayoutVisualPreview : MonoBehaviour
    {
        private void Awake() { if (Application.isPlaying) gameObject.SetActive(false); }
    }
}
