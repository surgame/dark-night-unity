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
    /// 验证工作占用与训练之间的互斥，以及死亡和兵营销毁后的清理。训练通过完整移动与模拟计时完成，断言身份和人口未被转职复制。
    /// </summary>
    public static class WorkTrainingScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var game = new GameSession(catalog, layout);
            int[] selection = Array.Empty<int>();
            var workers = Workers(game);
            var wood = Site(game, "wood");
            check(game.Work.Assign(workers[0], wood), "Worker can claim a resource site");
            check(!game.Work.Assign(workers[1], wood) && wood.WorkerId == workers[0].Id, "A second worker cannot steal an occupied site");
            Step(game, 12);
            check(game.Economy.Stock.Wood == 103, "Gathering produces one configured batch after travel and work");
            workers[0].OrderMove(200);
            check(wood.WorkerId == 0 && wood.Progress == 0, "Changing orders releases work and partial progress");
            game.Work.Assign(workers[1], wood);
            game.Combat.Damage(workers[1], 100);
            check(wood.WorkerId == 0 && game.Economy.Population == 6, "Death releases work and population");
            game.Work.Assign(workers[0], wood);
            selection = new[] { workers[0].Id, workers[2].Id };
            int population = game.Economy.Population;
            check(game.Training.Start("spearman", selection) == 2 && wood.WorkerId == 0, "Training claims two workers and releases their previous work");
            check(workers[0].IsTraining && !game.Work.Assign(workers[0], wood), "A training worker cannot gather or construct");
            Step(game, 20);
            check(workers[0].Kind == "spearman" && workers[2].Kind == "worker", "Barracks completes training sequentially");
            Step(game, 20);
            check(workers[2].Kind == "spearman" && workers[2].Hp == 30 && game.Economy.Population == population, "Promotion applies combat stats without adding population");
            selection = new[] { workers[3].Id };
            game.Training.Start("archer", selection);
            var stock = game.Economy.Stock;
            game.Combat.Damage(Building(game, "barracks"), 1000);
            check(!workers[3].IsTraining && game.Economy.Stock.Food == stock.Food + 10, "Destroyed barracks releases queued trainees and refunds uncompleted training");
        }
    }
}
