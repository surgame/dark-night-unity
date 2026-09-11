using System;
using System.IO;
using DarkNights.Tests;
using Newtonsoft.Json.Linq;

namespace DarkNights.Tools.CoreRegression
{
    /// <summary>
    /// 在独立 .NET 进程复用正式 Core 程序集及实际 Runtime JSON 映射，输出完整断言报告。
    /// 不启动引擎、不改写冻结夹具；非零退出表示存在失败，不能代替 Unity Player 或联机验证。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            RuleScenario.RepositoryRoot = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
            var checks = new JArray();
            int failures = 0;
            Action<bool, string> check = (ok, name) =>
            {
                checks.Add(new JObject { ["name"] = name, ["passed"] = ok });
                if (!ok) { failures++; Console.WriteLine("FAIL: " + name); }
            };
            try
            {
                var catalog = RuleScenario.Catalog();
                var layout = RuleScenario.Layout();
                EconomyScenarios.Run(check, catalog, layout);
                WorkTrainingScenarios.Run(check, catalog, layout);
                TimeCombatScenarios.Run(check, catalog, layout);
                CampaignScenario.Run(check, catalog, layout);
                SaveMigrationScenarios.Run(check, catalog, layout);
                GameSaveScenarios.Run(check, catalog, layout);
                GameSaveFileScenarios.Run(check, catalog, layout);
                SessionCommandScenarios.Run(check, catalog, layout);
                SessionBoundaryScenarios.Run(check, catalog, layout);
                SessionLifecycleScenarios.Run(check, catalog, layout);
                SessionProjectionScenarios.Run(check, catalog, layout);
                SessionReplicaScenarios.Run(check, catalog, layout);
                SessionClockScenarios.Run(check, catalog, layout);
                SessionEventScenarios.Run(check, catalog, layout);
                SessionStorageScenarios.Run(check, catalog, layout);
                RandomCompatibilityScenarios.Run(check);
            }
            catch (Exception error) { check(false, error.ToString()); }
            string output = Path.Combine(RuleScenario.RepositoryRoot, "artifacts/migration/core-regression.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, new JObject
            {
                ["utc"] = DateTime.UtcNow.ToString("O"), ["runtime"] = Environment.Version.ToString(),
                ["passed"] = failures == 0, ["total"] = checks.Count, ["failures"] = failures, ["checks"] = checks
            }.ToString());
            Console.WriteLine("Core regression: " + checks.Count + " checks, " + failures + " failures; " + output);
            return failures == 0 ? 0 : 1;
        }
    }
}
