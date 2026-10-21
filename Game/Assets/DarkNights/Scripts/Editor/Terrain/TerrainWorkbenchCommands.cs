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
    internal sealed class TerrainWorkbenchCommands
    {
        private readonly TerrainStylePreviewWindow owner;
        private TerrainGenerationPreview Model => owner.Generation;
        internal TerrainWorkbenchCommands(TerrainStylePreviewWindow owner) { this.owner = owner; }

        internal void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("先退出Play，再编辑地图。");
            if (owner.Preview.Map.HasChanges)
                throw new InvalidOperationException("先到高级与兼容取消临时拆填；实验格子不能保存或导出。");
        }

        internal void Edit(string title, Action edit)
        {
            Guard(); Undo.RecordObject(owner.Draft, title); edit();
            EditorUtility.SetDirty(owner.Draft); owner.DraftChanged();
        }

        internal void NewRules()
        {
            Edit("选择新规则地图来源", () =>
            {
                if (owner.Draft.Config.MineMap != null) owner.State.PreviousAuthor = owner.Draft.Config.MineMap;
                owner.Draft.Config.MineMap = null;
                if (owner.Draft.Config.CaveMap.Rules == null) owner.Draft.Config.CaveMap.Rules = new CaveRuleSettings();
                owner.Draft.Config.CaveMap.Rules.Enabled = true;
            });
            Generate(); owner.SelectPage("space");
        }

        internal void UseAuthor(MineMapAsset map)
        {
            Edit("选择已有作者地图", () =>
            {
                owner.Draft.Config.CaveMap.Rules.Enabled = false;
                owner.Draft.Config.MineMap = map;
                if (map != null) owner.State.PreviousAuthor = map;
            });
            owner.SelectPage("source");
            owner.Notice = map == null ? "请选择作者图，再更新候选；旧资产不会删除。" : "作者图已选入草稿，更新候选后显示。";
        }

        internal void Generate()
        {
            Guard(); owner.State.ViewInstalled = false; owner.State.Version = null;
            owner.State.CandidateMode = true; owner.State.CheckedIdentity = "";
            Model.RequestGeneration(); owner.Preview.RequestOpen();
            owner.Notice = "正在按当前作者输入生成完整候选；正式新局引用不改变。";
        }

        internal void ContentOnly()
        {
            if (!Model.RuleMode) throw new InvalidOperationException("只重排内容适用于新规则；已有作者图使用完整候选更新。");
            Edit("只重排地图内容", () =>
                owner.Draft.Config.CaveMap.Rules.ContentRevision = checked(owner.Draft.Config.CaveMap.Rules.ContentRevision + 1));
            Generate();
        }

        internal void ViewInstalled()
        {
            Guard(); owner.State.ViewInstalled = true; owner.State.Version = null;
            owner.Preview.RequestOpen(); owner.Notice = "只切换查看正式引用；作者草稿保留。";
        }

        internal void ViewCandidate()
        {
            Guard(); owner.State.ViewInstalled = false; owner.State.Version = null;
            if (Model.LastCandidate == null) Generate();
            else owner.Preview.RequestOpen();
        }

        internal void ViewVersion(ReviewedMineMap version)
        {
            Guard();
            if (version == null) throw new InvalidOperationException("先选择冻结版本。");
            owner.State.FrozenVersion = version; owner.State.Version = version; owner.State.ViewInstalled = false;
            owner.Preview.RequestOpen(); owner.Notice = "只读查看冻结版；不会设为新局引用。";
        }

        internal void SaveDraft()
        {
            Model.Apply();
            owner.Notice = "地图／航程作者草稿已保存；没有创建版本或更换新局，旧存档保持。";
        }

        internal void Check()
        {
            Guard(); Model.CheckCandidate();
            owner.State.CheckedIdentity = Model.Identity;
            owner.Notice = "当前候选静态数据核对通过；未执行人物通行、Play、联机或恢复验收。";
            owner.RebuildPage();
        }

        internal string FreezeReason()
        {
            if (owner.Preview.Map.HasChanges) return "先取消临时拆填；实验不参与冻结。";
            string reason = Model.ExportBlockReason();
            if (reason.Length != 0) return reason;
            return owner.State.CheckedIdentity == Model.Identity ? "" : "先核对当前候选静态数据。";
        }

        internal void Freeze()
        {
            Guard(); string reason = FreezeReason();
            if (reason.Length != 0) throw new InvalidOperationException(reason);
            bool install = owner.State.UseForNewGame;
            if (!EditorUtility.DisplayDialog("创建冻结地图新版本",
                "读取当前同一候选，不重新生成。\n" + (install ? "创建后同时更换正式新局引用。" : "只创建新版本，正式新局引用保持。") +
                "\n旧版资产、已有地图和旧存档保留。", "创建新版本", "返回")) return;
            var asset = Model.ExportJoint(install);
            owner.State.FrozenVersion = asset;
            owner.State.Version = asset; owner.State.ViewInstalled = false;
            Model.ObserveExternalChanges(); owner.Preview.RequestOpen();
            owner.Notice = "已创建：" + AssetDatabase.GetAssetPath(asset) + (install ? "；已设为新局。" : "；未设为新局。");
            owner.RebuildPage();
        }

        internal void Install()
        {
            Guard();
            if (Model.HasChanges) throw new InvalidOperationException("先保存或取消作者草稿，不能装配与草稿混合的版本。");
            var asset = owner.State.FrozenVersion;
            if (asset == null) throw new InvalidOperationException("先选择冻结版本。");
            asset.VerifySource(owner.Draft.Config);
            if (!EditorUtility.DisplayDialog("更换正式新局地图", AssetDatabase.GetAssetPath(asset) +
                "\n只影响后续新局；旧版与旧存档保留。", "用于新局", "返回")) return;
            JointMineContentSetup.Install(asset); Model.ObserveExternalChanges();
            owner.State.ViewInstalled = true; owner.State.Version = null;
            owner.Preview.RequestOpen(); owner.RebuildPage();
            owner.Notice = "正式新局已引用所选冻结版；当前运行局与旧存档不重生成。";
        }

        internal void SaveStyles() => SaveStyles(false);

        internal void SaveStyles(bool confirmed)
        {
            if (!owner.Styles.HasChanges) { owner.Notice = "共享表现没有未保存修改。"; return; }
            if (!confirmed && !EditorUtility.DisplayDialog("保存共享表现资产", owner.Styles.DescribeChanges() +
                "\n其他引用这些资产的地图也会受到影响。", "保存列出资产", "返回")) return;
            int count = owner.Styles.Apply(); owner.Styles.Clear();
            owner.Preview.RequestStyle(); owner.RebuildPage();
            owner.Notice = "已保存 " + count + " 份共享表现资产；地图与本机实验预设未保存。";
        }

        internal void OpenBootstrap()
        {
            Guard();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(EnvironmentValidation.ScenePath);
            owner.Notice = "已打开Bootstrap，未自动Play；普通建房加载正式冻结引用。F1矿镐快速局独立生成，不用于核对本冻结版。";
        }
    }
}
