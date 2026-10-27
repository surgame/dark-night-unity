using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Expedition;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地表环境首张样板的显式装配；复用已保存作者图、矿巢配方及原素材，创建新预制地图和独立装饰资产。
    /// 编辑态仅写声明的WorldSession地表配置与Style环境字段，保留旧图、源资产及玩家存档，不运行会话。
    /// </summary>
    public static class SurfaceEnvironmentContentSetup
    {
        private const string SessionPath = "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset";
        private const string StylePath = "Assets/DarkNights/Res/Terrain/StrataCave/Style.asset";

        [DarkNightsWorkbenchCommand("Dark Nights/Terrain/Surface environment sample", "创建地表起伏样板并用于新局")]
        public static void InstallSample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("先等待编辑器回到空闲编辑态。");
            var source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(SessionPath);
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(StylePath);
            if (source == null || style == null || EditorUtility.IsDirty(source) || EditorUtility.IsDirty(style))
                throw new InvalidOperationException("正式会话／样式缺失或有未保存修改。");
            if (Resources.FindObjectsOfTypeAll<MapWorkbenchWindow>().Length != 0)
                throw new InvalidOperationException("先处理并关闭地图工作台草稿，再装配首张样板。");
            var original = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            if (original.CaveMap.UsesLandform || style.SurfaceEnvironment.Layers.Length != 0)
                throw new InvalidOperationException("地表样板已配置；后续使用地图工作台制作新版本，不覆盖现有环境。");
            original.ReviewedMap.VerifySource(original);
            var config = ExpeditionFlowDraft.Clone(original);
            config.CaveMap.Relief = new SurfaceReliefSettings
            { Enabled = true, HeightCells = 8, WavelengthCells = 84, DockTransitionCells = 16 };
            config.CaveMap.SurfaceAlgorithm = TerrainGenerationSettings.LandformSurface;
            config.CaveMap.Surface = "rolling";
            var diagnostics = new List<string>();
            var candidate = JointMineContentSetup.Build(config, 6, true, diagnostics.Add);
            candidate = GeologyMapDesign.Attach(candidate, config.Geology);
            string recipe = "surface-environment-v1|" + JointMineCandidateRecipe();
            ReviewedMineMap.Encode(candidate, recipe);
            var environment = style.SurfaceEnvironment.Capture();
            environment.Layers = Layers(); environment.DecorationMaterial = DecorationMaterial();
            var layout = SurfaceSceneryAuthoring.Create(candidate, config.PreviewPlanet());
            layout.Capture();
            environment.Layouts = environment.Layouts.Concat(new[] { layout }).ToArray();
            Undo.RecordObjects(new UnityEngine.Object[] { source, style }, "装配地表环境与起伏样板");
            source.SharedConfigs[source.SharedConfigs.IndexOf(original)] = config;
            style.SurfaceEnvironment = environment;
            Undo.FlushUndoRecordObjects();
            EditorUtility.SetDirty(source); EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssetIfDirty(source); AssetDatabase.SaveAssetIfDirty(style);
            var map = JointMineContentSetup.Create(candidate, ReviewedMineMap.SourceFingerprint(config), recipe, true);
            map.VerifySource(config);
            Debug.Log("地表样板已装配：" + AssetDatabase.GetAssetPath(map) + "；装饰 " + layout.Placements.Length +
                " 项；来源 " + map.SourceIdentity + "\n" + string.Join("\n", diagnostics));
        }

        private static string JointMineCandidateRecipe() => "max=6|nests=True|relief-v1";

        private static Material DecorationMaterial()
        {
            SurfaceSceneryAuthoring.EnsureFolder(SurfaceSceneryAuthoring.Root);
            string path = SurfaceSceneryAuthoring.Root + "/SurfaceDecoration.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (existing.shader == null || existing.shader.name != "Dark Nights/Camp Sprite")
                    throw new InvalidOperationException("已有装饰材质合同不符，不能覆盖。");
                return existing;
            }
            var shader = Shader.Find("Dark Nights/Camp Sprite") ?? throw new InvalidOperationException("缺少既有受光Shader。");
            var result = new Material(shader) { name = "SurfaceDecoration" };
            result.SetFloat("_UseGlobalAmbient", 0);
            AssetDatabase.CreateAsset(result, path); AssetDatabase.SaveAssetIfDirty(result);
            return result;
        }

        public static SurfaceBackdropLayer[] Layers()
        {
            return new[]
            {
                Layer("bg_sky_clear", 600, 0, 0, -22, new Color(.47f, .60f, .70f, 1)),
                Layer("bg_Lforest_middle", 150, .07f, 1, 0, new Color(.64f, .73f, .79f, 1)),
                Layer("bg_forest_middle", 100, .16f, 1, 0, new Color(.62f, .72f, .72f, 1)),
                Layer("bg_forest_close", 64, .29f, 1, -.5f, new Color(.63f, .74f, .69f, 1)),
                Layer("bg_forest_new_close", 64, .45f, 1, -.5f, new Color(.68f, .79f, .66f, 1))
            };
        }

        private static SurfaceBackdropLayer Layer(string name, int topPixels, float horizontal, float vertical,
            float bottom, Color tint)
        {
            var texture = SurfaceSceneryAuthoring.Texture(name, 0);
            return new SurfaceBackdropLayer
            {
                Texture = texture, Pixels = new RectInt(0, texture.height - topPixels, texture.width, topPixels),
                HorizontalParallax = horizontal, VerticalParallax = vertical, BottomCells = bottom, Tint = tint
            }.Capture();
        }
    }
}
