using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 同一地图的额外设计任务；巢穴与人工塌方按类型组织，清单／画布选择共用窗口导航状态。
    /// 只组合已有站点数据，不把清单变为多点编辑器，不新增生物方向、伤害或运行事件系统。
    /// </summary>
    internal static class MapWorkbenchExtrasPage
    {
        internal static void Build(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            if (owner.State.Extra == "scenery")
            { form.Title("植被与装饰", "在当前地形上布置表现锚点，不生成或修改地形矿区。"); MapWorkbenchSceneryPage.Build(form); return; }
            if (owner.State.Extra == "collapse")
            { form.Title("塌方设计", "人工站点在第八步验证并写入完整候选，不自动修正地形。"); MapWorkbenchGeologyPage.Build(form); return; }
            form.Title("侧巢", "读取同一张地图的侧巢配置与实际生成结果。");
            form.Basic("侧巢");
            if (owner.Draft.Config.CaveMap.Minerals.Algorithm != Core.Config.Terrain.TerrainMineralSettings.BaseDeposits)
                form.Field("Config.CaveMap.Minerals.Nests", "允许生成侧巢");
            else form.Note("基础矿床不生成侧巢。");
            form.Note("巢位读取同批矿群与围岩；不是每处富矿必有巢。当前支持既有右侧出巢，不支持任意手动移位。");
            var map = owner.Preview.Displayed;
            form.Heading("当前地图的侧巢");
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
                        owner.Notice = "已定位侧巢。";
                        owner.RebuildPage();
                    });
                    button.EnableInClassList("map-selected", owner.State.SelectedNest == site.Id);
                }
            var why = form.Advanced("侧巢");
            foreach (string message in owner.Generation.ContentDiagnostics) form.Note(message, why);
            form.Heading("制作实验");
            form.Note("局部矿形、巢口动画与揭露回放仅存本机，不写入本图。");
            form.Button("打开本机矿巢实验", () =>
            { owner.State.ShowStudy = true; owner.State.AdvancedPage = "study"; owner.SelectPage("advanced"); });
        }
    }
}
