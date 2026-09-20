using DarkNights.Core.Logic.State;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 单个 YYGC 单位的唯一运行状态；身份、移动和任务计时随对象会话一起退休。
    /// 字符串不可变，数值按值复制；展示和保存使用冻结副本，不持有状态池中的实例。
    /// </summary>
    [MemoryPackable, StateData]
    public partial record ActorState
    {
        public uint Sequence { get; set; }
        public int Id { get; internal set; }
        public string PlacementKey { get; internal set; }
        public string Name { get; internal set; }
        public bool Enemy { get; internal set; }
        public float X { get; internal set; }
        public double Hp { get; internal set; }
        public ActorActivity Activity { get; internal set; }
        public int TargetId { get; internal set; }
        public float MoveX { get; internal set; }
        public float RallyX { get; internal set; }
        public float Face { get; internal set; }
        public double ActionTime { get; internal set; }
        public double AttackClock { get; internal set; }
        public double Windup { get; internal set; }
        public bool HitPending { get; internal set; }
        public bool ForcedAttack { get; internal set; }
        public double AiClock { get; internal set; }
        public bool Walking { get; internal set; }
        public double HitFlash { get; internal set; }
        public float Height { get; internal set; }
        public float VerticalSpeed { get; internal set; }
        public int SupportPlatform { get; internal set; }
        public int IgnoredPlatform { get; internal set; }
        public double DropRemaining { get; internal set; }
        public bool ManualControl { get; internal set; }
        public int SelectedItem { get; internal set; }
        public int SelectionRevision { get; internal set; }
        public bool JetpackEquipped { get; internal set; }
        public double JetpackFuel { get; internal set; }
        public int ExplosiveCharges { get; internal set; }
        public long LastTerrainActionTick { get; internal set; } = -1000;
        public float AimAngle { get; internal set; }
        public double EquipmentCooldown { get; internal set; }
        public double EquipmentAction { get; internal set; }
        public double EquipmentActionDuration { get; internal set; }
        public bool Charging { get; internal set; }
        public double ChargeSeconds { get; internal set; }
        public bool UsePressed { get; internal set; }
        public bool UseReleased { get; internal set; }
        // 占用及输入只属于本次连接和 epoch，不写入存档。
        public int ControllerSlot { get; internal set; } = -1;
        public int ControllerGeneration { get; internal set; }
        public int ControlLease { get; internal set; }
        public long LastInputSequence { get; internal set; }
        public long LastInputTick { get; internal set; }
        public int Horizontal { get; internal set; }
        public bool JumpHeld { get; internal set; }
        public bool UseHeld { get; internal set; }
        public bool JumpPending { get; internal set; }
        public bool DropPending { get; internal set; }
    }
}
