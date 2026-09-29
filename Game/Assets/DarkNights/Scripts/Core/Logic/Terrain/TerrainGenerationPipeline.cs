using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>按作者顺序执行冻结步骤的纯 Core 流水线；阶段屏障由生成器控制，错误候选直接失败。</summary>
    public sealed class TerrainGenerationPipeline
    {
        private readonly ITerrainGenerationModifier[] modifiers;
        public static TerrainGenerationPipeline Default { get; } = new TerrainGenerationPipeline(
            new ITerrainGenerationModifier[] { new EntranceWalkwayModifier() });

        public TerrainGenerationPipeline(IEnumerable<ITerrainGenerationModifier> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            var list = new List<ITerrainGenerationModifier>(steps);
            if (list.Count > 32) throw new ArgumentException("权威地形步骤最多 32 项。");
            foreach (var step in list)
                if (step == null || string.IsNullOrWhiteSpace(step.StableId) ||
                    step.Stage < TerrainGenerationStage.AfterCave || step.Stage > TerrainGenerationStage.BeforeGeometry)
                    throw new ArgumentException("权威地形步骤包含空项或未知阶段。");
            modifiers = list.ToArray();
        }

        public void Run(TerrainGenerationStage stage, TerrainGenerationContext context,
            Action<TerrainModifierDiagnostic> report = null)
        {
            foreach (var modifier in modifiers)
            {
                if (modifier.Stage != stage) continue;
                context.CheckCancellation();
                byte[] before = report == null ? null : context.CopyMaterials();
                bool[] protectedBefore = report == null ? null : context.CopyProtection();
                bool[] softBefore = report == null ? null : context.CopySoftRock();
                modifier.Apply(context);
                context.CheckCancellation();
                if (report == null) continue;
                var after = context.CopyMaterials();
                var protectedAfter = context.CopyProtection();
                var softAfter = context.CopySoftRock();
                int count = 0, minX = TerrainGenerationSettings.Width, minY = TerrainGenerationSettings.Height;
                int maxX = -1, maxY = -1;
                for (int i = 0; i < after.Length; i++)
                {
                    if (after[i] == before[i] && protectedAfter[i] == protectedBefore[i] && softAfter[i] == softBefore[i]) continue;
                    int x = i % TerrainGenerationSettings.Width, y = i / TerrainGenerationSettings.Width;
                    count++; minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
                report(new TerrainModifierDiagnostic(modifier.StableId, stage, count,
                    count == 0 ? -1 : minX, count == 0 ? -1 : minY, maxX, maxY));
            }
        }
    }
}
