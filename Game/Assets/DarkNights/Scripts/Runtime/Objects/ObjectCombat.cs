using System;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 本局 YYGC 能力之间的目标查询与伤害协调，不保存生命或目标引用图。
    /// 同距离取舍、护甲舍入和死亡清理在当前同步事务内完成。
    /// </summary>
    public sealed class ObjectCombat
    {
        private readonly ObjectSession session;
        internal ObjectCombat(ObjectSession session) { this.session = session; }

        internal static float Height(ICombatantCapability target) => target is ActorBehaviour actor ? actor.Read().Height : 0;
        internal bool InRange(ActorBehaviour actor, ICombatantCapability target)
        {
            double width = target is BuildingBehaviour b ? b.Definition.Width * 0.5 : 0;
            double x = Math.Max(0, Math.Abs(target.X - actor.X) - width);
            double y = Height(target) - actor.Read().Height;
            return x * x + y * y <= actor.Definition.Range * actor.Definition.Range;
        }

        internal ActorBehaviour NearestEnemy(float x, double reach)
        {
            ActorBehaviour nearest = null;
            double distance = reach;
            foreach (ActorBehaviour actor in session.Index.Actors)
                if (actor.Enemy && actor.Hp > 0 && Math.Abs(actor.X - x) <= distance)
                {
                    distance = Math.Abs(actor.X - x);
                    nearest = actor;
                }
            return nearest;
        }

        internal ICombatantCapability FindTarget(ActorBehaviour actor)
        {
            ActorState state = actor.Read();
            ICombatantCapability forced = session.Index.Find<ICombatantCapability>(state.TargetId);
            if (state.ForcedAttack && forced != null && forced.Hp > 0) return forced;
            if (!actor.Enemy)
            {
                ActorBehaviour enemy = NearestEnemy(actor.X, actor.Definition.Aggro);
                return enemy != null && (actor.RuleKey == "worker" ||
                    Math.Abs(enemy.X - state.RallyX) < actor.Definition.Leash) ? enemy : null;
            }
            ICombatantCapability target = null;
            double distance = actor.Definition.Aggro;
            foreach (ActorBehaviour citizen in session.Index.Actors)
            {
                if (citizen.Enemy || citizen.Hp <= 0 || citizen.IsTraining) continue;
                double gap = Math.Sqrt(Math.Pow(citizen.X - actor.X, 2) + Math.Pow(citizen.Read().Height - state.Height, 2));
                if (gap < distance) { distance = gap; target = citizen; }
            }
            if (target != null) return target;
            distance = double.PositiveInfinity;
            foreach (BuildingBehaviour building in session.Index.Buildings)
            {
                double gap = Math.Abs(building.X - actor.X) - building.Definition.Width * 0.5;
                if (building.Hp > 0 && gap < distance) { distance = gap; target = building; }
            }
            return target;
        }

        internal void Damage(ICombatantCapability target, double incoming, bool ignoreArmor = false)
        {
            session.Mutations.RequireWriting();
            if (target.Hp <= 0) return;
            double armor = target is ActorBehaviour actor && !ignoreArmor ? actor.Definition.Armor : 0;
            double damage = Math.Max(1, Math.Round(incoming - armor, MidpointRounding.AwayFromZero));
            if (target is ActorBehaviour unit)
            {
                unit.Edit().Hp = Math.Max(0, unit.Hp - damage);
                unit.Edit().HitFlash = 0.15;
            }
            else if (target is BuildingBehaviour building)
            {
                building.Edit().Hp = Math.Max(0, building.Hp - damage);
                building.Edit().HitFlash = 0.15;
            }
            session.Emit(new VisualCue("damage", target.X, session.Layout.GroundY - Height(target) - (target is BuildingBehaviour ? 40 : 19),
                ((int)damage).ToString(), Enemy: target is ActorBehaviour enemy && enemy.Enemy));
            if (target.Hp > 0) return;
            if (target is ActorBehaviour victim) session.Lifecycle.ActorDied(victim);
            else session.Lifecycle.BuildingDestroyed((BuildingBehaviour)target);
        }
    }
}
