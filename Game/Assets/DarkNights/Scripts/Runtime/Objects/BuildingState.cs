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
        public int ShipPhase { get; internal set; }
        public int PilotId { get; internal set; }
        public float ShipVelocityX { get; internal set; }
        public float ShipVelocityY { get; internal set; }
        public double ShipDoorClock { get; internal set; }
        public float DockX { get; internal set; }
        public float DockHeight { get; internal set; }
        public float Height { get; internal set; }
        public int CargoIron { get; internal set; }
        public int CargoGold { get; internal set; }
        public int DeviceStage { get; internal set; }
        public int ParentId { get; internal set; }
        public float TargetX { get; internal set; }
        public float TargetHeight { get; internal set; }
        public bool Powered { get; internal set; }
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
            ShipPhase = value.ShipPhase;
            PilotId = value.PilotId;
            ShipVelocityX = value.ShipVelocityX;
            ShipVelocityY = value.ShipVelocityY;
            ShipDoorClock = value.ShipDoorClock;
            DockX = value.DockX;
            DockHeight = value.DockHeight;
            Height = value.Height;
            CargoIron = value.CargoIron;
            CargoGold = value.CargoGold;
            DeviceStage = value.DeviceStage;
            ParentId = value.ParentId;
            TargetX = value.TargetX;
            TargetHeight = value.TargetHeight;
            Powered = value.Powered;
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
