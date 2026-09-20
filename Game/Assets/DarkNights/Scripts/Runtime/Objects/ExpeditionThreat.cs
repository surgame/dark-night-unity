using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>远征警戒驱动的有限敌人与炮塔；只在合法洞穴位置出生，二维射线通过后才结算伤害。</summary>
    public sealed class ExpeditionThreat
    {
        private readonly ObjectSession world;
        internal ExpeditionThreat(ObjectSession world) { this.world = world; }
        internal void Tick(double delta)
        {
            var c = world.Camp.Edit();
            double interval = world.Catalog.Balance.Expedition.ThreatSeconds;
            if (c.ExpeditionRisk >= interval && world.Index.Actors.Count(a => a.Enemy) < 6)
            {
                var spot = world.Index.MineralDeposits.FirstOrDefault(d =>
                    ExpeditionNavigation.Sight(world.Terrain.Map, d.X, Height((MineralDepositBehaviour)d) + 1, d.X, Height((MineralDepositBehaviour)d) + 24));
                if (spot != null)
                {
                    var enemy = world.Lifecycle.SpawnActor("zombie", spot.X, true);
                    enemy.Edit().Height = Height((MineralDepositBehaviour)spot); enemy.Edit().ExpeditionRole = 3;
                    c.ExpeditionRisk -= interval;
                    world.Notify("开采声引来了洞穴游荡者。", true);
                }
            }
            foreach (var enemy in world.Index.Actors.Where(a => a.Enemy).ToArray())
            {
                var s = enemy.Edit();
                var target = world.Index.Actors.Where(a => !a.Enemy && !a.Read().Boarded && a.Hp > 0)
                    .OrderBy(a => ExpeditionDevices.Distance(a.X, a.Read().Height, s.X, s.Height)).FirstOrDefault();
                if (target == null) continue;
                s.AttackClock = Math.Max(0, s.AttackClock - delta);
                bool near = ExpeditionDevices.Distance(s.X, s.Height, target.X, target.Read().Height) < 20;
                if (!near) world.ExpeditionDevices.Navigation.Move(enemy, target.X, target.Read().Height, delta, false);
                else if (s.AttackClock == 0 && ExpeditionNavigation.Sight(world.Terrain.Map, s.X, s.Height + 10, target.X, target.Read().Height + 10))
                { world.Combat.Damage(target, 2); s.AttackClock = enemy.Definition.AttackSeconds; }
            }
            foreach (var turret in world.Index.Buildings.Where(b => b.RuleKey == "turret" && b.Read().Powered))
            {
                var s = turret.Edit(); s.AttackClock = Math.Max(0, s.AttackClock - delta);
                if (s.AttackClock > 0) continue;
                var target = world.Index.Actors.Where(a => a.Enemy &&
                    ExpeditionDevices.Distance(a.X, a.Read().Height, s.X, s.Height) < 160 &&
                    ExpeditionNavigation.Sight(world.Terrain.Map, s.X, s.Height + 20, a.X, a.Read().Height + 10))
                    .OrderBy(a => a.Id).FirstOrDefault();
                if (target == null) continue;
                world.Combat.Damage(target, 6); s.AttackClock = 1.2;
            }
        }
        private static float Height(MineralDepositBehaviour d) => PlayableTerrain.OriginY - (d.Y + .5f) * 16;
    }
}
