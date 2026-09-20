using System.Linq;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>远征冻结合同的有界网络字段；接收后立即复制，不能参与玩法计算。</summary>
    [MemoryPackable]
    public partial class ExpeditionActorWire
    {
        public int Id { get; set; }
        public double Oxygen { get; set; }
        public int Iron { get; set; }
        public int Gold { get; set; }
        public int Role { get; set; }
        public int TaskTarget { get; set; }
        public int TaskPhase { get; set; }
        public double TaskClock { get; set; }
        public int OwnerSlot { get; set; }
        public bool Boarded { get; set; }
        public static ExpeditionActorWire From(ExpeditionActorData v) => v == null ? null : new ExpeditionActorWire
        {
            Id = v.Id,
            Oxygen = v.Oxygen,
            Iron = v.Iron,
            Gold = v.Gold,
            Role = v.Role,
            TaskTarget = v.TaskTarget,
            TaskPhase = v.TaskPhase,
            TaskClock = v.TaskClock,
            OwnerSlot = v.OwnerSlot,
            Boarded = v.Boarded,
        };
        public ExpeditionActorData Freeze() => new ExpeditionActorData(Id, Oxygen, Iron, Gold, Role, TaskTarget, TaskPhase, TaskClock, OwnerSlot, Boarded);
    }
}
