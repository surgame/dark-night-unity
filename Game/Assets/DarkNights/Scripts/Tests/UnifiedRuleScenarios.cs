using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Objects;

namespace DarkNights.Tests
{
    /// <summary>
    /// 将旧规则回归中的支付、工位、施工、训练、时间与战斗断言迁到真实 YYGC 能力。
    /// 只在测试中调用受事务保护的内部边界；转职后按稳定 ID 查询新对象，不继续读取已退休状态。
    /// </summary>
    public static class UnifiedRuleScenarios
    {
        public static void Economy(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var overlapping = layout.Buildings.Select((p, i) => i == 0 ? new PlacementDefinition(p.Kind, 130) : p).ToArray();
            check(SessionScenario.Throws<ArgumentException>(() => new LevelLayout(layout.WorldWidth, layout.GroundY,
                layout.BuildMinX, layout.BuildMaxX, layout.SpawnX, layout.CameraX, overlapping, layout.Worksites, layout.Actors).Validate(catalog)),
                "Initial building overlap is rejected before object creation");
            var invalidVariant = layout.Actors.Select((p, i) => i == 0 ? new PlacementDefinition(p.Kind, p.X, 1, p.Name) : p).ToArray();
            check(SessionScenario.Throws<ArgumentException>(() => new LevelLayout(layout.WorldWidth, layout.GroundY,
                layout.BuildMinX, layout.BuildMaxX, layout.SpawnX, layout.CameraX, layout.Buildings, layout.Worksites, invalidVariant).Validate(catalog)),
                "Actor variants cannot alter gameplay layout identity");
            using var game = SessionScenario.World(catalog, layout);
            var before = game.Economy.Stock;
            check(game.Economy.Population == 7 && game.Economy.Capacity == 9, "Initial population remains 7/9");
            check(!UnifiedGameplayProbe.Pay(game, new ResourceAmounts(food: 1, wood: 1000)) && Same(before, game.Economy.Stock),
                "Multi-resource payment fails atomically");
            check(!UnifiedGameplayProbe.Pay(game, new ResourceAmounts(food: -5)) && Same(before, game.Economy.Stock),
                "Negative costs cannot create resources");
            check(game.PlaceBuilding("tower", 130, Array.Empty<int>()) == 0 && Same(before, game.Economy.Stock),
                "Overlapping construction does not deduct resources");
            int towerId = game.PlaceBuilding("tower", 708, new[] { Workers(game)[0].Id });
            var tower = game.Index.Find<BuildingBehaviour>(towerId);
            check(tower != null && tower.WorkerId != 0, "Valid tower placement assigns the requested worker");
            check(game.Economy.Stock.Wood == 55 && game.Economy.Stock.Stone == 45 && game.Economy.Stock.Iron == 30,
                "Construction deducts configured costs exactly once");
            check(tower.Progress == 0 && tower.Hp == 40, "Construction progress is independent of HP");
            Step(game, 45);
            check(tower.IsComplete && tower.WorkerId == 0 && Math.Abs(tower.Hp - 200) < 0.000001,
                "Travel, construction and completion release worker ownership");
            check(catalog.Balance.Buildings["tower"].Hp == 200, "Instance changes do not mutate shared rules");
            check(game.PlaceBuilding("house", 184, new[] { Workers(game)[0].Id }) != 0, "House fits the original free village slot");
            Step(game, 30);
            check(game.Economy.Capacity == 12, "Completed house adds three population places");
        }

