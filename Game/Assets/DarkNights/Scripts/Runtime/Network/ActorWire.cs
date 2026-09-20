using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// ActorViewData 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
    [MemoryPackable]
    public partial class ActorWire
    {
        public int Id { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public bool Enemy { get; set; }
        public float X { get; set; }
        public double Hp { get; set; }
        public string Activity { get; set; }
        public int TargetId { get; set; }
        public float Face { get; set; }
        public bool Walking { get; set; }
        public double ActionTime { get; set; }
        public double Windup { get; set; }
        public double HitFlash { get; set; }
        public float Height { get; set; }
        public float VerticalSpeed { get; set; }
        public int SupportPlatform { get; set; }
        public bool ManualControl { get; set; }
        public int SelectedItem { get; set; }
        public int SelectionRevision { get; set; }
        public bool JetpackEquipped { get; set; }
        public double JetpackFuel { get; set; }
        public int ExplosiveCharges { get; set; }
        public int ControllerSlot { get; set; } = -1;
        public int ControlLease { get; set; }

        public float AimAngle { get; set; }
        public double EquipmentCooldown { get; set; }
        public double EquipmentAction { get; set; }
        public double EquipmentActionDuration { get; set; }
        public bool Charging { get; set; }
        public double ChargeSeconds { get; set; }

        public static ActorWire From(ActorViewData value) => new ActorWire
        {
            Id = value.Id,
            Kind = value.Kind,
            Name = value.Name,
            Enemy = value.Enemy,
            X = value.X,
            Hp = value.Hp,
            Activity = value.Activity,
            TargetId = value.TargetId,
            Face = value.Face,
            Walking = value.Walking,
            ActionTime = value.ActionTime,
            Windup = value.Windup,
            HitFlash = value.HitFlash,
            Height = value.Height,
            VerticalSpeed = value.VerticalSpeed,
            SupportPlatform = value.SupportPlatform,
            ManualControl = value.ManualControl,
            SelectedItem = value.SelectedItem,
            SelectionRevision = value.SelectionRevision,
            JetpackEquipped = value.JetpackEquipped,
            JetpackFuel = value.JetpackFuel,
            ExplosiveCharges = value.ExplosiveCharges,
            ControllerSlot = value.ControllerSlot,
            ControlLease = value.ControlLease,
            AimAngle = value.AimAngle,
            EquipmentCooldown = value.EquipmentCooldown,
            EquipmentAction = value.EquipmentAction,
            EquipmentActionDuration = value.EquipmentActionDuration,
            Charging = value.Charging,
            ChargeSeconds = value.ChargeSeconds,

        };

        public ActorViewData Freeze() => new ActorViewData(
            Id,
            Kind,
            Name,
            Enemy,
            X,
            Hp,
            Activity,
            TargetId,
            Face,
            Walking,
            ActionTime,
            Windup,
            HitFlash,
            Height,
            VerticalSpeed,
            SupportPlatform,
            ManualControl,
            SelectedItem,
            SelectionRevision,
            JetpackEquipped,
            JetpackFuel,
            ControllerSlot,
            ControlLease, ExplosiveCharges, AimAngle, EquipmentCooldown, EquipmentAction, EquipmentActionDuration, Charging, ChargeSeconds);
    }
}
