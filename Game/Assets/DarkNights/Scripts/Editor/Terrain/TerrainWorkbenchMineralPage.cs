using System.Linq;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 完整候选的矿物分布任务页；矿量、内容随机流与空间输入分离，实际结果只读当前显示快照。
    /// 局部矿形／动画试参仍留在本机实验区，不能假称生产配置或把重新布矿当作运行奖励。
    /// </summary>
    internal static class TerrainWorkbenchMineralPage
    {
        internal static void Build(TerrainWorkbenchFields form)
        {
            var owner = form.Owner;
            form.Title("矿物分布", "只配置生产候选的矿物；巢位统一在额外设计管理。");
            if (owner.Generation.RuleMode)
            {
                const string p = "Config.CaveMap.Rules.";
                form.Field(p + "ContentSeed", "内容种子");
                form.Field(p + "VeinCount", "矿团预算");
                form.Field(p + "Capacity", "基础储量");
                form.Field(p + "ExposedShare", "露矿格比例上限");
                var more = form.Fold("更多矿物参数");
                form.Field(p + "ContentRevision", "内容修订", more);
                form.Field(p + "EmptyShare", "无矿区域比例", more);
                form.Field(p + "RichShare", "富矿区域比例", more);
                form.Button("只重排内容（保持基础空间）", owner.Commands.ContentOnly, primary: true);
                form.Note("矿层与侧巢局部补丁从基础重建，不累积旧改形。");
            }
            else
            {
                form.Help("已有作者图沿用原矿层与联合配方；旧配方参数保留在高级与兼容，不伪装为新规则字段。");
                form.Button("更新完整联合候选", owner.Commands.Generate);
            }
            form.Button("配置富矿伴巢 →", () =>
            { owner.State.Extra = "nest"; owner.SelectPage("extras"); });
            var map = owner.Preview.Displayed;
            if (map != null)
            {
                form.Heading("当前画布的实际数据");
                form.Note(map.Deposits.Count + " 矿床分片 · 储量 " + map.Deposits.Sum(d => d.Capacity));
                form.Note("结果归当前画布；候选过期时不会把新参数显示成已生成结果。");
            }
            var report = form.Fold("区域预算与失败原因", true);
            foreach (string line in owner.Generation.ContentDiagnostics)
                form.Note(line, report);
        }
    }
}
