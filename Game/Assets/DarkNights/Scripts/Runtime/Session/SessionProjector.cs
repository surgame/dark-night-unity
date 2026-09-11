using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 在权威线程将真实世界映射成冻结展示帧，不绕经完整存档，不修改规则或随机数。
    /// 每个会话独占发布序号及箭矢身份表；载入新 epoch 清空箭矢引用，消失的箭矢每次采集时移除。
    /// </summary>
    internal sealed class SessionProjector
    {
        private readonly Dictionary<Projectile, long> projectiles = new Dictionary<Projectile, long>();
        private long nextProjectile;
        private long publication;
        private int epoch;

        public SessionViewData Capture(SessionAuthority session, GameSession game, IReadOnlyList<PresentationEvent> events,
            IReadOnlyList<PresentationEvent> remnants)
        {
            WorldState world = game.World;
            if ((long)world.Actors.Count + world.Buildings.Count + world.Worksites.Count > WorldViewData.MaximumEntities ||
                world.Projectiles.Count > WorldViewData.MaximumProjectiles)
                throw new InvalidOperationException("World exceeds full projection limits; never truncate entities.");
            if (epoch != session.Epoch)
            {
                projectiles.Clear();
                nextProjectile = 0;
                epoch = session.Epoch;
            }
            var live = new HashSet<Projectile>(world.Projectiles);
            foreach (Projectile old in projectiles.Keys.ToArray())
                if (!live.Contains(old)) projectiles.Remove(old);
            var arrows = new List<ProjectileViewData>(world.Projectiles.Count);
            foreach (Projectile arrow in world.Projectiles)
            {
                if (!projectiles.TryGetValue(arrow, out long id))
                {
                    id = checked(++nextProjectile);
                    projectiles.Add(arrow, id);
                }
                arrows.Add(new ProjectileViewData(id, arrow.From.X, arrow.From.Y, arrow.To.X, arrow.To.Y, arrow.Age, arrow.Duration));
            }
            var actors = world.Actors.Select(a => new ActorViewData(a.Id, a.Kind, a.Name, a.Enemy, a.X, a.Hp,
                a.State.ToString(), a.TargetId, a.Face, a.Walking, a.ActionTime, a.Windup, a.HitFlash)).ToArray();
            var buildings = world.Buildings.Select(b => new BuildingViewData(b.Id, b.Kind, b.X, b.Hp, b.Progress,
                b.WorkerId, b.FarmSiteId, b.HitFlash, b.TrainingQueue.Select(t =>
                    new TrainingViewData(t.ActorId, t.Kind, t.Remaining)).ToArray())).ToArray();
            var sites = world.Worksites.Select(w => new WorksiteViewData(w.Id, w.Kind, w.X,
                w.WorkerId, w.Amount, w.Progress, w.Variant, w.FarmId)).ToArray();
            var camp = new CampViewData(game.Economy.Stock, game.Economy.Population, game.Economy.Capacity,
                game.Economy.RecruitCooldown, game.Waves.Index, game.Waves.Phase.ToString(), game.Waves.DayRemaining,
                world.EnemyCount, game.Mode.ToString(), game.Stats.Kills, game.Stats.Lost, game.Stats.Gathered, game.Waves.NextSpawn);
            var view = new WorldViewData(camp, actors, buildings, sites, arrows);
            return new SessionViewData(checked(++publication), session.Epoch, session.Revision, session.ServerTick,
                session.PolicyRevision, session.ControlMode == CampControlMode.HostOnly, session.PlayerCount,
                session.ReadyCount, session.Loading, game.Paused, (int)game.Speed, game.Elapsed, view, events, remnants);
        }

        public void Clear()
        {
            projectiles.Clear();
        }
    }
}
