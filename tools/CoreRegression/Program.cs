using System;
using System.IO;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
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
                PureRuleScenarios.Run(check, catalog, layout);
                RandomCompatibilityScenarios.Run(check);
                RunMapPlanChecks(check);
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

        /// <summary>执行地图方案中不依赖 Unity 或网络的生成与破坏策略断言。</summary>
        private static void RunMapPlanChecks(Action<bool, string> check)
        {
            var settings = new TerrainGenerationSettings
            {
                Seed = "MAP-PLAN-CORE",
                Surface = "rolling",
                OrganicCaves = true
            };
            var first = TerrainGenerator.Generate(settings);
            var second = TerrainGenerator.Generate(settings);
            check(first.CopyMaterials().AsSpan().SequenceEqual(second.CopyMaterials().AsSpan()),
                "Map plan generation remains deterministic for the same seed");
            check(first.Rooms.Count == 8 && first.Deposits.Count == 11 && first.SoftRockCount > 0,
                "Map plan keeps eight rooms and gameplay resource markers");
            check(TerrainDestructionPolicy.Offsets(TerrainEditAction.HandMine).Count == 1 &&
                TerrainDestructionPolicy.Offsets(TerrainEditAction.Explosive).Count == 13,
                "Hand mining and explosive target sets stay bounded");
            check(TerrainDestructionPolicy.CanDestroy(TerrainEditAction.HandMine, 1, false, true) &&
                TerrainDestructionPolicy.CanDestroy(TerrainEditAction.HandMine, 4, false, false) &&
                !TerrainDestructionPolicy.CanDestroy(TerrainEditAction.HandMine, 1, false, false) &&
                !TerrainDestructionPolicy.CanDestroy(TerrainEditAction.HandMine, 8, false, true),
                "Hand mining is limited to soft rock and scattered ore");
            check(!TerrainDestructionPolicy.CanDestroy(TerrainEditAction.Explosive, 8, false, true) &&
                !TerrainDestructionPolicy.CanDestroy(TerrainEditAction.Explosive, 1, true, true),
                "Explosives cannot clear bedrock or protected cells");
        }
    }
}
