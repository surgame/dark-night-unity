using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 同一地图的额外设计任务；巢穴与人工塌方按类型组织，清单／画布选择共用窗口导航状态。
    /// 只组合已有站点数据，不把清单变为多点编辑器，不新增生物方向、伤害或运行事件系统。
    /// </summary>
    internal static class TerrainWorkbenchExtrasPage
    {
        internal static void Build(TerrainWorkbenchFields form)
        {
            var owner = form.Owner;
            form.Title("额外设计", "同一张地图上的站点层；画布标记与右侧属性联动。");
            var tabs = new VisualElement(); tabs.AddToClassList("map-row"); form.Root.Add(tabs);
            var nests = form.Button("巢穴", () =>
            { owner.State.Extra = "nest"; owner.State.Pick = 0; owner.RebuildPage(); }, tabs);
            var geology = form.Button("塌方", () =>
            { owner.State.Extra = "collapse"; owner.State.Pick = 0; owner.RebuildPage(); }, tabs);
            nests.EnableInClassList("map-selected", owner.State.Extra == "nest");
            geology.EnableInClassList("map-selected", owner.State.Extra == "collapse");
            if (owner.State.Extra == "collapse") { TerrainWorkbenchGeologyPage.Build(form); return; }
            if (owner.Generation.RuleMode) form.Field("Config.CaveMap.Rules.Nests", "允许富矿伴侧巢");
            else form.Note("已有作者图的联合巢开关保留在高级与兼容的旧联合配方中。");
            form.Note("巢位读取同批矿群与围岩；不是每处富矿必有巢。当前支持既有右侧出巢，不支持任意手动移位。");
            var map = owner.Preview.Displayed;
            form.Heading("当前画布的冻结站点");
            if (map == null || map.Nests.Count == 0)
                form.Help("当前画布没有侧巢。检查生成结果与空间／预算跳过原因，不强行补点。");
            else
                foreach (var site in map.Nests)
                {
                    var selected = site;
                    var button = form.Button("侧巢 " + site.Id + " · U " + site.U + " / V " + -site.Row, () =>
                    {
                        owner.State.SelectedNest = selected.Id;
                        owner.Preview.FocusCell(selected.U, selected.Row);
                        owner.Notice = "已定位真实冻结站点；标记不代表当前草稿已通过运行验收。";
                        owner.RebuildPage();
                    });
                    button.EnableInClassList("map-selected", owner.State.SelectedNest == site.Id);
                }
            var why = form.Fold("分布与跳过原因");
            foreach (string message in owner.Generation.ContentDiagnostics) form.Note(message, why);
            form.Heading("制作实验");
            form.Note("局部矿形、巢口动画与揭露回放仅存本机，不写入本图。");
            form.Button("打开本机矿巢实验", () =>
            { owner.State.ShowStudy = true; owner.State.AdvancedPage = "study"; owner.SelectPage("advanced"); });
        }
    }
}
