using System;
using System.IO;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 工作台唯一的文件切换入口；新建、打开与导入都完整替换编辑上下文，先保护未保存输入。
    /// 导入仅读取来源，后续保存独立配置；打开文档后保存更新该文档，不在属性页切换生成来源。
    /// </summary>
    internal static class MapWorkbenchFiles
    {
        internal static bool CanReplace(MapWorkbenchWindow owner)
        {
            owner.Commands.Guard(false);
            return !owner.hasUnsavedChanges || EditorUtility.DisplayDialog("切换配置",
                "当前配置有未保存修改，是否放弃这些修改？", "放弃并继续", "返回");
        }

        internal static void New(MapWorkbenchWindow owner)
        {
            if (!CanReplace(owner)) return;
            owner.FileLoad.Run("新建地图", () => CreateNew(owner));
        }

        private static void CreateNew(MapWorkbenchWindow owner)
        {
            var input = new ExpeditionFlowConfig();
            input.CaveMap.Layout = TerrainGenerationSettings.ConnectedRooms;
            input.CaveMap.Structure = new CaveLayoutSettings();
            input.CaveMap.Minerals = new TerrainMineralSettings { Algorithm = TerrainMineralSettings.RegionalVeins };
            input.CaveMap.Rules = null;
            input.CaveMap.SurfaceAlgorithm = TerrainGenerationSettings.LandformSurface;
            Start(owner, input, DefaultStyle());
            owner.Notice = "已新建未命名配置；首次保存时选择文件位置。";
        }

        internal static void Open(MapWorkbenchWindow owner, TerrainMapDocument document)
        {
            if (document == null) return;
            if (document == owner.State.Document && owner.State.ViewedMap != null)
            { ReturnToDraft(owner); return; }
            if (!CanReplace(owner)) return;
            owner.FileLoad.Open(document);
        }

        internal static void ViewFrozen(MapWorkbenchWindow owner, ReviewedMineMap map)
        {
            owner.Commands.Guard(false);
            owner.FileLoad.ViewFrozen(map);
        }

        internal static void ReturnToDraft(MapWorkbenchWindow owner)
        {
            owner.FileLoad.Cancel();
            owner.State.ViewedMap = null;
            owner.Preview.RequestOpen(); owner.RebuildPage();
            owner.Notice = "已返回原编辑配置。";
        }

        internal static void OpenPicker(MapWorkbenchWindow owner)
        {
            string absolute = EditorUtility.OpenFilePanel("打开地图配置", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(absolute)) return;
            string path = FileUtil.GetProjectRelativePath(absolute);
            var document = AssetDatabase.LoadAssetAtPath<TerrainMapDocument>(path);
            if (document == null) throw new InvalidOperationException("请选择本工程中的地图配置文件。导入地形请使用顶部预设入口。");
            Open(owner, document);
        }

        internal static void Import(MapWorkbenchWindow owner, UnityEngine.Object asset)
        {
            if (asset == null) throw new InvalidOperationException("请选择要导入的配置或地形资源。");
            if (!CanReplace(owner)) return;
            if (asset is TerrainMapDocument document)
            { owner.FileLoad.Open(document, true); return; }
            owner.FileLoad.Run("导入 " + asset.name, () => ImportNow(owner, asset));
        }

        private static void ImportNow(MapWorkbenchWindow owner, UnityEngine.Object asset)
        {
            ExpeditionFlowConfig input;
            CaveTerrainStyle style = DefaultStyle();
            string planet = null;
            if (asset is ObjectDefinition definition)
            {
                var config = definition.SharedConfigs.OfType<ExpeditionFlowConfig>().SingleOrDefault();
                if (config == null) throw new InvalidOperationException("所选资源没有地图配置。");
                input = ExpeditionFlowDraft.Clone(config);
                var profile = definition.SharedConfigs.OfType<TerrainProfileConfig>().SingleOrDefault();
                if (profile?.CaveStyle is CaveTerrainStyle appearance) style = appearance;
            }
            else throw new InvalidOperationException("可导入地图配置或含地图配置的会话资源。旧H5底图输入已退场。");
            input.Validate();
            Start(owner, input, style, planet);
            owner.Notice = "已从 " + asset.name + " 创建未命名配置；保存时选择新文件。";
        }

        internal static CaveTerrainStyle DefaultStyle()
        {
            var source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            return source.SharedConfigs.OfType<TerrainProfileConfig>().Single().CaveStyle as CaveTerrainStyle
                ?? throw new InvalidOperationException("当前正式配置缺少地形表现设置。");
        }

        private static void Start(MapWorkbenchWindow owner, ExpeditionFlowConfig input, CaveTerrainStyle style,
            string planet = null)
        {
            Undo.ClearUndo(owner.Draft); owner.Preview.Map.Cancel();
            input.ReviewedMap = null;
            TerrainAuthoringImport.Normalize(input);
            owner.Draft.LoadDocument(input);
            owner.Draft.MarkSaved();
            owner.SetDocumentStyle(style);
            owner.State.Document = null; owner.State.DocumentBaseline = "";
            owner.State.ViewedMap = null;
            owner.State.SelectedRegion = 0; owner.State.SelectedSite = ""; owner.State.Pick = 0;
            owner.Generation.PlanetId = planet ?? input.Planets.FirstOrDefault(p => p != null && p.Enabled)?.Id;
            owner.Commands.Generate(); owner.SelectPage("space");
        }
    }
}
