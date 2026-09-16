using GameCore.NetworkCommands;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 请求切换当前连接在指定世界 epoch 中的 Ready 状态。
    /// 玩家身份只能由服务端连接上下文取得；序号用于服务端去重，SenderObjectId 仅定位本地 YYGC 命令入口。
    /// </summary>
    [MemoryPackable, NetworkCommand(CommandChannel.Reliable, CommandScope.ServerOnly)]
    public partial record SetReadyCommand
    {
        public int SenderObjectId { get; set; }
        public int Protocol { get; set; }
        public int Epoch { get; set; }
        public long RequestSequence { get; set; }
        public bool Ready { get; set; }
        public bool RequestHero { get; set; }
        public int AppliedRevision { get; set; }
        public long AppliedPublication { get; set; }
        public string RecoveryToken { get; set; } = "";

        public void OnReturnToPool()
        {
            SenderObjectId = 0;
            Protocol = 0;
            Epoch = 0;
            RequestSequence = 0;
            Ready = false;
            RequestHero = false;
            AppliedRevision = 0;
            AppliedPublication = 0;
            RecoveryToken = "";
        }
    }
}
