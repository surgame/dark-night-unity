using System;
using DarkNights.Runtime.Session;
using GameCore.NetworkCommands;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 正式营地操作的可靠服务端请求；只有显式意图，没有玩家身份、库存或伤害写入字段。
    /// Processor 提供可信连接，处理时复制到 SessionRequest，归池后不保留本对象或 ActorIds 数组。
    /// </summary>
    [MemoryPackable, NetworkCommand(CommandChannel.Reliable, CommandScope.ServerOnly)]
    public partial record SessionCommand
    {
        public int SenderObjectId { get; set; }
        public int Protocol { get; set; }
        public int Epoch { get; set; }
        public int PolicyRevision { get; set; }
        public long RequestSequence { get; set; }
        public SessionOperation Operation { get; set; }
        public int[] ActorIds { get; set; }
        public int TargetId { get; set; }
        public float X { get; set; }
        public string Kind { get; set; }
        public int Value { get; set; }

        public SessionRequest Freeze() => new SessionRequest(Operation, Protocol, Epoch, PolicyRevision,
            RequestSequence, ActorIds, TargetId, X, Kind, Value);

        public void OnReturnToPool()
        {
            SenderObjectId = Protocol = Epoch = PolicyRevision = TargetId = Value = 0;
            RequestSequence = 0;
            Operation = default;
            ActorIds = null;
            X = 0;
            Kind = null;
        }
    }
}
