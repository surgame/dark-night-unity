using DarkNights.Core.Config.Terrain;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 新规则的空间任务页；只编辑同一作者草稿的入口、拓扑、矿室及空间种子，不承担内容结算。
    /// 已有作者图不显示无效随机参数；更多参数渐进展开，变更只标记候选过期。
    /// </summary>
    internal static class TerrainWorkbenchSpacePage
    {
        internal static void Build(TerrainWorkbenchFields form)
        {
            var owner = form.Owner;
            form.Title("空间生成", "参数变更不偷偷换图；更新候选后才替换画布。");
            if (!owner.Generation.RuleMode)
            {
                form.Help("当前为已有作者图／旧随机来源。原始格子与作者资源保持；新规则另建草稿。");
                form.Button("切换为新规则草稿", owner.Commands.NewRules, primary: true);
                form.Field("Config.CaveMap.Seed", "预览／复现种子");
                form.Field("Config.CaveMap.Amplitude", "地表起伏");
                form.Button("更新完整候选", owner.Commands.Generate);
                return;
            }
            const string path = "Config.CaveMap.Rules.";
            form.Heading("道路结构");
            form.Choices(path + "Road", CaveRuleSettings.Roads,
                new[] { "侵蚀曲廊", "断层折返", "洞厅星簇", "裂谷侧坡", "密岔根网" });
            form.Options(path + "Entrance", "入口形状", CaveRuleSettings.Entrances,
                new[] { "凹谷侧入口", "半塌陷洞口", "山脚横洞", "天然石桥", "干涸河床", "巨石压顶", "断层阶台", "根系裂口", "单向井对照" });
            form.Options(path + "RoomMix", "矿室配比", CaveRuleSettings.Mixes,
                new[] { "混合地质", "裂隙与长缝", "大洞厅与岩台", "矿窝与双腔" });
            form.Field("Config.CaveMap.Seed", "空间种子");
            var more = form.Fold("更多空间参数");
            form.Field(path + "SpaceRevision", "空间修订", more);
            form.Field(path + "RoomCount", "请求矿室数", more);
            form.Field(path + "RoomScale", "矿室尺度", more);
            form.Field(path + "RoadRadius", "道路半径", more);
            form.Field(path + "BranchShare", "支线占比", more);
            form.Field(path + "BranchDepth", "支线层级上限", more);
            form.Field(path + "RockGap", "支线封岩比例", more);
            form.Field("Config.CaveMap.Amplitude", "地表起伏", more);
            form.Button("更新候选", owner.Commands.Generate, primary: true);
            form.Note("不检查人物通行，不自动修路；声明连接不等于人物合法路径。");
            if (owner.Generation.GeologyConflict.Length != 0)
            {
                form.Help("保留的塌方草稿与此候选冲突，未进入实际地图；创建冻结版前需修正或停用。",
                    UnityEngine.UIElements.HelpBoxMessageType.Warning);
                form.Button("处理塌方草稿 →", () =>
                { owner.State.Extra = "collapse"; owner.SelectPage("extras"); });
            }
            var diagnostics = form.Fold("生成诊断与跳过原因");
            foreach (string line in owner.Generation.ContentDiagnostics) form.Note(line, diagnostics);
        }
    }
}
