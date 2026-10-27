using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>完整生成流程的统一矿物阶段；按配方选择算法，每张基础地图只执行一次，不读取历史模式或编辑器状态。</summary>
    internal static class TerrainContentGenerator
    {
        internal static PlayableTerrain Generate(PlayableTerrain source, TerrainGenerationSettings settings,
            CaveRulePlan plan, Action<string> report)
        {
            var minerals = settings.Minerals;
            switch (minerals.Algorithm)
            {
                case TerrainMineralSettings.BaseDeposits: return source;
                case TerrainMineralSettings.WallVeins: return JointMineCandidate.Generate(source, minerals.VeinCount, minerals.Nests,
                    source.Seed + "|" + minerals.ContentKey, report);
                case TerrainMineralSettings.RegionalVeins: return CaveRuleContent.Generate(source, settings, plan, report);
                default: throw new ArgumentException("未知矿物算法。");
            }
        }
    }
}
