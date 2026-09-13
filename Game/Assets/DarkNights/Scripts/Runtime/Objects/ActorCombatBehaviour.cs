using System;
using DarkNights.Core.Logic.State;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 所有职业共用的索敌、追击和攻击前摇；读写所属 ActorState，不建立第二套战斗实体。
    /// 低频索敌、强制目标、警戒半径及回防位置保留原顺序，命中方式由定义装配。
    /// </summary>
    public sealed partial class ActorCombatBehaviour : PooledBehaviour, IActorCombatCapability
    {
        [Inject] private ActorBehaviour actor;
        [Inject] private IMovementCapability movement;
        [Inject] private IAttackCapability attack;

        protected override void OnSpawn()
        {
            if (actor == null || movement == null || attack == null)
                throw new InvalidOperationException("Actor combat requires state, movement and one attack capability.");
        }

        public void FindTarget()
        {
            ObjectSession session = actor.World;
            ICombatantCapability target = session.Combat.FindTarget(actor);
            if (target != null)
            {
                if (actor.TargetId != target.Id || actor.Activity != ActorActivity.Attack)
                {
                    session.Work.Clear(actor);
                    actor.Edit().TargetId = target.Id;
                    actor.Edit().Activity = ActorActivity.Attack;
                }
            }
            else if (actor.Activity == ActorActivity.Attack)
            {
                session.Work.Clear(actor);
                ActorState state = actor.Edit();
                if (!actor.Enemy && actor.RuleKey != "worker" && Math.Abs(actor.X - state.RallyX) > 8)
                {
                    state.MoveX = state.RallyX;
                    state.Activity = ActorActivity.Move;
                }
            }
        }

        public void TickAttack(double delta)
        {
            ObjectSession session = actor.World;
            ICombatantCapability target = session.Index.Find<ICombatantCapability>(actor.TargetId);
            if (target == null || target.Hp <= 0) { session.Work.Clear(actor); return; }
            double reach = actor.Definition.Range + (target is BuildingBehaviour b ? b.Definition.Width * 0.5 : 0);
            ActorState state = actor.Edit();
            state.Face = target.X < actor.X ? -1 : 1;
            if (Math.Abs(target.X - actor.X) > reach)
            {
                state.HitPending = false;
                movement.MoveTo((float)(target.X - state.Face * (reach - 1)), delta);
                return;
            }
            if (state.HitPending)
            {
                state.Windup -= delta;
                if (state.Windup > 0) return;
                state.HitPending = false;
                int damage = session.Camp.RandomInt(actor.Definition.Damage[0], actor.Definition.Damage[1]);
                attack.Hit(target, damage);
            }
            else if (state.AttackClock <= 0)
            {
                state.AttackClock = actor.Definition.AttackSeconds;
                state.Windup = actor.Definition.Windup;
                state.HitPending = true;
                state.ActionTime = 0;
            }
        }
    }
}
