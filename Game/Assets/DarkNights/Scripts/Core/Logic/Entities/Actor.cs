using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Entities
{
    /// <summary>
    /// 居民或敌人的个人状态及移动/任务状态机，不负责绘制和文件访问。工作占用交给WorkOrders，目标查询和伤害交给CombatService，实体身份在转职时保持不变。
    /// </summary>
    public sealed class Actor : Combatant
    {
        private readonly GameSession _session;
        public UnitDefinition Definition { get; private set; }
        public override double MaximumHp => Definition.Hp;
        public bool Enemy { get; }
        public string Name { get; }
        public ActorActivity State { get; internal set; } = ActorActivity.Idle;
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
        public bool Walking { get; private set; }
        public bool IsTraining => State is ActorActivity.Training or ActorActivity.TrainingMove;

        internal Actor(GameSession session, int id, string kind, float x, bool enemy, string name) : base(id, kind, x)
        {
            _session = session;
            Definition = session.Catalog.Balance.Units[kind];
            Enemy = enemy;
            Name = string.IsNullOrEmpty(name) ? Definition.Name : name;
            Hp = Definition.Hp;
            MoveX = x;
            RallyX = x;
            Face = enemy ? -1 : 1;
            AiClock = id * 0.07 % 0.25;
        }

        public void ClearOrder() => _session.Work.Clear(this);

        public void OrderMove(float x)
        {
            if (IsTraining)
                return;
            ClearOrder();
            MoveX = Math.Clamp(x, 16, _session.Layout.WorldWidth - 16);
            RallyX = MoveX;
            State = ActorActivity.Move;
        }

        internal void Tick(double delta)
        {
            if (Hp <= 0)
                return;
            ActionTime += delta;
            AttackClock = Math.Max(0, AttackClock - delta);
            HitFlash = Math.Max(0, HitFlash - delta);
            AiClock = Math.Max(0, AiClock - delta);
            Walking = false;
            if (IsTraining)
            {
                var barracks = _session.World.Find<Building>(TargetId);
                if (barracks == null)
                    ClearOrder();
                else if (State == ActorActivity.TrainingMove && MoveTo(barracks.X + 10, delta))
                    State = ActorActivity.Training;
                return;
            }
            if (State is ActorActivity.WorkMove or ActorActivity.Work or ActorActivity.BuildMove or ActorActivity.Build)
            {
                var workplace = _session.World.Find(TargetId);
                int owner = workplace switch
                {
                    Worksite site => site.WorkerId,
                    Building building => building.WorkerId,
                    _ => 0
                };
                if (workplace == null || owner != Id)
                    ClearOrder();
                else if (State is ActorActivity.WorkMove or ActorActivity.BuildMove)
                {
                    if (MoveTo(workplace.X - 10, delta))
                    {
                        State = State == ActorActivity.WorkMove ? ActorActivity.Work : ActorActivity.Build;
                        ActionTime = 0;
                    }
                }
                else
                    Face = 1;
            }
            if (State == ActorActivity.Move)
            {
                if (MoveTo(MoveX, delta))
                    State = ActorActivity.Idle;
            }
            else if (AiClock <= 0)
            {
                AiClock = 0.25;
                FindTarget();
            }
            if (State == ActorActivity.Attack)
                _session.Combat.TickAttack(this, delta);
        }

        internal bool MoveTo(float x, double delta)
        {
            if (Math.Abs(X - x) < 0.8)
            {
                X = x;
                return true;
            }
            Face = Math.Sign(x - X);
            X = (float)SimulationMath.MoveToward((double)X, x, Definition.Speed * delta);
            Walking = true;
            return Math.Abs(X - x) < 0.8;
        }

        private void FindTarget()
        {
            var target = _session.Combat.FindTarget(this);
            if (target != null)
            {
                if (TargetId != target.Id || State != ActorActivity.Attack)
                {
                    ClearOrder();
                    TargetId = target.Id;
                    State = ActorActivity.Attack;
                }
            }
            else if (State == ActorActivity.Attack)
            {
                ClearOrder();
                if (!Enemy && Kind != "worker" && Math.Abs(X - RallyX) > 8)
                {
                    MoveX = RallyX;
                    State = ActorActivity.Move;
                }
            }
        }

        internal void Promote(string kind)
        {
            ClearOrder();
            Kind = kind;
            Definition = _session.Catalog.Balance.Units[kind];
            Hp = Definition.Hp;
            RallyX = X;
        }
    }
}
