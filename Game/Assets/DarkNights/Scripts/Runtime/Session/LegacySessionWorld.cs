using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Save;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 未迁移正式入口的临时整局适配，历史规则测试仍可独立运行；新对象入口绝不创建此类型。
    /// 只委托原模型，不参与新 Behaviour 的任何操作；U5 连同旧 GameSession 一起删除。
    /// </summary>
    internal sealed class LegacySessionWorld : SessionWorld
    {
        private GameSession game;
        private readonly Dictionary<Projectile, long> projectiles = new Dictionary<Projectile, long>();
        private long nextProjectile;
        public override GameCatalog Catalog { get; }
        public override LevelLayout Layout { get; }
        public override SessionFeedback Feedback { get; }
        public override bool Paused => game.Paused;
        public override int Speed => (int)game.Speed;
        public override double Elapsed => game.Elapsed;

        public LegacySessionWorld(GameCatalog catalog, LevelLayout layout)
        {
            Catalog = catalog;
            Layout = layout;
            Feedback = new SessionFeedback();
        }
        private LegacySessionWorld(GameSession game)
        {
            this.game = game;
            Catalog = game.Catalog;
            Layout = game.Layout;
            Feedback = game.Feedback;
        }
        public override void Activate() { game = game ?? new GameSession(Catalog, Layout, Feedback); }
        public override bool ValidRequest(SessionRequest request) => SessionOperations.ValidWorld(game, request);
        public override int Apply(SessionRequest request, out int entityId) => SessionOperations.Apply(game, request, out entityId);
        public override void Advance(double seconds) => game.Advance(seconds);
        public override SessionSnapshot CaptureWorld() => SnapshotMapper.Capture(game);
        public override SessionWorld Restore(string json) => new LegacySessionWorld(new GameSaveJson(Catalog, Layout).Restore(json));
        public override SessionWorld Restart() => new LegacySessionWorld(new GameSession(Catalog, Layout));
        public override void Dispose() { projectiles.Clear(); }

        public override WorldViewData CaptureView()
        {
            var world = game.World;
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
            return new WorldViewData(camp, actors, buildings, sites, arrows);
        }
    }
}
