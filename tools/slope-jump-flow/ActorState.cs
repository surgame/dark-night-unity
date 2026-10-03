namespace DarkNights.Runtime.Objects
{
    /// <summary>独立源码验证的最小状态适配；正式角色仍使用 YYGC ActorState，此适配不进入 Unity 或 Player。</summary>
    public sealed class ActorState
    {
        public float X { get; set; }
        public float Height { get; set; }
        public float VerticalSpeed { get; set; }
        public bool ManualControl { get; set; } = true;
        public bool JetpackOwned { get; set; }
        public bool JetpackEquipped { get; set; }
        public double JetpackFuel { get; set; }
        public double DropRemaining { get; set; }
        public int SupportPlatform { get; set; }
        public int IgnoredPlatform { get; set; }
        public double JumpBufferRemaining { get; set; }
        public bool JumpAscending { get; set; }
    }
}
