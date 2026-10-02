using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Save;
using DarkNights.Core.ViewData;

namespace DarkNights.Tools.PlanetFlowRegression
{
    /// <summary>存档和网络共用冻结合同的阶段、身份、驾驶关系及包络检查；完整文件写盘和会话恢复另由集成验收负责。</summary>
    internal static class JourneyScenarios
    {
        private const string JourneyId = "11111111111111111111111111111111";
        private const string MapId = "22222222222222222222222222222222";
        private static readonly string Fingerprint = new string('a', 64);
        private static readonly PlanetDefinition Planet = new PlanetDefinition("test", "测试星球");
        private static readonly ExpeditionDefinition Rules = new ExpeditionDefinition();

        public static void Run(ScenarioReport r)
        {
            foreach (JourneyPhase phase in Enum.GetValues(typeof(JourneyPhase)))
            {
                var journey = Data(phase);
                r.Check(JourneyValidator.Validate(journey) == "", phase + "网络接受合法阶段");
                bool stable = phase is JourneyPhase.Orbit or JourneyPhase.Descent or JourneyPhase.Landed;
                r.Check((JourneyValidator.Validate(journey, true) == "") == stable, phase + "存档只接受稳定阶段");
                r.Check(Validate(Expedition(journey)) == "", phase + "合法船体乘员与地面阶段关系");
            }
            r.Check(JourneyValidator.Validate(null) == "", "未启用旧模式允许没有航程合同");
            Reject(r, Data(enabled: false), "启用字段必须与航程合同一致");
            Reject(r, Data(phase: (JourneyPhase)999), "拒绝未知航程枚举");
            Reject(r, Data(revision: 0), "拒绝无效航程修订");
            Reject(r, Data(id: "invalid"), "拒绝非法航程身份");
            Reject(r, Data(id: new string('0', 32)), "拒绝空GUID航程身份");
            Reject(r, Data(mapId: "invalid"), "拒绝非法地图身份");
            Reject(r, Data(mapId: new string('0', 32)), "拒绝空GUID地图身份");
            Reject(r, Data(seed: " "), "拒绝空白实际种子");
            Reject(r, Data(seed: new string('s', 81)), "拒绝超长实际种子");
            Reject(r, Data(fingerprint: new string('z', 64)), "拒绝非十六进制指纹");
            Reject(r, Data(fingerprint: "short"), "拒绝错误指纹长度");
            Reject(r, Data(error: new string('e', 513)), "拒绝超长错误字段");
            foreach (double elapsed in new[] { -1, double.NaN, double.PositiveInfinity, 1000001 })
                Reject(r, Data(elapsed: elapsed), "拒绝非法阶段时钟 " + elapsed);
            Reject(r, Data(planets: Array.Empty<PlanetDefinition>()), "拒绝空目录");
            Reject(r, Data(planets: new[] { Planet, Planet }), "拒绝重复星球身份");
            Reject(r, Data(planets: new PlanetDefinition[] { null }), "拒绝空星球行");
            Reject(r, Data(planets: new[] { new PlanetDefinition("test", "停用", enabled: false) }), "拒绝全部停用目录");
            Reject(r, Data(planets: Enumerable.Range(0, 33).Select(n => new PlanetDefinition("p" + n, "星球")).ToArray()), "拒绝超限目录");
            Reject(r, Data(planetId: "missing"), "拒绝失效目的地引用");
            Reject(r, Data(JourneyPhase.Orbit, id: JourneyId), "太空待命不得保留航程身份");
            Reject(r, Data(JourneyPhase.Orbit, elapsed: 1), "太空待命不得保留阶段时间");
            var fixedPlanet = new PlanetDefinition("fixed", "固定", seed: "FIXED-SEED");
            r.Check(JourneyValidator.Validate(Data(planetId: "fixed", seed: "FIXED-SEED", planets: new[] { fixedPlanet })) == "", "固定种子与目录一致");
            Reject(r, Data(planetId: "fixed", seed: "WRONG-SEED", planets: new[] { fixedPlanet }), "固定种子不可替换");
            r.Check(JourneyValidator.Validate(Data(seed: "BASE-retry2")) == "", "随机实际衍生种子允许保存");
            r.Check(JourneyValidator.Validate(Data(), true, MapId) == "", "在星球上保存匹配当前地图身份");
            r.Check(JourneyValidator.Validate(Data(), true, JourneyId) != "", "拒绝跨地图身份存档");
            r.Check(JourneyValidator.Validate(Data(JourneyPhase.Preparing), false, JourneyId) == "", "提交前目的地图与活动太空图允许不同");
            CheckRelationships(r);
            CheckEnvelopes(r);
            var input = new[] { Planet };
            var frozen = Data(planets: input); input[0] = fixedPlanet;
            r.Check(frozen.Planets[0].Id == "test", "网络与保存目录冻结时复制集合");
        }

