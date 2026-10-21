using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图候选的静态核对、冻结版本与新局装配页；各步骤显示真实门控，不把保存或生成冒充运行验收。
    /// 旧版本和存档保留，快速入口仅打开Bootstrap而不自动Play，也不承诺尚未接线的候选直达试玩。
    /// </summary>
    internal sealed class TerrainWorkbenchReleasePage
    {
        private readonly TerrainWorkbenchFields form;
        private Label saved, fresh, checkedState, reference;
        private HelpBox reason;
        private Button check, freeze, install;
        internal TerrainWorkbenchReleasePage(TerrainWorkbenchFields form)
        {
            this.form = form; var owner = form.Owner;
            form.Title("检查与使用", "保存草稿 ≠ 更新候选 ≠ 创建版本 ≠ 设为新局。");
            saved = form.Note(""); fresh = form.Note(""); checkedState = form.Note("");
            form.Button("保存地图草稿", owner.Commands.SaveDraft);
            form.Button("更新当前候选", owner.Commands.Generate);
            check = form.Button("核对静态数据（不检查通行）", owner.Commands.Check);
            reason = form.Help("");
            freeze = form.Button("创建冻结新版本…", owner.Commands.Freeze, primary: true);
            var use = new Toggle("创建后同时设为正式新局");
            use.SetValueWithoutNotify(owner.State.UseForNewGame);
            use.RegisterValueChangedCallback(change => owner.State.UseForNewGame = change.newValue);
            form.Root.Add(use);
            form.Note("关闭时只创建新资产；开启时确认后更换新局引用。旧版本、现有地图与旧存档保持。");
            form.Heading("已创建／所选冻结版本");
            var version = new ObjectField("冻结地图")
            { objectType = typeof(Runtime.Terrain.ReviewedMineMap), allowSceneObjects = false };
            version.SetValueWithoutNotify(owner.State.FrozenVersion);
            version.RegisterValueChangedCallback(change =>
            {
                owner.State.FrozenVersion = (Runtime.Terrain.ReviewedMineMap)change.newValue;
                Refresh();
            });
            form.Root.Add(version);
            form.Button("只读查看所选版本", () => owner.Commands.ViewVersion(owner.State.FrozenVersion));
            install = form.Button("将所选版本用于新局…", owner.Commands.Install);
            reference = form.Note("");
            form.Heading("正式试玩入口");
            form.Button("打开 Bootstrap（不自动Play）", owner.Commands.OpenBootstrap);
            form.Note("普通建房加载正式新局引用。F1矿镐快速局仍独立生成，不能核对当前冻结版；未装配候选直达试玩尚未接线。");
            form.Note("静态核对不代表人物通行、真实采掘、联机、保存恢复或发布验收通过。");
            Refresh();
        }

        internal void Refresh()
        {
            var owner = form.Owner; var model = owner.Generation;
            saved.text = "作者草稿：" + (model.HasChanges ? "未保存" : "已保存");
            fresh.text = "候选：" + (model.CandidateFresh ? "与草稿一致" : "缺失或过期");
            bool verified = owner.State.CheckedIdentity == model.Identity;
            checkedState.text = "静态数据：" + (verified ? "当前候选已核对" : "尚未核对／输入已变化");
            reason.text = owner.Commands.FreezeReason();
            if (reason.text.Length == 0) reason.text = "可创建新版本；不会在导出时重新生成地图。";
            freeze.SetEnabled(owner.Commands.FreezeReason().Length == 0);
            check.SetEnabled(!model.HasChanges && model.CandidateFresh && !owner.Preview.Map.HasChanges);
            check.tooltip = model.ExportBlockReason();
            install.SetEnabled(owner.State.FrozenVersion != null && !model.HasChanges && !owner.Preview.Map.HasChanges);
            reference.text = "正式新局引用：" + (model.InstalledConfig.ReviewedMap == null
                ? "尚无冻结版" : AssetDatabase.GetAssetPath(model.InstalledConfig.ReviewedMap));
        }
    }
}
