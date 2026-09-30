using DarkNights.Runtime.Session;
using GameCore.NetworkCommands;
using MemoryPack;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 主角输入的有界可靠命令；变化和保活采样复用 YYGC Gateway 与可信 Sender 校验。
    /// 与营地离散请求分流，不产生逐包回执或完整世界发布；按键边沿由输入序号去重。
    /// </summary>
    [MemoryPackable, NetworkCommand(CommandChannel.Reliable, CommandScope.ServerOnly)]
    public partial record HeroInputCommand
    {
        public int SenderObjectId { get; set; }
        public int Protocol { get; set; }
        public int Epoch { get; set; }
        public int PolicyRevision { get; set; }
        public int ActorId { get; set; }
        public int ControlLease { get; set; }
        public long InputSequence { get; set; }
        public long ObservedTick { get; set; }
        public int Horizontal { get; set; }
        public bool JumpHeld { get; set; }
        public bool UseHeld { get; set; }
        public bool JumpPressed { get; set; }
        public bool DropPressed { get; set; }
        public bool SprintHeld { get; set; }

        public float AimAngle { get; set; }
        public int SelectionRevision { get; set; }
        public bool UsePressed { get; set; }
        public bool UseReleased { get; set; }
        public bool CancelUse { get; set; }
        public string MiningWorldId { get; set; }
        public ulong MiningMapEpoch { get; set; }
        public int MiningU { get; set; }
        public int MiningV { get; set; }
        public uint MiningTileId { get; set; }
        public ushort MiningFlags { get; set; }

        public HeroInputRequest Freeze() => new HeroInputRequest(Protocol, Epoch, PolicyRevision, ActorId,
            ControlLease, InputSequence, ObservedTick, Horizontal, JumpHeld, UseHeld, JumpPressed, DropPressed, AimAngle,
            SelectionRevision, UsePressed, UseReleased, CancelUse, SprintHeld,
            new HeroMiningTarget(MiningWorldId, MiningMapEpoch, MiningU, MiningV, MiningTileId, MiningFlags));
        public void OnReturnToPool()
        {
            SenderObjectId = Protocol = Epoch = PolicyRevision = ActorId = ControlLease = Horizontal = 0;
            InputSequence = ObservedTick = 0;
            AimAngle = 0;
            SelectionRevision = 0;
            UsePressed = false;
            UseReleased = false;
            CancelUse = false;
            MiningWorldId = null; MiningMapEpoch = 0; MiningU = MiningV = 0; MiningTileId = 0; MiningFlags = 0;

            JumpHeld = UseHeld = JumpPressed = DropPressed = SprintHeld = false;
        }
    }
}
