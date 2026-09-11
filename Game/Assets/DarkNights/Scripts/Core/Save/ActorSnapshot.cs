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
            double aiClock)
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
        }
    }
}
