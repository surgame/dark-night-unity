using System;
using System.Collections.Generic;

namespace DarkNights.Experiments.MiningArchetypes
{
    /// <summary>
    /// 一条由矿体侧向进入巢腔的冻结封岩；揭露序仅用于对比，玩家不被强制按此开挖。
    /// 未包含怪物状态、门或触发器，也不承担运行收益和地形写入。
    /// </summary>
    public sealed class ArchetypeGate
    {
        public int Contact { get; }
        public int RevealCount { get; }
        public IReadOnlyList<int> Cells { get; }
        public ArchetypeGate(int contact, int revealCount, int[] cells)
        {
            if (cells.Length < 1 || cells.Length > 3) throw new ArgumentException("侧壁距离无效。");
            Contact = contact; RevealCount = revealCount; Cells = Array.AsReadOnly((int[])cells.Clone());
        }
    }
}
