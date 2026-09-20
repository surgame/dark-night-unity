using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>远征的冻结数据合同；仅供投影和保存，构造时复制集合，不拥有权威状态。</summary>
    public sealed class ExpeditionActorData
    {
        public int Id { get; }
        public double Oxygen { get; }
        public int Iron { get; }
        public int Gold { get; }
        public int Role { get; }
        public int TaskTarget { get; }
        public int TaskPhase { get; }
        public double TaskClock { get; }
        public int OwnerSlot { get; }
        public bool Boarded { get; }
        public ExpeditionActorData(int id, double oxygen, int iron, int gold, int role, int taskTarget, int taskPhase, double taskClock, int ownerSlot, bool boarded)
        {
            Id = id;
            Oxygen = oxygen;
            Iron = iron;
            Gold = gold;
            Role = role;
            TaskTarget = taskTarget;
            TaskPhase = taskPhase;
            TaskClock = taskClock;
            OwnerSlot = ownerSlot;
            Boarded = boarded;
        }
    }
}
