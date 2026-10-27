using DarkNights.Core.Config.Terrain;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图规则的空间任务页；只编辑同一作者草稿的入口、拓扑、矿室及空间种子，不承担内容结算。
    /// 种子策略和空间参数来自同一配置；更多参数渐进展开，变更只标记候选过期。
    /// </summary>
    internal static class MapWorkbenchSpacePage
    {
        internal static void Build(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            string page = owner.State.Page;
            if (page == "surface") { Surface(form); return; }
            if (!owner.Generation.HasRoomPlan)
            {
                form.Title(page == "rooms" ? "地下洞室" : page == "roads" ? "道路连接" : "入口形状",
                    "当前布局为天然洞穴，沿用它的自然雕刻规则。");
                form.Basic("当前布局");
                form.Note("九类入口、五类道路和区域矿室属于连通洞室布局；天然洞穴不伪装有这些规划参数。");
                form.Advanced("布局切换");
                form.Button("前往算法与种子", () => owner.SelectPage("recipe"));
                return;
            }
            if (page == "entrance") Entrance(form);
            else if (page == "roads") Roads(form);
            else Rooms(form);
        }

        private static void Surface(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            form.Title("地表轮廓", "先生成自然地表，再雕刻洞穴；调参保留本次种子与地下计划。");
            form.Basic("地表轮廓");
            var surfaceAlgorithm = form.Options("Config.CaveMap.SurfaceAlgorithm", "地表算法",
                new[] { TerrainGenerationSettings.NoiseSurface, TerrainGenerationSettings.LandformSurface },
                new[] { "基础噪声", "地貌轮廓" });
            surfaceAlgorithm.RegisterValueChangedCallback(_ => owner.RebuildPage());
            bool landform = owner.Draft.Config.CaveMap.UsesLandform;
            if (landform)
            {
                form.Options("Config.CaveMap.Surface", "地貌类型", TerrainGenerationSettings.SurfaceNames,
                    new[] { "针峰", "阶地", "喀斯特", "盆地", "丘陵", "断裂地表" });
                form.Field("Config.CaveMap.Relief.HeightCells", "起伏高度（格）");
                form.Field("Config.CaveMap.Relief.WavelengthCells", "起伏尺度（格）");
            }
            form.Note("所选算法直接确定洞穴雕刻前的地表；不按泊位削平或填筑平台。");
            if (!landform) form.Field("Config.CaveMap.Amplitude", "地表起伏倍率");
            form.Advanced("地表轮廓");
            if (landform)
            {
                form.Field("Config.CaveMap.Relief.BaseHeightCells", "基准偏移（格）");
                form.Field("Config.CaveMap.Amplitude", "地表起伏倍率");
            }
            else form.Note("基础噪声沿用既有轮廓采样；切换算法可撤销，不改变地下随机域。");
        }

        private static void Entrance(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            const string path = "Config.CaveMap.Structure.";
            form.Title("入口形状", "只改变入口及其连接段，保持地下洞室和道路计划。");
            form.Basic("入口");
            var entry = form.Options(path + "Entrance", "入口形状 · 9种", CaveLayoutSettings.Entrances,
                new[] { "凹谷侧入口", "半塌陷洞口", "山脚横洞", "天然石桥", "干涸河床", "巨石压顶", "断层阶台", "根系裂口", "单向井对照" });
            entry.RegisterValueChangedCallback(_ => owner.RebuildPage());
            form.Note("只改变入口及其连接段；地下洞室和道路计划保持。");
            form.Advanced("入口");
            var sculpt = form.Field("Config.CaveMap.DisableValleySculpt", "关闭凹谷多边形挖空");
            sculpt.SetEnabled(owner.Draft.Config.CaveMap.Structure.Entrance == "valley");
            sculpt.EnableInClassList("map-readonly", owner.Draft.Config.CaveMap.Structure.Entrance != "valley");
            form.Note("只作用于凹谷侧入口的固定多边形；两段接洞通道保留，关闭后可能被原地表覆住。");
        }

        private static void Rooms(MapWorkbenchFields form)
        {
            const string path = "Config.CaveMap.Structure.";
            form.Title("地下洞室", "调整房间计划与配比，会影响后续道路、入口和内容；保留地表算法与种子。");
            form.Basic("地下洞室");
            form.Options(path + "RoomMix", "矿室配比", CaveLayoutSettings.Mixes,
                new[] { "混合地质", "裂隙与长缝", "大洞厅与岩台", "矿窝与双腔" });
            form.Field(path + "RoomCount", "请求矿室数");
            form.Field(path + "RoomScale", "矿室尺度");
            form.Advanced("地下洞室");
            form.Field(path + "SpaceRevision", "空间修订");
            form.Note("请求数量受可用空间限制，实际数量见画布结果。改变计划后，旧区域覆盖保留并明确报告失效。");
            Diagnostics(form);
        }

        private static void Roads(MapWorkbenchFields form)
        {
            const string path = "Config.CaveMap.Structure.";
            form.Title("道路连接", "结构类型影响空间计划；道路半径只改变走廊宽度，不重抽洞室。");
            form.Basic("道路连接");
            form.Options(path + "Road", "道路结构", CaveLayoutSettings.Roads,
                new[] { "侵蚀曲廊", "断层折返", "洞厅星簇", "裂谷侧坡", "密岔根网" });
            form.Field(path + "RoadRadius", "道路半径");
            var more = form.Advanced("道路连接");
            form.Field(path + "BranchShare", "支线占比", more);
            form.Field(path + "BranchDepth", "支线层级上限", more);
            form.Field(path + "RockGap", "支线封岩比例", more);
            form.Note("不检查人物通行，不自动修路；声明连接不等于人物合法路径。");
            Diagnostics(form);
        }

        private static void Diagnostics(MapWorkbenchFields form)
        {
            var owner = form.Owner;
            if (owner.Generation.GeologyConflict.Length != 0)
            {
                form.Help("保留的塌方草稿与此候选冲突，未进入实际地图；保存前需修正或停用。",
                    UnityEngine.UIElements.HelpBoxMessageType.Warning);
                form.Button("处理塌方草稿 →", () =>
                { owner.State.Extra = "collapse"; owner.SelectPage("extras"); });
            }
            var diagnostics = form.Fold("当前候选诊断");
            foreach (string line in owner.Generation.ContentDiagnostics) form.Note(line, diagnostics);
        }
    }
}
