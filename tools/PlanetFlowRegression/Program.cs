using System;
using System.Diagnostics;
using System.IO;

namespace DarkNights.Tools.PlanetFlowRegression
{
    /// <summary>独立纯 Core 航程回归入口；报告仅覆盖数据与算法，不能代替 Unity、真实移动或网络验收。</summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 1 && args[0] == "--diagnose") { TerrainScenarios.Diagnose(args[1]); return 0; }
            bool snapshotOnly = args.Length > 1 && args[0] == "--snapshot";
            string output = Path.GetFullPath(snapshotOnly ? args[1] : args.Length > 0 ? args[0] : "artifacts/space-planet-flow/core-planet-flow.json");
            var clock = Stopwatch.StartNew();
            var report = new ScenarioReport();
            if (!snapshotOnly)
            {
                report.Run("配置边界批次", () => ConfigurationScenarios.Run(report));
                report.Run("地形生成批次", () => TerrainScenarios.Run(report));
                report.Run("航程与存档关系批次", () => JourneyScenarios.Run(report));
            }
            report.Run("完整冻结快照批次", () => SnapshotScenarios.Run(report));
            report.Write(output, clock.Elapsed.TotalSeconds);
            Console.WriteLine($"Planet flow: {report.Total - report.Failures}/{report.Total}, failures={report.Failures}; {output}");
            return report.Failures == 0 ? 0 : 1;
        }
    }
}
