using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>冻结的有序轮廓流水线；空栈原样返回，各阶段从前一只读结果计算，每次地图重建从基础轮廓开始，不累积上次产物。</summary>
    public sealed class CaveModifierStack
    {
        private readonly ICaveMaskModifier[] modifiers;
        public static CaveModifierStack Empty { get; } = new CaveModifierStack(Array.Empty<ICaveMaskModifier>());
        public bool Enabled => modifiers.Length > 0;
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
    }
}
