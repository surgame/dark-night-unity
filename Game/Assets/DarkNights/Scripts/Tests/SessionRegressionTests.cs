using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 把仍有效的命令、权限、时钟、投影及文件边界断言迁到真实 YYGC 会话。
    /// 每项一次预加载、独立装配并统一释放，不保留旧运行模型或伪造框架对象。
    /// </summary>
    [Category("UnifiedSession")]
    public sealed class SessionRegressionTests
    {
        private UnifiedSessionScope scope;

        [UnitySetUp]
        public IEnumerator Prepare()
        {
            yield return UniTask.ToCoroutine(async () => scope = await UnifiedSessionScope.Create());
        }

        [TearDown]
        public void Cleanup() => scope?.Dispose();

        [TestCase("economy")]
        [TestCase("work-training")]
        [TestCase("time-combat")]
        [TestCase("new-save")]
        [TestCase("save-files")]
        [TestCase("commands")]
        [TestCase("boundary")]
        [TestCase("lifecycle")]
        [TestCase("projection")]
        [TestCase("replica")]
        [TestCase("clock")]
        [TestCase("events")]
        [TestCase("storage")]
        public void ContractsUseRealObjects(string scenario)
        {
            var catalog = RuleScenario.Catalog();
            var layout = RuleScenario.Layout();
            var checks = new JArray();
            bool passed = false;
            System.Action<bool, string> check = (ok, name) =>
            {
                checks.Add(new JObject { ["passed"] = ok, ["name"] = name });
                Assert.That(ok, Is.True, name);
            };
            try
            {
                switch (scenario)
                {
                case "economy": UnifiedRuleScenarios.Economy(check, catalog, layout); break;
                case "work-training": UnifiedRuleScenarios.WorkAndTraining(check, catalog, layout); break;
                case "time-combat": UnifiedRuleScenarios.TimeAndCombat(check, catalog, layout); break;
                case "new-save": GameSaveScenarios.Run(check, catalog, layout); break;
                case "save-files": GameSaveFileScenarios.Run(check, catalog, layout); break;
                case "commands": SessionCommandScenarios.Run(check, catalog, layout); break;
                case "boundary": SessionBoundaryScenarios.Run(check, catalog, layout); break;
                case "lifecycle": SessionLifecycleScenarios.Run(check, catalog, layout); break;
                case "projection": SessionProjectionScenarios.Run(check, catalog, layout); break;
                case "replica": SessionReplicaScenarios.Run(check, catalog, layout); break;
                case "clock": SessionClockScenarios.Run(check, catalog, layout); break;
                case "events": SessionEventScenarios.Run(check, catalog, layout); break;
                case "storage": SessionStorageScenarios.Run(check, catalog, layout); break;
                }
                passed = true;
            }
            finally
            {
                const string folder = "../artifacts/yygc-unified/u5/scenarios";
                Directory.CreateDirectory(folder);
                File.WriteAllText(folder + "/" + scenario + ".json", new JObject
                {
                    ["scenario"] = scenario, ["passed"] = passed, ["total"] = checks.Count, ["checks"] = checks
                }.ToString());
            }
        }
    }
}
