using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>冻结的有序轮廓栈；即时前景合同与原作者资产分离，局部能力不覆盖不受支持的 Modifier。</summary>
    public sealed class CaveModifierStack
    {
        private readonly ICaveMaskModifier[] modifiers;
        public static CaveModifierStack Empty { get; } = new CaveModifierStack(Array.Empty<ICaveMaskModifier>());
        public bool Enabled => modifiers.Length > 0;
        public bool SupportsLocalRoundedCluster => modifiers.Length == 0 ||
            (modifiers.Length == 1 && modifiers[0] is RoundedClusterModifier rounded &&
             rounded.AlgorithmVersion == RoundedClusterAlgorithmVersion.LocalV2);
        public string Identity => "mask-stack-1[" + string.Join(";", modifiers.Select(m => m.Identity.Length + ":" + m.Identity)) + "]";
        public CaveModifierStack(IEnumerable<ICaveMaskModifier> modifiers)
        {
            this.modifiers = modifiers?.ToArray() ?? throw new ArgumentNullException(nameof(modifiers));
            if (this.modifiers.Length > 8 || this.modifiers.Any(m => m == null || string.IsNullOrWhiteSpace(m.Identity)))
                throw new ArgumentException("轮廓栈最多八个有效 modifier。");
        }
        public CaveMaskField Apply(CaveMaskField source, string seed, Action checkpoint = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            foreach (var modifier in modifiers)
            {
                checkpoint?.Invoke();
                var next = modifier.Apply(source, seed, checkpoint);
                if (next == null || next.Width != source.Width || next.Height != source.Height || next.Top != source.Top)
                    throw new InvalidOperationException("Modifier 改写了栅格坐标合同。");
                source = next;
            }
            return source;
        }
        /// <summary>显式生成 LocalV2 前景副本，参数保留；不修改旧实例，不声称 V1/V2 像素完全相同。</summary>
        public CaveModifierStack AsLocalForeground()
        {
            if (SupportsLocalRoundedCluster) return this;
            if (modifiers.Length == 1 && modifiers[0] is RoundedClusterModifier original)
                return new CaveModifierStack(new ICaveMaskModifier[]
                {
                    new RoundedClusterModifier(original.Depth, original.Size, original.Petal, original.Density,
                        original.Variation, original.Grain, original.ModifierSeed, RoundedClusterAlgorithmVersion.LocalV2)
                });
            throw new NotSupportedException("即时前景只支持空栈或单个圆簇；请使用独立局部样式，或关闭 ImmediateForeground 对照旧栈。");
        }
        public RoundedClusterModifier RequireLocalRoundedCluster()
        {
            if (modifiers.Length == 0) return null;
            if (SupportsLocalRoundedCluster) return (RoundedClusterModifier)modifiers[0];
            throw new NotSupportedException("当前 modifier 栈不是已显式启用的 LocalV2 局部算法。");
        }
    }
}
