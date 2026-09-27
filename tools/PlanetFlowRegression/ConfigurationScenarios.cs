using System;
using DarkNights.Core.Config.Expedition;

namespace DarkNights.Tools.PlanetFlowRegression
{
    /// <summary>星球冻结配置的可接受边界与拒绝输入；显式覆盖非有限数、地图包络和稳定内容标识。</summary>
    internal static class ConfigurationScenarios
    {
        public static void Run(ScenarioReport r)
        {
            var p = new PlanetDefinition("grey-pine", "灰松星");
            r.Check(p.DockX == 568 && p.DockHeight == 0 && p.LandingWidth == 32, "默认泊位与已有船体坐标保持一致");
            r.Check(p.Seed == "" && p.Enabled && p.StarCount == 120, "默认随机种子与表现目录冻结");
            r.Reject(() => new PlanetDefinition("Upper", "星球"), "拒绝大小写不稳定ID");
            r.Reject(() => new PlanetDefinition(" bad ", "星球"), "拒绝空白ID");
            r.Reject(() => new PlanetDefinition(new string('a', 49), "星球"), "拒绝超长ID");
            r.Reject(() => new PlanetDefinition("id", ""), "拒绝空名称");
            r.Reject(() => new PlanetDefinition("id", "星球", new string('字', 513)), "拒绝超长说明");
            r.Reject(() => new PlanetDefinition("id", "星球", seed: " "), "空白种子不可假装随机");
            r.Reject(() => new PlanetDefinition("id", "星球", seed: new string('s', 81)), "拒绝超长种子");
            r.Reject(() => new PlanetDefinition("id", "星球", landingWidth: 27), "拒绝不足船体安全余量的平台");
            r.Reject(() => new PlanetDefinition("id", "星球", landingWidth: 33), "拒绝非偶数平台宽度");
            r.Reject(() => new PlanetDefinition("id", "星球", dockColumn: 2), "拒绝越出左界的平台");
            r.Reject(() => new PlanetDefinition("id", "星球", dockColumn: 318), "拒绝越出右界的平台");
            r.Reject(() => new PlanetDefinition("id", "星球", dockRow: 27), "拒绝不支持的高平台行");
            r.Reject(() => new PlanetDefinition("id", "星球", dockRow: 49), "拒绝不支持的低平台行");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
            {
                r.Reject(() => new PlanetDefinition("id", "星球", arrivalHeight: invalid), "拒绝非法到达高度 " + invalid);
                r.Reject(() => new PlanetDefinition("id", "星球", horizontalRange: invalid), "拒绝非法横向范围 " + invalid);
                r.Reject(() => new PlanetDefinition("id", "星球", maximumLift: invalid), "拒绝非法升高 " + invalid);
                r.Reject(() => new PlanetDefinition("id", "星球", transitSeconds: invalid), "拒绝非法过场时长 " + invalid);
                r.Reject(() => new PlanetDefinition("id", "星球", starSpeed: invalid), "拒绝非法星点速度 " + invalid);
            }
            r.Reject(() => new PlanetDefinition("id", "星球", arrivalHeight: 449), "拒绝到达点超过飞行上界");
            r.Reject(() => new PlanetDefinition("id", "星球", maximumLift: 480), "屋顶净空纳入飞行上界");
            r.Reject(() => new PlanetDefinition("id", "星球", horizontalRange: 390), "船壳半宽纳入左右边界");
            r.Reject(() => new PlanetDefinition("id", "星球", transitionKind: "script"), "拒绝未知表现策略");
            r.Reject(() => new PlanetDefinition("id", "星球", spaceColorHex: "060C20"), "拒绝非标准颜色长度");
            r.Reject(() => new PlanetDefinition("id", "星球", skyColorHex: "#00XYZZ"), "拒绝非法颜色字符");
            r.Reject(() => new PlanetDefinition("id", "星球", starCount: 513), "限制本地星点容量");
            foreach (string strategy in new[] { "star-shift", "fade", "none" })
                r.Check(new PlanetDefinition("id", "星球", transitionKind: strategy).TransitionKind == strategy,
                    "接受已注册表现策略 " + strategy);
            var high = new PlanetDefinition("high", "高平台", dockRow: 28, maximumLift: 256);
            r.Check(high.DockHeight == 192 && high.MaximumLift + 176 <= high.DockRow * 16, "非默认高平台坐标与屋顶余量");
        }
    }
}
