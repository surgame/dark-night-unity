using System;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 将单个渲染器和作者基础色作为不可拆分的序列化条目保存。
    /// 最终颜色始终从基础色重算，避免环境光或受击色在连续帧中累乘。
    /// </summary>
    [Serializable]
    public struct SpriteTintTarget
    {
        [SerializeField] private SpriteRenderer renderer;
        [SerializeField] private Color baseColor;

        public SpriteRenderer Renderer => renderer;
        public Color BaseColor => baseColor;
    }
}
