using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 从输入命令同步复制的不可变意图；采矿格坐标仅是目标，不能指定身份、速度、燃料、伤害或奖励。
    /// 序号在输入流内递增，Lease 对应本次接管，短按边沿独立于最终持有状态。
    /// </summary>
    public readonly struct HeroInputRequest
    {
        public readonly int Protocol, Epoch, PolicyRevision, ActorId, ControlLease, Horizontal;
        public readonly long Sequence, ObservedTick;
        public readonly bool JumpHeld, UseHeld, JumpPressed, DropPressed;
        public readonly float AimAngle;
        public readonly int SelectionRevision;
        public readonly bool UsePressed;
        public readonly bool UseReleased;
        public readonly bool CancelUse;
        public readonly bool SprintHeld;
        public readonly HeroMiningTarget Mining;
        public HeroInputRequest(int protocol, int epoch, int policyRevision, int actorId, int controlLease,
            long sequence, long observedTick, int horizontal, bool jumpHeld, bool useHeld, bool jumpPressed, bool dropPressed, float aimAngle = 0, int selectionRevision = 0, bool usePressed = false, bool useReleased = false, bool cancelUse = false, bool sprintHeld = false, HeroMiningTarget mining = default)
        {
            Protocol = protocol; Epoch = epoch; PolicyRevision = policyRevision; ActorId = actorId;
            ControlLease = controlLease; Sequence = sequence; ObservedTick = observedTick; Horizontal = horizontal;
            AimAngle = aimAngle;
            SelectionRevision = selectionRevision;
            UsePressed = usePressed;
            UseReleased = useReleased;
            CancelUse = cancelUse;
            JumpHeld = jumpHeld; UseHeld = useHeld; JumpPressed = jumpPressed; DropPressed = dropPressed;
            SprintHeld = sprintHeld;
            Mining = mining;
        }
    }
}
