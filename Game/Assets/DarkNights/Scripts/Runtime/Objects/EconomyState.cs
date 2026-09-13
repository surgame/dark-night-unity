using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 营地经济能力拥有的库存、生产统计及消耗时钟；所有字段按值复制。
    /// 共享成本配置不被修改，跨对象的支付、占用与创建由会话同步事务协调。
    /// </summary>
    [MemoryPackable, StateData]
    public partial record EconomyState
    {
        public uint Sequence { get; set; }
        public double Food { get; internal set; }
        public double Wood { get; internal set; }
        public double Stone { get; internal set; }
        public double Iron { get; internal set; }
        public double Gold { get; internal set; }
        public double GatheredFood { get; internal set; }
        public double GatheredWood { get; internal set; }
        public double GatheredStone { get; internal set; }
        public double GatheredIron { get; internal set; }
        public double GatheredGold { get; internal set; }
        public double UpkeepElapsed { get; internal set; }
        public double StarvationElapsed { get; internal set; }
        public double RecruitCooldown { get; internal set; }
    }
}
