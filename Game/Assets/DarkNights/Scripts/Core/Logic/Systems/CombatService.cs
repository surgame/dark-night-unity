using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Systems
{
    /// <summary>
    /// 统一索敌、攻击前摇和伤害规则。目标查询低频进行，伤害只在命中点结算；远程攻击转交ProjectileSystem后由它处理一次命中。
    /// </summary>
    public sealed class CombatService
    {
        private readonly GameSession session;

        public CombatService(GameSession session)
        {
            this.session = session;
        }

        public Actor NearestEnemy(float x, double reach)
        {
            Actor nearest = null;
            double distance = reach;
            foreach (var actor in session.World.Actors)
                if (actor.Enemy && actor.Hp > 0 && Math.Abs(actor.X - x) <= distance)
                {
                    distance = Math.Abs(actor.X - x);
                    nearest = actor;
                }
            return nearest;
        }

        internal Combatant FindTarget(Actor actor)
        {
            if (actor.ForcedAttack && session.World.Find<Combatant>(actor.TargetId) is { Hp: > 0 } forced)
                return forced;
            if (!actor.Enemy)
            {
                var enemy = NearestEnemy(actor.X, actor.Definition.Aggro);
                return enemy != null && (actor.Kind == "worker" || Math.Abs(enemy.X - actor.RallyX) < actor.Definition.Leash) ? enemy : null;
            }
            Combatant target = null;
            double distance = actor.Definition.Aggro;
            foreach (var citizen in session.World.Actors)
            {
                if (citizen.Enemy || citizen.Hp <= 0 || citizen.IsTraining)
                    continue;
                double gap = Math.Abs(citizen.X - actor.X);
                if (gap < distance)
                {
                    distance = gap;
                    target = citizen;
                }
            }
            if (target != null)
                return target;
            distance = double.PositiveInfinity;
            foreach (var building in session.World.Buildings)
            {
                double gap = Math.Abs(building.X - actor.X) - building.Definition.Width * 0.5;
                if (building.Hp > 0 && gap < distance)
                {
                    distance = gap;
                    target = building;
                }
            }
            return target;
        }

        internal void TickAttack(Actor actor, double delta)
        {
            var target = session.World.Find<Combatant>(actor.TargetId);
            if (target == null || target.Hp <= 0)
            {
                actor.ClearOrder();
                return;
            }
            double reach = actor.Definition.Range + (target is Building b ? b.Definition.Width * 0.5 : 0);
            actor.Face = target.X < actor.X ? -1 : 1;
            if (Math.Abs(target.X - actor.X) > reach)
            {
                actor.HitPending = false;
                actor.MoveTo((float)(target.X - actor.Face * (reach - 1)), delta);
                return;
            }
            if (actor.HitPending)
            {
                actor.Windup -= delta;
                if (actor.Windup > 0)
                    return;
                actor.HitPending = false;
                int damage = session.Random.RandiRange(actor.Definition.Damage[0], actor.Definition.Damage[1]);
                if (actor.Kind == "archer")
                    session.Projectiles.Launch(new(actor.X, session.GroundY - 10), target, damage);
                else
                {
                    Damage(target, damage);
                    session.Feedback.PlaySound("snd_hit_fleshy_light1", -16);
                }
            }
            else if (actor.AttackClock <= 0)
            {
                actor.AttackClock = actor.Definition.AttackSeconds;
                actor.Windup = actor.Definition.Windup;
                actor.HitPending = true;
                actor.ActionTime = 0;
            }
        }

        public void Damage(Combatant target, double incoming, bool ignoreArmor = false)
        {
            if (target.Hp <= 0)
                return;
            double armor = target is Actor actor && !ignoreArmor ? actor.Definition.Armor : 0;
            double damage = Math.Max(1, Math.Round(incoming - armor, MidpointRounding.AwayFromZero));
            target.Hp = Math.Max(0, target.Hp - damage);
            target.HitFlash = 0.15;
            session.Feedback.Emit(new("damage", target.X, session.GroundY - (target is Building ? 40 : 19),
                ((int)damage).ToString(), Enemy: target is Actor { Enemy: true }));
            if (target.Hp > 0)
                return;
            if (target is Actor victim)
                session.Lifecycle.ActorDied(victim);
            else if (target is Building building)
                session.Lifecycle.BuildingDestroyed(building);
        }
    }
}
