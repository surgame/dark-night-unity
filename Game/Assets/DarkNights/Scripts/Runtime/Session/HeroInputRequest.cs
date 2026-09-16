namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 从输入命令同步复制的不可变意图；没有玩家身份、坐标、速度、燃料或伤害字段。
    /// 序号在输入流内递增，Lease 对应本次接管，短按边沿独立于最终持有状态。
    /// </summary>
    public readonly struct HeroInputRequest
    {
        public readonly int Protocol, Epoch, PolicyRevision, ActorId, ControlLease, Horizontal;
        public readonly long Sequence, ObservedTick;
        public readonly bool JumpHeld, UseHeld, JumpPressed, DropPressed;
        public HeroInputRequest(int protocol, int epoch, int policyRevision, int actorId, int controlLease,
            long sequence, long observedTick, int horizontal, bool jumpHeld, bool useHeld, bool jumpPressed, bool dropPressed)
        {
            Protocol = protocol; Epoch = epoch; PolicyRevision = policyRevision; ActorId = actorId;
            ControlLease = controlLease; Sequence = sequence; ObservedTick = observedTick; Horizontal = horizontal;
            JumpHeld = jumpHeld; UseHeld = useHeld; JumpPressed = jumpPressed; DropPressed = dropPressed;
        }
    }
}
