using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 建筑状态中的一条已付款训练记录，以值保存单位身份、目标职业与剩余秒数。
    /// 没有独立生命周期；队列复制按值隔离，计时只由所属兵营能力推进。
    /// </summary>
    [MemoryPackable]
    public partial struct TrainingStateEntry
    {
        public int ActorId { get; set; }
        public string Kind { get; set; }
        public double Remaining { get; set; }

        public TrainingStateEntry(int actorId, string kind, double remaining)
        {
            ActorId = actorId;
            Kind = kind;
            Remaining = remaining;
        }
    }
}
