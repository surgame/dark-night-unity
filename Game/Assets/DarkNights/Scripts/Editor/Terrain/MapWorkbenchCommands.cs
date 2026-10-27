using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图工作台的显式作者操作；编辑仅写原草稿，保存、静态核对、创建版本与新局装配各自分离。
    /// 所有资产写入沿原冲突检查与Undo入口；不启动运行会话、不删除旧版，也不把预览实验纳入导出。
    /// </summary>
    internal sealed class MapWorkbenchCommands
    {
        private readonly MapWorkbenchWindow owner;
        private TerrainGenerationPreview Model => owner.Generation;
        internal MapWorkbenchCommands(MapWorkbenchWindow owner) { this.owner = owner; }

        internal void Guard(bool editing = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("先退出Play，再编辑地图。");
            if (editing && owner.FileLoad?.Busy == true)
                throw new InvalidOperationException("地图正在读取，请等待完成或取消读取后再编辑。");
            if (editing && owner.State.ViewedMap != null)
                throw new InvalidOperationException("当前地图为只读，请先返回编辑配置。");
            if (owner.Preview.Map.HasChanges)
                throw new InvalidOperationException("先到临时拆填实验取消笔触；实验格子不能保存或导出。");
        }

        internal void Edit(string title, Action edit)
        {
            Guard(); Undo.RecordObject(owner.Draft, title); edit();
            EditorUtility.SetDirty(owner.Draft); owner.DraftChanged();
        }

        internal void Generate()
        {
            Guard();
            owner.State.CheckedIdentity = "";
            Model.RequestGeneration(); owner.Preview.RequestOpen();
            owner.Notice = "正在更新当前配置的预览。";
        }

        internal void Reseed()
        {
            var settings = owner.Draft.Config.CaveMap;
            var selection = (settings.SeedSelection ?? new TerrainSeedSettings()).CopyValidated();
            if (selection.Mode == TerrainSeedSettings.Fixed)
            {
                Generate();
                owner.Notice = "固定种子模式：已复用 " + settings.Seed + "；换图请修改固定种子或选择随机／种子池。";
                return;
            }
            string random = Guid.NewGuid().ToString("N");
            string next = selection.Select(settings.Seed, "MAP-" + random,
                Convert.ToUInt32(random.Substring(0, 8), 16));
            Edit("换种子生成地图", () =>
            {
                settings.Seed = next;
            });
            owner.Notice = "已按种子策略选择 " + next + "；调参和保存继续使用此种子。";
        }

        internal void ContentOnly()
        {
            if (!Model.RegionalMinerals) throw new InvalidOperationException("当前配置使用全图矿物参数。");
            Edit("只重排地图内容", () =>
                owner.Draft.Config.CaveMap.Minerals.Revision = checked(owner.Draft.Config.CaveMap.Minerals.Revision + 1));
            Generate();
        }

        internal void SaveDraft()
        {
            var document = TerrainDocumentStore.Save(owner);
            if (document != null) owner.Notice = "已保存配置：" + AssetDatabase.GetAssetPath(document);
        }

        internal void Check()
        {
            Guard(); Model.CheckCandidate();
            owner.State.CheckedIdentity = Model.Identity;
            owner.Notice = "地图静态数据核对通过；新局不创建飞船。人物支撑与净空在实际开局检查，静态检查不代表通行或采掘验收。";
            owner.RebuildPage();
        }

        internal string FreezeReason()
        {
            if (owner.Preview.Map.HasChanges) return "先取消临时拆填；实验结果不能保存为地图。";
            string reason = Model.ExportBlockReason();
            if (reason.Length != 0) return reason;
            return owner.State.CheckedIdentity == Model.Identity ? "" : "先核对当前候选静态数据。";
        }

        internal void Freeze() => SaveDraft();

        internal void Install() => TerrainDocumentStore.Install(owner);

        internal void OpenBootstrap()
        {
            Guard();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(EnvironmentValidation.ScenePath);
            owner.Notice = "已打开Bootstrap，未自动Play；普通建房加载已指定用于新局的地图。F1矿镐快速局独立生成，不用于核对这份地图。";
        }
    }
}
