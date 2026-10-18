using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>单巢一只幼虫的值状态；地图局部编号与冻结巢位对应，所有姿态和清理结果仅由TerrainGameplayBehaviour推进。</summary>
    [MemoryPackable]
    public partial struct NestCrawlerState
    {
        public int Id;
        public int Phase;
        public double Clock;
        public float X;
        public float Height;
        public float VerticalSpeed;
        public int Hp;
    }
}
