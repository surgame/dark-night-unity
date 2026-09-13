using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话箭矢状态中的按值飞行记录，目标、伤害和时长在发射时确定。
    /// ViewId 只服务本 epoch 的表现跟踪；命中前先移除记录，避免重复伤害。
    /// </summary>
    [MemoryPackable]
    public partial struct ProjectileFlight
    {
        public long ViewId { get; set; }
        public float FromX { get; set; }
        public float FromY { get; set; }
        public float ToX { get; set; }
        public float ToY { get; set; }
        public int TargetId { get; set; }
        public int Damage { get; set; }
        public double Age { get; set; }
        public double Duration { get; set; }
    }
}
