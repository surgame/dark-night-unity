using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 星球表面候选生成器；复用天然洞穴，按固定阶段运行冻结的权威格子步骤。
    /// 全部输入输出为纯数据，可在后台执行；背景参考必须在最后一次地形修改之后捕获。
    /// </summary>
    public static class PlanetTerrainGenerator
    {
        private const int W = TerrainGenerationSettings.Width;
        private const int H = TerrainGenerationSettings.Height;
        public const string SpaceSeed = "SPACE-CARRIER-V1";
        public const int Version = 5;

        /// <summary>正式航程与所有随机洞穴预览唯一的完整生成入口；重试、最终坡形与背景捕获均在此复用。</summary>
        public static PlayableTerrain GenerateCandidate(PlanetDefinition planet, string seed, string worldId,
            TerrainGenerationSettings template, Func<bool> cancelled = null,
            TerrainGenerationPipeline pipeline = null, Action<TerrainModifierDiagnostic> report = null)
        {
            pipeline = pipeline ?? TerrainGenerationPipeline.Default;
            int attempts = planet.Seed.Length == 0 ? 3 : 1;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                CheckCancellation(cancelled);
                string actualSeed = attempt == 0 ? seed : seed.Substring(0, Math.Min(seed.Length, 73)) + "-retry" + attempt;
                try
                {
                    var diagnostics = report == null ? null : new List<TerrainModifierDiagnostic>();
                    var result = Generate(planet, actualSeed, worldId, cancelled, template, pipeline,
                        diagnostics == null ? null : new Action<TerrainModifierDiagnostic>(diagnostics.Add));
                    if (diagnostics != null) foreach (var item in diagnostics) report(item);
                    return result;
                }
                catch (InvalidOperationException) when (attempt + 1 < attempts) { }
            }
            throw new InvalidOperationException("星球候选生成重试已耗尽。");
        }

        public static PlayableTerrain Generate(PlanetDefinition planet, string seed, string worldId, Func<bool> cancelled = null,
            TerrainGenerationSettings template = null, TerrainGenerationPipeline pipeline = null,
            Action<TerrainModifierDiagnostic> report = null)
        {
            if (planet == null) throw new ArgumentNullException(nameof(planet));
            CheckCancellation(cancelled);
            var source = TerrainGenerator.GenerateCave(template ?? new TerrainGenerationSettings
            { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile }, seed);
            CheckCancellation(cancelled);
            var cells = source.CopyMaterials();
            var protection = new bool[cells.Length];
            var soft = source.CopySoftRock();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    protection[y * W + x] = source.IsProtected(x, y) && cells[y * W + x] != 0;
            var context = new TerrainGenerationContext(source, planet, cells, protection, soft, cancelled);
            pipeline = pipeline ?? TerrainGenerationPipeline.Default;
            pipeline.Run(TerrainGenerationStage.AfterCave, context, report);
            int left = planet.DockColumn - planet.LandingWidth / 2;
            int right = left + planet.LandingWidth - 1;
            for (int y = 0; y < planet.DockRow; y++)
            {
                CheckCancellation(cancelled);
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    cells[i] = 0; protection[i] = false; soft[i] = false;
                }
            }
            pipeline.Run(TerrainGenerationStage.AfterSky, context, report);
            for (int y = planet.DockRow; y < planet.DockRow + 3; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    int i = y * W + x;
                    // 只在飞船泊位铺设平台；保留工作台生成的其余天然洞穴轮廓。
                    cells[i] = 1; soft[i] = false;
                    protection[i] = true;
                }
            }
            pipeline.Run(TerrainGenerationStage.AfterDock, context, report);
            pipeline.Run(TerrainGenerationStage.BeforeGeometry, context, report);
            for (int i = 0; i < cells.Length; i++)
                if (protection[i]) soft[i] = false;
            CheckCancellation(cancelled);
            byte[] shapes = TerrainShapeGeometry.Build(cells, protection, W, H);
            var deposits = new List<TerrainDepositBlueprint>();
            foreach (var deposit in source.Deposits)
                if (!(deposit.X >= left && deposit.X <= right && deposit.Y < planet.DockRow + 3) &&
                    !protection[deposit.Y * W + deposit.X])
                    deposits.Add(deposit);
            CheckCancellation(cancelled);
            var background = new BackgroundBakeDescriptor(worldId, seed, cells, shapes);
            return new PlayableTerrain(worldId, seed, cells, protection, soft, source.Rooms.ToArray(),
                TerrainDepositFootprints.Build(deposits, cells, protection, seed, planet.DockRow), shapes, true, background);
        }
        public static PlayableTerrain Space(string worldId)
        {
            // 初始环境沿用现有 AMP1 地图生命周期；只有协议要求的底边基岩，没有可开采星球。
            // 这不是一颗虚构星球：船舱关闭，地面玩法门控由航程阶段负责。
            const string seed = SpaceSeed;
            var cells = new byte[W * H];
            var protection = new bool[cells.Length];
            var shapes = new byte[cells.Length];
            for (int x = 0; x < W; x++)
            {
                int i = (H - 1) * W + x;
                cells[i] = 8; protection[i] = true;
            }
            var background = new BackgroundBakeDescriptor(worldId, seed, cells, shapes);
            return new PlayableTerrain(worldId, seed, cells, protection, new bool[cells.Length],
                Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>(), shapes, true, background);
        }

        private static void CheckCancellation(Func<bool> cancelled)
        {
            if (cancelled != null && cancelled()) throw new OperationCanceledException("星球候选生成已取消。");
        }

    }
}
