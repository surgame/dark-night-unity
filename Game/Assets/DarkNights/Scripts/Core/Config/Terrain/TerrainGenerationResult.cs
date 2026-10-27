using System;
using System.Collections.Generic;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 一次制作计算的冻结产物；最终地图、同次地下计划和步骤回执共同交给预览及保存。
    /// 不拥有运行写权限，不用于重建历史地图；步骤快照仅在作者明确要求记录时保留。
    /// </summary>
    public sealed class TerrainGenerationResult
    {
        public PlayableTerrain Terrain { get; }
        public CaveRulePlan Plan { get; }
        public int Version { get; }
        public string SpaceIdentity { get; }
        public IReadOnlyList<TerrainGenerationStep> Steps { get; }

        public TerrainGenerationResult(PlayableTerrain terrain, CaveRulePlan plan, int version,
            string spaceIdentity, TerrainGenerationStep[] steps)
        {
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            Plan = plan;
            Version = version;
            SpaceIdentity = spaceIdentity ?? "";
            Steps = Array.AsReadOnly((TerrainGenerationStep[])steps.Clone());
        }
    }
}
