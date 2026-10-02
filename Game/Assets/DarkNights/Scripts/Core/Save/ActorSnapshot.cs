using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 单位持久状态的传输记录，保留任务、攻击前摇及AI计时。外观闪烁和当前帧绘制标志可以重建，不进入存档。
    /// </summary>
    public sealed class ActorSnapshot
    {
        public int Id { get; }
        public string Kind { get; }
        public bool Enemy { get; }
        public string Name { get; }
        public double X { get; }
        public double Hp { get; }
        public ActorActivity State { get; }
        public int TargetId { get; }
        public double MoveX { get; }
        public double RallyX { get; }
        public double Face { get; }
        public double ActionTime { get; }
        public double AttackClock { get; }
        public double Windup { get; }
        public bool HitPending { get; }
        public bool ForcedAttack { get; }
        public double AiClock { get; }
        public float Height { get; }
        public float VerticalSpeed { get; }
        public int SupportPlatform { get; }
        public int IgnoredPlatform { get; }
        public double DropRemaining { get; }
        public bool ManualControl { get; }
        public int SelectedItem { get; }
        public int SelectionRevision { get; }
        public int InventoryRevision { get; }
        public string Slot0 { get; }
        public string Slot1 { get; }
        public string Slot2 { get; }
        public string Slot3 { get; }
        public bool JetpackOwned { get; }
        public bool JetpackEquipped { get; }
        public double JetpackFuel { get; }
        public int ExplosiveCharges { get; }

        public float AimAngle { get; }
        public double EquipmentCooldown { get; }
        public double EquipmentAction { get; }
        public double EquipmentActionDuration { get; }

        public ActorSnapshot(
            int id,
            string kind,
            bool enemy,
            string name,
            double x,
            double hp,
            ActorActivity state,
            int targetId,
            double moveX,
            double rallyX,
            double face,
            double actionTime,
            double attackClock,
            double windup,
            bool hitPending,
            bool forcedAttack,
            double aiClock,
            float height = 0,
            float verticalSpeed = 0,
            int supportPlatform = 0,
            int ignoredPlatform = 0,
            double dropRemaining = 0,
            bool manualControl = false,
            int selectedItem = 0,
            int selectionRevision = 0,
            bool jetpackEquipped = false,
            double jetpackFuel = 0,
            int explosiveCharges = 0,
            float aimAngle = 0,
            double equipmentCooldown = 0,
            double equipmentAction = 0,
            double equipmentActionDuration = 0, int inventoryRevision = 0,
            string slot0 = "", string slot1 = "", string slot2 = "", string slot3 = "", bool jetpackOwned = false)
        {
            Id = id;
            Kind = kind;
            Enemy = enemy;
            Name = name;
            X = x;
            Hp = hp;
            State = state;
            TargetId = targetId;
            MoveX = moveX;
            RallyX = rallyX;
            Face = face;
            ActionTime = actionTime;
            AttackClock = attackClock;
            Windup = windup;
            HitPending = hitPending;
            ForcedAttack = forcedAttack;
            AiClock = aiClock;
            Height = height;
            VerticalSpeed = verticalSpeed;
            SupportPlatform = supportPlatform;
            IgnoredPlatform = ignoredPlatform;
            DropRemaining = dropRemaining;
            ManualControl = manualControl;
            SelectedItem = selectedItem;
            SelectionRevision = selectionRevision;
            InventoryRevision = inventoryRevision;
            Slot0 = slot0; Slot1 = slot1; Slot2 = slot2; Slot3 = slot3;
            JetpackOwned = jetpackOwned;
            JetpackEquipped = jetpackEquipped;
            JetpackFuel = jetpackFuel;
            ExplosiveCharges = explosiveCharges;
            AimAngle = aimAngle;
            EquipmentCooldown = equipmentCooldown;
            EquipmentAction = equipmentAction;
            EquipmentActionDuration = equipmentActionDuration;

        }
    }
}
