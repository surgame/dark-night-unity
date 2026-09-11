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
    /// 从正常开局执行可重复的三夜防守策略，并运行无人照料的失败场景。报告来自真实模拟与伤害结算，通关快照仅用于后续画面捕获。
    /// </summary>
    public static class CampaignScenario
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var game = new GameSession(catalog, layout);
            int[] selection = Array.Empty<int>();
            string buildKind = "";
            var workers = Workers(game);
            int[] production = new[] { workers[0].Id, workers[1].Id, workers[4].Id };
            for (int i = 0; i < 2; i++)
            {
                selection = new[] { workers[i].Id };
                buildKind = "tower";
                check(game.Construction.Place(buildKind, 708 + i * 56, selection), $"Campaign: tower {i + 1} placed using starting resources");
            }
            selection = new[] { workers[2].Id, workers[3].Id };
            check(game.Training.Start("archer", selection) == 2, "Campaign: two archers funded from starting resources");
            game.Work.Assign(workers[4], Site(game, "food"));

            for (int second = 0; second < 720 && game.Mode == SessionMode.Playing; second++)
            {
                if (second >= 40 && game.Economy.Population < game.Economy.Capacity && game.Economy.RecruitCooldown <= 0 && game.Economy.CanPay(new ResourceAmounts(food: 25)))
                    game.Camp.Recruit();
                foreach (var actor in game.World.Actors.ToArray())
                {
                    if (actor.Enemy)
                        continue;
                    if (actor.Kind == "worker" && actor.State == ActorActivity.Idle)
                    {
                        if (production.Contains(actor.Id))
                        {
                            var site = game.World.Worksites.FirstOrDefault(s => s.Kind == "wood" && s.WorkerId == 0 && s.Amount != 0);
                            if (site != null)
                                game.Work.Assign(actor, site);
                        }
                        else
                        {
                            selection = new[] { actor.Id };
                            game.Training.Start("spearman", selection);
                        }
                    }
                    else if (actor.Kind != "worker" && actor.State == ActorActivity.Idle)
                    {
                        float rally = actor.Kind == "archer" ? 712 : 766;
                        if (Math.Abs(actor.X - rally) > 2)
                            actor.OrderMove(rally);
                    }
                }
                foreach (var building in game.World.Buildings)
                    if (building.IsComplete && building.Hp < building.MaximumHp - 80 && game.Economy.CanPay(catalog.Balance.Economy.RepairCost))
                    {
                        selection = new[] { building.Id };
                        game.Camp.Repair(building.Id);
                    }
                Step(game, 1);
            }
            check(game.Mode == SessionMode.Won && game.Waves.Index == 2 && game.Stats.Kills == 34 && game.World.EnemyCount == 0,
                "Campaign: normal-resource strategy defeats all 34 enemies across three nights");
            JObject expected = RuleScenario.Fixture("godot-gameplay-validation.json");
            RuleScenario.CompareCampaign(check, game, expected["playthrough"]);
            var idle = new GameSession(catalog, layout);
            Step(idle, 1000);
            check(idle.Mode == SessionMode.Lost, "Campaign: leaving the camp undefended naturally reaches defeat");
            check(Math.Abs(idle.Elapsed - (double)expected["idle_playthrough"]["simulation_seconds"]) < 0.0001 &&
                idle.Stats.Kills == (int)expected["idle_playthrough"]["kills"], "Idle campaign matches frozen elapsed time and kills");
        }
    }
}
