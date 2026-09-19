using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// ProjectileState 固定槽位中的值记录；Kind=0 表示空闲，1 子弹、2 黏性炸弹、3 爆炸残留。
    /// 坐标采用世界像素 X 和向上的 Height；爆炸先转为无伤害残留，保证只结算一次。
    /// </summary>
    [MemoryPackable]
    public partial struct BallisticFlight
    {
        public long ViewId { get; set; }
        public int Kind { get; set; }
        public float X { get; set; }
        public float Height { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public float Gravity { get; set; }
        public float Radius { get; set; }
        public float BlastRadius { get; set; }
        public int Damage { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; set; }
        public bool Stuck { get; set; }
    }
}
