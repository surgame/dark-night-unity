using System;
using System.Linq;
using DarkNights.Runtime.Save;
using Newtonsoft.Json.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Save;
using DarkNights.Core.Logic;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

using static DarkNights.Tests.RuleScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证统一时间边界、最小伤害与箭矢只结算一次。冻结状态按完整快照比较，同时覆盖击杀奖励和酒馆摧毁的失败路径。
    /// </summary>
    public static class TimeCombatScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var game = new GameSession(catalog, layout);
            int[] selection = Array.Empty<int>();
            string buildKind = "";
            game.Work.Assign(Workers(game)[0], Site(game, "wood"));
            selection = new[] { Workers(game)[1].Id };
            game.Training.Start("archer", selection);
            buildKind = "tower";
            game.Construction.Place(buildKind, 708, selection);
            game.Lifecycle.SpawnActor("zombie", 950, true);
            Step(game, 2);
            game.Paused = true;
            string frozen = LegacySnapshotJson.Serialize(SnapshotMapper.Capture(game));
            Step(game, 120);
            check(LegacySnapshotJson.Serialize(SnapshotMapper.Capture(game)) == frozen, "Pause freezes movement, economy, construction, training, combat and waves");
            game.NewGame();
            Workers(game)[0].OrderMove(400);
            Step(game, 5);
            float oneX = Workers(game)[0].X;
            double oneDay = game.Waves.DayRemaining;
            game.NewGame();
            game.Speed = 2;
            Workers(game)[0].OrderMove(400);
            Step(game, 2.5);
            check(RuleScenario.Approx(Workers(game)[0].X, oneX) && RuleScenario.Approx(game.Waves.DayRemaining, oneDay), "2x applies time scaling once to movement and phase timers");
            var armored = game.Lifecycle.SpawnActor("armored", 970, true);
            game.Combat.Damage(armored, 1);
            check(armored.Hp == 39, "Armor reduction retains a minimum of one damage");
            game.Speed = 1;
            var enemy = game.Lifecycle.SpawnActor("zombie", 950, true);
            game.Projectiles.Launch(new WorldPoint(870, game.GroundY - 15), enemy, 5);
            check(enemy.Hp == 20 && game.World.Projectiles.Count == 1, "Ranged attacks wait for visible projectile flight");
            Step(game, 1);
            check(enemy.Hp == 15 && game.World.Projectiles.Count == 0, "A projectile applies its damage exactly once");
            int id = enemy.Id;
            game.Combat.Damage(enemy, 100);
            check(game.World.Find(id) == null && game.Stats.Kills == 1 && game.Economy.Stock.Gold == 2, "Enemy death removes entity and grants the configured bounty");
            game.Combat.Damage(Building(game, "tavern"), 999);
            check(game.Mode == SessionMode.Lost, "Tavern destruction ends the session in defeat");
        }
    }
}
