using System;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 单个 YYGC 建筑拥有的生命、施工和双向占用状态；规则数值始终来自只读配置。
    /// 地基创建与支付在同一会话事务提交，准备失败时不会成为活动建筑。
    /// </summary>
    [MemoryPackable, StateData]
    public partial record BuildingState
    {
        public uint Sequence { get; set; }
        public int Id { get; internal set; }
        public string PlacementKey { get; internal set; }
        public float X { get; internal set; }
        public double Hp { get; internal set; }
        public double Progress { get; internal set; }
        public int WorkerId { get; internal set; }
        public int FarmSiteId { get; internal set; }
        public double AttackClock { get; internal set; }
        public double HitFlash { get; internal set; }
        public TrainingStateEntry[] TrainingQueue { get; internal set; } = Array.Empty<TrainingStateEntry>();

        public void CopyFrom(IStateData source)
        {
            if (!(source is BuildingState value)) throw new ArgumentException("Expected building state.", nameof(source));
            Sequence = value.Sequence;
            Id = value.Id;
            PlacementKey = value.PlacementKey;
            X = value.X;
            Hp = value.Hp;
            Progress = value.Progress;
            WorkerId = value.WorkerId;
            FarmSiteId = value.FarmSiteId;
            AttackClock = value.AttackClock;
            HitFlash = value.HitFlash;
            TrainingQueue = value.TrainingQueue == null ? Array.Empty<TrainingStateEntry>() :
                (TrainingStateEntry[])value.TrainingQueue.Clone();
        }
    }
}
