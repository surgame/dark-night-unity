using System;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 保存需要统一环境染色的精灵及各自作者基础色，并按一次调用恢复性地计算最终颜色。
    /// 条目把 Renderer 与基础色绑定为同一配置单元，避免两个并行数组在 Prefab 编辑后错位。
    /// </summary>
    [Serializable]
    public sealed class SpriteTintGroup
    {
        [SerializeField] private SpriteTintTarget[] targets = Array.Empty<SpriteTintTarget>();

        public int Count => targets.Length;

        public void Apply(Color tint, Color ambient)
        {
            Apply(tint, ambient, null, tint);
        }

        public void Apply(Color tint, Color ambient, SpriteRenderer accent, Color accentTint)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                SpriteRenderer renderer = targets[i].Renderer;
                if (renderer == null) throw new InvalidOperationException("Tint target contains a missing renderer.");
                Color applied = renderer == accent ? accentTint : tint;
                renderer.color = targets[i].BaseColor * applied * ambient;
            }
        }
    }
}
