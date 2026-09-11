using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 声明正式页面占满其运行时父 Canvas；在 YYGC 移除编辑预览 Canvas 后恢复全屏锚点。
    /// 子控件的人工布局保持原生 RectTransform，组件不查找绑定或创建面板。
    /// </summary>
    [DefaultExecutionOrder(-9999), RequireComponent(typeof(RectTransform))]
    public sealed class FullscreenPanel : MonoBehaviour
    {
        private void Awake()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