        public static void WorkAndTraining(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var game = SessionScenario.World(catalog, layout);
            var workers = Workers(game);
            int[] ids = workers.Select(w => w.Id).ToArray();
            var wood = game.Index.Worksites.First(w => w.RuleKey == "wood");
            check(UnifiedGameplayProbe.Assign(game, workers[0], wood), "Worker can claim a resource site");
            check(!UnifiedGameplayProbe.Assign(game, workers[1], wood) && wood.WorkerId == ids[0],
                "A second worker cannot steal an occupied site");
            Step(game, 12);
            check(game.Economy.Stock.Wood == 103, "Gathering yields the configured batch after travel");
            game.IssueOrders(new[] { ids[0] }, 0, 200);
            check(wood.WorkerId == 0 && wood.CaptureState().Progress == 0, "Changed order releases work and partial progress");
            UnifiedGameplayProbe.Assign(game, workers[1], wood);
            UnifiedGameplayProbe.Damage(game, workers[1], 100);
            check(wood.WorkerId == 0 && game.Economy.Population == 6, "Death releases work and population");
            UnifiedGameplayProbe.Assign(game, workers[0], wood);
            int population = game.Economy.Population;
            check(game.TrainActors("spearman", new[] { ids[0], ids[2] }) == 2 && wood.WorkerId == 0,
                "Training claims requested workers and releases previous work");
            check(workers[0].IsTraining && !UnifiedGameplayProbe.Assign(game, workers[0], wood),
                "A training worker cannot gather or construct");
            Step(game, 20);
            check(game.Index.Find<ActorBehaviour>(ids[0]).RuleKey == "spearman" && game.Index.Find<ActorBehaviour>(ids[2]).RuleKey == "worker",
                "Barracks completes training sequentially");
            Step(game, 20);
            var promoted = game.Index.Find<ActorBehaviour>(ids[2]);
            check(promoted.RuleKey == "spearman" && promoted.Hp == 30 && game.Economy.Population == population,
                "Promotion replaces profession without duplicating population");
            game.TrainActors("archer", new[] { ids[3] });
            var stock = game.Economy.Stock;
            UnifiedGameplayProbe.Damage(game, game.Index.Buildings.First(b => b.RuleKey == "barracks"), 1000);
            check(!game.Index.Find<ActorBehaviour>(ids[3]).IsTraining && game.Economy.Stock.Food == stock.Food + 10,
                "Destroyed barracks releases trainees and refunds unfinished training");
        }

        public static void TimeAndCombat(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var game = SessionScenario.World(catalog, layout);
            var workers = Workers(game);
            UnifiedGameplayProbe.Assign(game, workers[0], game.Index.Worksites.First(w => w.RuleKey == "wood"));
            game.TrainActors("archer", new[] { workers[1].Id });
            game.PlaceBuilding("tower", 708, new[] { workers[1].Id });
            UnifiedGameplayProbe.Spawn(game, "zombie", 950);
            Step(game, 2);
            game.SetTime(true, 1);
            string frozen = game.SaveCodec.Serialize(game.CaptureWorld());
            Step(game, 120);
            check(game.SaveCodec.Serialize(game.CaptureWorld()) == frozen,
                "Pause freezes movement, economy, construction, training, combat and waves");
            game.Restart();
            game.IssueOrders(new[] { Workers(game)[0].Id }, 0, 400);
            Step(game, 5);
            float oneX = Workers(game)[0].X;
            double oneDay = game.Waves.CaptureState().DayRemaining;
            game.Restart();
            game.SetTime(false, 2);
            game.IssueOrders(new[] { Workers(game)[0].Id }, 0, 400);
            Step(game, 2.5);
            check(RuleScenario.Approx(Workers(game)[0].X, oneX) && RuleScenario.Approx(game.Waves.CaptureState().DayRemaining, oneDay),
                "2x scales movement and phase time once");
            var armored = UnifiedGameplayProbe.Spawn(game, "armored", 970);
            UnifiedGameplayProbe.Damage(game, armored, 1);
            check(armored.Hp == 39, "Armor retains minimum one damage");
            game.SetTime(false, 1);
            var enemy = UnifiedGameplayProbe.Spawn(game, "zombie", 950);
            UnifiedGameplayProbe.Shoot(game, 870, enemy, 5);
            check(enemy.Hp == 20 && game.Projectiles.CaptureState().Shots.Length == 1, "Ranged damage waits for flight");
            Step(game, 1);
            check(enemy.Hp == 15 && game.Projectiles.CaptureState().Shots.Length == 0, "Projectile applies damage exactly once");
            int enemyId = enemy.Id;
            UnifiedGameplayProbe.Damage(game, enemy, 100);
            check(game.Index.Find(enemyId) == null && game.Camp.CaptureState().Kills == 1 && game.Economy.Stock.Gold == 2,
                "Death retires the object and grants configured bounty");
            UnifiedGameplayProbe.Damage(game, game.Index.Buildings.First(b => b.RuleKey == "tavern"), 999);
            check(game.Camp.CaptureState().Mode == SessionMode.Lost, "Destroyed tavern ends the session");
        }

        private static ActorBehaviour[] Workers(ObjectSession world) => world.Index.Actors.Where(a => !a.Enemy && a.RuleKey == "worker").ToArray();
        private static bool Same(ResourceAmounts a, ResourceAmounts b) => GameText.ResourceIds.All(key => a.Get(key) == b.Get(key));
        private static void Step(ObjectSession game, double seconds)
        {
            for (int i = 0; i < (int)Math.Round(seconds * 60); i++) game.Advance(1.0 / 60);
        }
    }
}
