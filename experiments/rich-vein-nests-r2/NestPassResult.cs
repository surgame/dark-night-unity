using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// 可插拔生成步骤的冻结输出，巢位、局部空腔补丁和最终坡形属于同一候选版本。
    /// 关闭时补丁为空；输入R1地形／矿物未被修改，触发阶段不是本对象的状态。
    /// </summary>
    public sealed class NestPassResult
    {
        public bool Enabled { get; }
        public IReadOnlyList<NestPlacement> Placements { get; }
        public IReadOnlyList<NestDecision> Decisions { get; }
        public IReadOnlyList<int> CarveCells { get; }
        public IReadOnlyList<byte> Shapes { get; }

        public NestPassResult(bool enabled, IEnumerable<NestPlacement> placements,
            IEnumerable<NestDecision> decisions, int[] carve, byte[] shapes)
        {
            Enabled = enabled; Placements = Array.AsReadOnly(placements.ToArray());
            Decisions = Array.AsReadOnly(decisions.ToArray()); CarveCells = Array.AsReadOnly((int[])carve.Clone());
            Shapes = Array.AsReadOnly((byte[])shapes.Clone());
            if (!enabled && (Placements.Count != 0 || CarveCells.Count != 0))
                throw new ArgumentException("关闭巢穴步骤后不得保留静态巢位或雕刻补丁。");
        }
    }
}
