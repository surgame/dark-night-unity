using DarkNights.Samples.LanCoop.Core;
using GameCore.NetworkCommands;
using MemoryPack;

namespace DarkNights.Samples.LanCoop.Runtime
{
    /// <summary>固定大小的可靠请求；身份由 YYGC 连接上下文提供，SenderObjectId 仅用于本地入口定位。</summary>
    [MemoryPackable, NetworkCommand(CommandChannel.Reliable, CommandScope.ServerOnly)]
    public partial record CampCommand
    {
        public int SenderObjectId { get; set; }
        public int Protocol { get; set; }
        public int Epoch { get; set; }
        public long RequestSequence { get; set; }
        public SampleOperation Operation { get; set; }
        public int EntityId { get; set; }
        public void OnReturnToPool()
        {
            SenderObjectId = Protocol = Epoch = EntityId = 0;
            RequestSequence = 0;
            Operation = SampleOperation.Ready;
        }
    }
}
