using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>独立地图保存与正式新局的兼容入口；只操作已保存文档，不再组合旧候选保存和装配开关。</summary>
    internal sealed class MapWorkbenchReleasePage
    {
        private readonly MapWorkbenchFields form;
        private readonly Label status;
        private readonly Button install;
        internal MapWorkbenchReleasePage(MapWorkbenchFields form)
        {
            this.form = form;
            form.Title("地图文件", "保存完整结果和制作配方；重新打开沿用原地图，按需用于后续新局。");
            form.Basic("保存与使用");
            status = form.Note("");
            form.Note("当前新局直接进入自然地表，不创建飞船；保存及使用不检查停泊条件。");
            form.Button("保存地图", form.Owner.Commands.SaveDraft, primary: true);
            form.Button("另存为地图…", () => TerrainDocumentStore.SaveAs(form.Owner));
            form.Button("打开配置文件…", () => MapWorkbenchFiles.OpenPicker(form.Owner));
            form.Button("核对当前静态数据", form.Owner.Commands.Check);
            install = form.Button("将已保存地图用于新局", form.Owner.Commands.Install);
            form.Advanced("运行入口");
            form.Button("新建配置", () => MapWorkbenchFiles.New(form.Owner));
            form.Button("导入已有配方／预设", () => form.Owner.SelectPage("recipe"));
            form.Button("打开 Bootstrap", form.Owner.Commands.OpenBootstrap);
            form.Note("不会自动Play；静态核对不代表人物通行、真实采掘、联机或保存恢复已验收。");
            Refresh();
        }
        internal void Refresh()
        {
            var owner = form.Owner;
            status.text = owner.State.Document == null ? "尚未保存为地图文档。" :
                "已保存：" + UnityEditor.AssetDatabase.GetAssetPath(owner.State.Document);
            install.SetEnabled(owner.State.Document != null && !owner.Generation.HasChanges &&
                !owner.Generation.CandidateNeedsSave && !owner.Styles.HasChanges);
        }
    }
}
