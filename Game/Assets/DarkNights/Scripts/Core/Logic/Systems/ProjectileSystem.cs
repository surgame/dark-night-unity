using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Logic.Entities;


namespace DarkNights.Core.Logic.Systems
{
    /// <summary>
    /// 拥有箭矢从发射到消耗的模拟过程。命中时先移除记录，再对仍存活的目标结算；存档保留其进度，死亡目标的历史ID可以安全失效。
    /// </summary>
    public sealed class ProjectileSystem
    {
        private readonly GameSession session;

        public ProjectileSystem(GameSession session)
        {
            this.session = session;
        }

        public void Launch(WorldPoint from, Combatant target, int damage)
        {
            var to = new WorldPoint(target.X, session.GroundY - 9);
            session.World.Projectiles.Add(new(from, to, target.Id, damage, Math.Max(0.15, Math.Abs(target.X - from.X) / 180.0)));
        }

        internal void Tick(double delta)
        {
            foreach (var shot in session.World.Projectiles.ToArray())
            {
                shot.Age += delta;
                var target = session.World.Find<Combatant>(shot.TargetId);
                if (target != null)
                    shot.To = new(target.X, session.GroundY - 9);
                if (shot.Age < shot.Duration)
                    continue;
                session.World.Projectiles.Remove(shot);
                if (target is { Hp: > 0 })
                    session.Combat.Damage(target, shot.Damage);
            }
        }
    }
}
