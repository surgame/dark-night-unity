using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 完整星球候选生成器；消费当前布局、地表及矿物规则，依次完成洞穴、坡形和背景。
    /// 全部输入输出为纯数据，可在后台执行；背景参考必须在最后一次地形修改之后捕获。
    /// </summary>
    public static class PlanetTerrainGenerator
    {
        private const int W = TerrainGenerationSettings.Width;
        private const int H = TerrainGenerationSettings.Height;
        public const string SpaceSeed = "SPACE-CARRIER-V1";
        public const int Version = 17;

        /// <summary>完整地图生成入口；只使用本次明确种子，不在失败时偷偷换种子，最终坡形与背景捕获均在此复用。</summary>
        public static PlayableTerrain GenerateCandidate(PlanetDefinition planet, string seed, string worldId,
            TerrainGenerationSettings input, Func<bool> cancelled = null,
            TerrainGenerationPipeline pipeline = null, Action<TerrainModifierDiagnostic> report = null, Action<string> contentReport = null)
        {
            CheckCancellation(cancelled);
            var diagnostics = report == null ? null : new List<TerrainModifierDiagnostic>();
            var result = Generate(planet, seed, worldId, cancelled, input, pipeline,
                diagnostics == null ? null : new Action<TerrainModifierDiagnostic>(diagnostics.Add), contentReport);
            if (diagnostics != null) foreach (var item in diagnostics) report(item);
            return result;
        }

        public static PlayableTerrain Generate(PlanetDefinition planet, string seed, string worldId, Func<bool> cancelled = null,
            TerrainGenerationSettings input = null, TerrainGenerationPipeline pipeline = null,
            Action<TerrainModifierDiagnostic> report = null, Action<string> contentReport = null)
        {
            return GenerateResult(planet, seed, worldId, input, cancelled, contentReport).Terrain;
        }

        /// <summary>生产制作入口；一份地下计划贯穿雕刻、矿区、预览及保存，所有前景改形后统一派生格形和背景。</summary>
        public static TerrainGenerationResult GenerateResult(PlanetDefinition planet, string seed, string worldId,
            TerrainGenerationSettings input, Func<bool> cancelled = null, Action<string> contentReport = null,
            bool captureStages = false)
        {
            if (planet == null) throw new ArgumentNullException(nameof(planet));
            CheckCancellation(cancelled);
            var settings = (input ?? new TerrainGenerationSettings
            { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile }).CopyValidated();
            settings.Seed = seed;
            settings.SurfaceRows = settings.UsesLandform ?
                PlanetSurfaceRelief.CreateProfile(settings) : null;
            if (settings.Layout == TerrainGenerationSettings.ConnectedRooms &&
                settings.Minerals.Algorithm == TerrainMineralSettings.BaseDeposits)
                throw new ArgumentException("连通洞室没有基础矿床供给；请选择区域矿物或近壁矿团。");
            var recorder = new TerrainGenerationRecorder(captureStages);
            CaveRulePlan plan = settings.Layout == TerrainGenerationSettings.ConnectedRooms ? CaveRulePlanner.Build(settings) : null;
            if (plan != null) foreach (string message in plan.Diagnostics) contentReport?.Invoke(message);
            var source = plan == null ? CaveExplorationGenerator.Generate(settings, recorder) :
                CaveRuleGenerator.Generate(settings, plan, recorder);
            CheckCancellation(cancelled);
            var cells = source.CopyMaterials();
            var protection = new bool[cells.Length];
            var soft = source.CopySoftRock();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    protection[y * W + x] = source.IsProtected(x, y) && cells[y * W + x] != 0;
            for (int i = 0; i < cells.Length; i++)
                if (protection[i]) soft[i] = false;
            CheckCancellation(cancelled);
            byte[] shapes = new byte[cells.Length];
            var deposits = new List<TerrainDepositBlueprint>();
            foreach (var deposit in source.Deposits)
                if (!protection[deposit.Y * W + deposit.X])
                    deposits.Add(deposit);
            CheckCancellation(cancelled);
            var background = new BackgroundBakeDescriptor(worldId, seed, cells, shapes);
            var terrain = new PlayableTerrain(worldId, seed, cells, protection, soft, source.Rooms.ToArray(),
                TerrainDepositFootprints.Build(deposits, cells, protection, seed), shapes, true, background);
            terrain = TerrainContentGenerator.Generate(terrain, source.Settings, plan, contentReport);
            cells = terrain.CopyMaterials(); protection = terrain.CopyProtection();
            recorder.Add("矿区与侧巢局部改形", cells, map: terrain);
            shapes = TerrainShapeGeometry.Build(cells, protection, W, H);
            soft = terrain.CopySoftRock();
            for (int i = 0; i < cells.Length; i++) if (cells[i] == 0 || protection[i]) soft[i] = false;
            background = new BackgroundBakeDescriptor(worldId, seed, cells, shapes);
            var final = new PlayableTerrain(worldId, seed, cells, protection, soft, terrain.Rooms.ToArray(),
                terrain.Deposits.ToArray(), shapes, true, background, nests: terrain.Nests.ToArray());
            recorder.Add("最终坡形与背景", cells, shapes, final);
            recorder.Add("冻结完整候选", cells, shapes, final);
            return new TerrainGenerationResult(final, plan, Version,
                plan == null ? "" : settings.Structure.SpaceKey(seed), recorder.Finish());
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
