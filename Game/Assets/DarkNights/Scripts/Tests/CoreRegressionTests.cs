using System.IO;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 在 Unity Editor 中复跑与独立进程相同的真实规则和冻结旧档检查。
    /// 不创建场景或更改美术资源；测试失败直接保留具体断言，不把独立 Core 成功当作引擎验证。
    /// </summary>
    public sealed class CoreRegressionTests
    {
        [TestCase("economy")]
        [TestCase("work-training")]
        [TestCase("time-combat")]
        [TestCase("campaign")]
        [TestCase("save")]
        [TestCase("new-save")]
        [TestCase("save-files")]
        [TestCase("random")]
        public void RulesMatchFrozenBaseline(string scenario)
        {
            RuleScenario.RepositoryRoot = Path.GetFullPath("..");
            var catalog = RuleScenario.Catalog();
            var layout = RuleScenario.Layout();
            System.Action<bool, string> check = (ok, name) => Assert.That(ok, Is.True, name);
            switch (scenario)
            {
                case "economy": EconomyScenarios.Run(check, catalog, layout); break;
                case "work-training": WorkTrainingScenarios.Run(check, catalog, layout); break;
                case "time-combat": TimeCombatScenarios.Run(check, catalog, layout); break;
                case "campaign": CampaignScenario.Run(check, catalog, layout); break;
                case "save": SaveMigrationScenarios.Run(check, catalog, layout); break;
                case "new-save": GameSaveScenarios.Run(check, catalog, layout); break;
                case "save-files": GameSaveFileScenarios.Run(check, catalog, layout); break;
                case "random": RandomCompatibilityScenarios.Run(check); break;
            }
        }
    }
}
