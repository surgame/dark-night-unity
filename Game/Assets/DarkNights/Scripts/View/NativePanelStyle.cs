using System;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 原生面板的可编辑主题值，保存来源样式的背景、内边框和圆角，不包含布局或业务状态。
    /// 由 Prefab 持久保存；按钮的四种交互状态各自拥有完整样式，避免颜色比例丢失透明度。
    /// </summary>
    [Serializable]
    public struct NativePanelStyle
    {
        public Color Background;
        public Color Border;
        public float BorderWidth;
        public float Radius;
    }
}