        private static void CheckRelationships(ScenarioReport r)
        {
            r.Check(Validate(Expedition(Data(JourneyPhase.Orbit), pilot: 1)) != "", "太空待命不能占用驾驶席");
            r.Check(Validate(Expedition(Data(JourneyPhase.Preparing), pilot: 0)) != "", "准备阶段必须有驾驶者");
            r.Check(Validate(Expedition(Data(JourneyPhase.Transit), pilot: 0)) != "", "过场阶段必须有驾驶者");
            r.Check(Validate(Expedition(Data(JourneyPhase.ArrivalSync), velocity: 1)) != "", "同步期间禁止残留推力");
            r.Check(Validate(Expedition(Data(JourneyPhase.Descent), velocity: 1)) == "", "合法驾驶速度仅在下降阶段可用");
            r.Check(Validate(Expedition(Data(), velocity: 61)) != "", "驾驶速度不能超过规则");
            r.Check(Validate(Expedition(Data(), boarded: false)) != "", "航行中禁止舱外船员");
            r.Check(Validate(Expedition(Data(), pilot: 99)) != "", "驾驶者必须引用有效已登船角色");
            r.Check(Validate(Expedition(Data(), shipPhase: 0)) != "", "下降中不能打开舱门");
            r.Check(Validate(Expedition(Data(), groundPhase: 1)) != "", "下降中不能开启地面结算");
            r.Check(Validate(Expedition(Data(JourneyPhase.Landed), shipPhase: 3)) != "", "着陆阶段不能仍处于飞行");
            r.Check(Validate(Expedition(Data(JourneyPhase.Landed), groundPhase: 0)) != "", "着陆后必须开始地面阶段");
            r.Check(Validate(Expedition(Data(JourneyPhase.Landed), boarded: false, pilot: 0)) == "", "合法着陆后允许船员下船");
            r.Check(Validate(Expedition(Data(JourneyPhase.Descent), pilot: 0)) == "", "下降阶段允许驾驶者离线安全悬停");
        }

        private static void CheckEnvelopes(ScenarioReport r)
        {
            var high = new PlanetDefinition("high", "高平台", dockRow: 28, maximumLift: 256);
            var journey = Data(planetId: high.Id, planets: new[] { high });
            var ship = new ExpeditionShipData(2, 3, 1, 0, 0, 0, high.DockX, high.DockHeight);
            r.Check(JourneyValidator.MaximumGroundHeight(journey, 300) == 492, "高平台增加地面角色高度上限");
            r.Check(JourneyValidator.MaximumCrewHeight(journey) == 616, "最高船舱高度包含平台升高与船顶");
            r.Check(ExpeditionShipValidator.Transform(ship, 2, high.DockX + high.HorizontalRange, high.DockHeight + high.MaximumLift, Rules.Ship, journey), "飞行边界合法点可恢复");
            r.Check(!ExpeditionShipValidator.Transform(ship, 2, high.DockX + high.HorizontalRange + .1f, high.DockHeight, Rules.Ship, journey), "拒绝越出水平包络");
            r.Check(!ExpeditionShipValidator.Transform(ship, 2, high.DockX, high.DockHeight + high.MaximumLift + .1f, Rules.Ship, journey), "拒绝越出垂直包络");
            r.Check(!ExpeditionShipValidator.Transform(ship, 2, high.DockX, high.DockHeight - .1f, Rules.Ship, journey), "拒绝落入地表下方");
            r.Check(!ExpeditionShipValidator.Transform(ship, 2, float.NaN, high.DockHeight, Rules.Ship, journey), "拒绝非有限船体位置");
            var wrongDock = new ExpeditionShipData(2, 3, 1, 0, 0, 0, 568, 0);
            r.Check(!ExpeditionShipValidator.Transform(wrongDock, 2, 568, 0, Rules.Ship, journey), "非默认平台不可沿用旧泊位");
        }

        private static void Reject(ScenarioReport r, JourneyViewData data, string name) =>
            r.Check(JourneyValidator.Validate(data) != "", name);

        private static string Validate(ExpeditionViewData data) =>
            ExpeditionValidator.Validate(data, new[] { 1 }, new[] { 2 }, Array.Empty<int>(), Rules);

        private static JourneyViewData Data(JourneyPhase phase = JourneyPhase.Descent, bool enabled = true,
            string id = null, int revision = 1, string planetId = null, string seed = null, string mapId = null,
            string fingerprint = null, double elapsed = 0, string error = "", PlanetDefinition[] planets = null)
        {
            bool orbit = phase == JourneyPhase.Orbit;
            return new JourneyViewData(enabled, id ?? (orbit ? "" : JourneyId), revision, phase,
                planetId ?? (orbit ? "" : Planet.Id), seed ?? (orbit ? "" : "FLOW-SEED"), mapId ?? (orbit ? "" : MapId),
                fingerprint ?? Fingerprint, elapsed, error, planets ?? new[] { Planet });
        }

        private static ExpeditionViewData Expedition(JourneyViewData journey, int? pilot = null, float velocity = 0,
            bool boarded = true, int? shipPhase = null, int? groundPhase = null)
        {
            bool landed = journey.Phase == JourneyPhase.Landed;
            var crew = new[] { new ExpeditionActorData(1, 0, 0, 0, 0, 0, 0, 0, boarded) };
            var devices = new[] { new ExpeditionDeviceData(2, 0, 0, 0, 0, 0, 568, 0, true) };
            var ship = new ExpeditionShipData(2, shipPhase ?? (landed ? 0 : 3), pilot ?? (journey.Phase == JourneyPhase.Orbit ? 0 : 1), velocity, 0, 0, 568, 0);
            return new ExpeditionViewData(1, groundPhase ?? (landed ? 1 : 0), 0, 0, false, 0, 0, 0, 0, 0, crew, devices,
                ship: ship, journey: journey);
        }
    }
}
