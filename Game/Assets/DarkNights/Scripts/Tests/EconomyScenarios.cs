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
    /// 验证开局、原子支付、放置及施工人口规则。覆盖无效命令与成功路径，确保UI触发前后的预算与共享定义保持一致。
    /// </summary>
    public static class EconomyScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            CheckLayoutValidation(check, catalog, layout);
            var game = new GameSession(catalog, layout);
            int[] selection = Array.Empty<int>();
            string buildKind = "";
            check(game.Economy.Population == 7 && game.Economy.Capacity == 9, "Initial population 7/9 and five resource types");
            var before = game.Economy.Stock;
            check(!game.Economy.Pay(new ResourceAmounts(food: 1, wood: 1000)) && game.Economy.Stock == before, "Multi-resource payment fails atomically");
            check(!game.Economy.Pay(new ResourceAmounts(food: -5)) && game.Economy.Stock == before, "Negative costs cannot create resources");
            buildKind = "tower";
            check(!game.Construction.Place(buildKind, 130, selection) && game.Economy.Stock == before, "Overlapping construction does not deduct resources");
            selection = new[] { Workers(game)[0].Id };
            check(game.Construction.Place(buildKind, 708, selection), "Valid tower placement creates an assigned construction site");
            var tower = Building(game, "tower");
            check(game.Economy.Stock.Wood == 55 && game.Economy.Stock.Stone == 45 && game.Economy.Stock.Iron == 30, "Construction deducts the configured cost once");
            check(tower.Progress == 0 && tower.Hp == 40, "Construction progress is independent from building HP");
            Step(game, 45);
            check(tower.IsComplete && tower.WorkerId == 0 && Math.Abs(tower.Hp - 200) < 0.000001, "Worker travel, construction, completion and ownership release");
            check(catalog.Balance.Buildings["tower"].Hp == 200, "Entity updates do not mutate shared definitions");
            selection = new[] { Workers(game)[0].Id };
            buildKind = "house";
            check(game.Construction.Place(buildKind, 184, selection), "House placement in a free village slot");
            Step(game, 30);
            check(game.Economy.Capacity == 12, "Completed house adds three population places");
        }

        private static void CheckLayoutValidation(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            var overlapping = layout.Buildings.Select((entry, index) => index == 0
                ? new PlacementDefinition(entry.Kind, 130)
                : entry).ToArray();
            check(Rejected(new LevelLayout(layout.WorldWidth, layout.GroundY, layout.BuildMinX, layout.BuildMaxX,
                layout.SpawnX, layout.CameraX, overlapping, layout.Worksites, layout.Actors), catalog),
                "Initial building overlap is rejected before world creation");
            var invalidVariant = layout.Actors.Select((entry, index) => index == 0
                ? new PlacementDefinition(entry.Kind, entry.X, 1, entry.Name)
                : entry).ToArray();
            check(Rejected(new LevelLayout(layout.WorldWidth, layout.GroundY, layout.BuildMinX, layout.BuildMaxX,
                layout.SpawnX, layout.CameraX, layout.Buildings, layout.Worksites, invalidVariant), catalog),
                "Initial actor visual variants cannot alter gameplay layout identity");
        }

        private static bool Rejected(LevelLayout layout, GameCatalog catalog)
        {
            try { layout.Validate(catalog); }
            catch (ArgumentException) { return true; }
            return false;
        }
    }
}
