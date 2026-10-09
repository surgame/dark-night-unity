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
        public int CargoIron { get; internal set; }
        public int CargoGold { get; internal set; }
        public int ExpeditionRole { get; internal set; }
        public int TaskTarget { get; internal set; }
        public int TaskPhase { get; internal set; }
        public double TaskClock { get; internal set; }
        public int OwnerSlot { get; internal set; } = -1;
        public bool Boarded { get; internal set; }
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
        public int InventoryRevision { get; internal set; }
        public string Slot0 { get; internal set; } = "";
        public string Slot1 { get; internal set; } = "";
        public string Slot2 { get; internal set; } = "";
        public string Slot3 { get; internal set; } = "";
        public bool JetpackOwned { get; internal set; }
        public bool JetpackEquipped { get; internal set; }
        public double JetpackFuel { get; internal set; }
        public int ExplosiveCharges { get; internal set; }
        public long LastTerrainActionTick { get; internal set; } = -1000;
        public float AimAngle { get; internal set; }
        public string LightDefinition { get; internal set; } = "";
        public bool LightEnabled { get; internal set; }
        public float LightAimAngle { get; internal set; }
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
        public bool SprintHeld { get; internal set; }
        public bool JumpHeld { get; internal set; }
        public bool UseHeld { get; internal set; }
        public bool JumpPending { get; internal set; }
        // 只属于当前权威输入生命周期；不发送或保存，撤销控制、传送和恢复时归零。
        [MemoryPackIgnore] public double JumpBufferRemaining { get; internal set; }
        [MemoryPackIgnore] public bool JumpAscending { get; internal set; }
        public bool DropPending { get; internal set; }
        public bool ShipEntryBlocked { get; internal set; }
        public string MiningWorldId { get; internal set; }
        public ulong MiningMapEpoch { get; internal set; }
        public int MiningU { get; internal set; }
        public int MiningV { get; internal set; }
        public uint MiningTileId { get; internal set; }
        public ushort MiningFlags { get; internal set; }
        public DarkNights.Core.ViewData.HeroMiningTargetKind MiningTargetKind { get; internal set; }
        public int MiningEntityId { get; internal set; }
        public ulong MiningContentVersion { get; internal set; }
        public ulong MiningMineralContentVersion { get; internal set; }
        // 单镐的冻结意图和命中门闩只属于当前权威输入生命周期；展示沿用装备动作，不保存或发送这些临时字段。
        [MemoryPackIgnore] public DarkNights.Core.ViewData.HeroMiningTarget PickaxeSwingTarget { get; internal set; }
        [MemoryPackIgnore] public float PickaxeSwingAim { get; internal set; }
        [MemoryPackIgnore] public string MiningToolDefinition { get; internal set; } = "";
        [MemoryPackIgnore] public int MiningToolSelectionRevision { get; internal set; }
        [MemoryPackIgnore] public bool PickaxeSwingActive { get; internal set; }
        [MemoryPackIgnore] public bool PickaxeHitPending { get; internal set; }
    }
}
