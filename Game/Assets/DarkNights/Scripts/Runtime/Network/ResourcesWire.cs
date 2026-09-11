using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// ResourceAmounts 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
    [MemoryPackable]
    public partial class ResourcesWire
    {
        public double Food { get; set; }
        public double Wood { get; set; }
        public double Stone { get; set; }
        public double Iron { get; set; }
        public double Gold { get; set; }

        public static ResourcesWire From(ResourceAmounts value) => new ResourcesWire
        {
            Food = value.Food,
            Wood = value.Wood,
            Stone = value.Stone,
            Iron = value.Iron,
            Gold = value.Gold,
        };

        public ResourceAmounts Freeze() => new ResourceAmounts(
            Food,
            Wood,
            Stone,
            Iron,
            Gold);
    }
}
