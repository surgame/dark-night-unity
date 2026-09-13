using System.IO;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 在 Unity 与独立 .NET 进程复跑同一纯计算及冻结 RNG 合同。
    /// 不创建游戏世界；对象、会话和存储的集成断言另由真实 YYGC 测试范围执行。
    /// </summary>
    public sealed class CoreRegressionTests
    {
        [Test]
        public void FrozenRandomAndPureRules()
        {
            RuleScenario.RepositoryRoot = Path.GetFullPath("..");
            System.Action<bool, string> check = (ok, name) => Assert.That(ok, Is.True, name);
            RandomCompatibilityScenarios.Run(check);
            PureRuleScenarios.Run(check, RuleScenario.Catalog(), RuleScenario.Layout());
        }
    }
}
