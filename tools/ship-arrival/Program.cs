using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;

namespace DarkNights.Tools.ShipArrival
{
    /// <summary>飞船表现的有限纯回归；验证到达身份、等待与暂停屏障及船载相对位置，报告不代表 Unity 画面验收。</summary>
    internal static class Program
    {
        private static readonly List<object> results = new List<object>();
        private static int failures;
        private static int Main(string[] args)
        {
            var transit = Frame(1, JourneyPhase.Transit);
            Check(JourneyContinuity.IsArrival(transit, Frame(2, JourneyPhase.ArrivalSync)), "同一航程接续到达");
            Check(JourneyContinuity.IsArrival(transit, Frame(2, JourneyPhase.Descent)), "跳过到达状态的快照仍识别同一航程");
            Check(!JourneyContinuity.IsArrival(transit, Frame(3, JourneyPhase.ArrivalSync)), "拒绝跨越多个epoch");
            Check(!JourneyContinuity.IsArrival(transit, Frame(2, JourneyPhase.ArrivalSync, mapId: "other")), "拒绝不同地图");
            Check(!JourneyContinuity.IsArrival(transit, Frame(2, JourneyPhase.ArrivalSync, journeyId: "other")), "拒绝不同航程");
            Check(!JourneyContinuity.IsArrival(Frame(1, JourneyPhase.Landed), Frame(2, JourneyPhase.Landed)), "读档不重播到达");
            var clock = new JourneyArrivalPresentation();
            clock.Observe(Frame(1, JourneyPhase.Preparing)); clock.Advance(4, false, false, 1.5);
            double travel = clock.StarTravel;
            clock.Observe(transit); Check(clock.StarTravel == travel, "进入过场星点位移不归零");
            clock.Observe(Frame(2, JourneyPhase.ArrivalSync)); Check(clock.StarTravel == travel, "到达星点位移不归零");
            clock.Advance(8, false, false, 110); Check(clock.SurfaceAmount == 0, "未绘制地图继续显示太空");
            clock.Advance(8, false, false, 110); Check(clock.StarTravel > 1, "星点逐层循环而不重置公共位移");
            clock.Advance(.6, true, false, 110); Check(Math.Abs(clock.SurfaceAmount - .5) < .00001, "就绪后连续显露");
            clock.Observe(Frame(2, JourneyPhase.Descent)); clock.Advance(8, true, true, 0);
            Check(Math.Abs(clock.SurfaceAmount - .5) < .00001, "暂停及阶段切换不跳过显露");
            clock.Advance(.6, true, false, 0); Check(clock.Complete && clock.SurfaceAmount == 1, "显露结束回到完整地表");
            clock.Observe(null); clock.Observe(Frame(5, JourneyPhase.Descent));
            Check(clock.Complete && clock.SurfaceAmount == 1, "新连接地面基线不播放旧航程");
            var first = Frame(1, JourneyPhase.Descent, x: 100, height: 50);
            var next = Frame(1, JourneyPhase.Descent, x: 200, height: 150, tick: 6, publication: 2);
            var timeline = new PresentationTimeline(); timeline.Push(first, 0); timeline.Push(next, .1);
            foreach (double time in new[] { .1, .125, .15, .175, .2 })
            {
                Check(Math.Abs(timeline.X(next.World.Actors[0], time) - timeline.X(next.World.Buildings[0], time) - 20) < .0001,
                    "船员横向相对位置 " + time);
                Check(Math.Abs(timeline.Height(next.World.Actors[0], time) - timeline.Height(next.World.Expedition.Devices[0], time) - 10) < .0001,
                    "船员竖向相对位置 " + time);
            }
            var arrival = Frame(2, JourneyPhase.ArrivalSync, x: 568, height: 176, publication: 3);
            timeline.Push(arrival, .16);
            Check(timeline.X(arrival.World.Buildings[0], .16) == 568 && timeline.Height(arrival.World.Expedition.Devices[0], .16) == 176,
                "跨epoch不插值穿过两个世界");
            File.WriteAllText(args[0], JsonSerializer.Serialize(new { scope = "pure presentation checks; no Unity or network", total = results.Count,
                failures, results }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Ship arrival checks: " + (results.Count - failures) + "/" + results.Count + "; " + args[0]);
            return failures == 0 ? 0 : 1;
        }
        private static void Check(bool value, string name)
        { if (!value) failures++; results.Add(new { name, passed = value }); }

        private static SessionViewData Frame(int epoch, JourneyPhase phase, string journeyId = "journey", string mapId = "map",
            float x = 568, float height = 0, long tick = 0, long publication = 1)
        {
            var journey = new JourneyViewData(true, journeyId, 1, phase, "planet", "seed", mapId, "content", 0, "", Array.Empty<PlanetDefinition>());
            var device = new ExpeditionDeviceData(2, height, 0, 0, 3, 0, 0, 0, true);
            var expedition = new ExpeditionViewData(1, 0, 0, 0, false, 0, 0, 0, 0, 0, Array.Empty<ExpeditionActorData>(),
                new[] { device }, ship: new ExpeditionShipData(2, 3, 1, 0, 0, 0, 568, 0), journey: journey);
            var camp = new CampViewData(default, 1, 4, 0, 0, "Day", 60, 0, "Playing", 0, 0, default);
            var actor = new ActorViewData(1, "worker", "crew", false, x + 20, 100, "Move", 0, 1, true, 0, 0, 0, height: height + 10);
            var building = new BuildingViewData(2, "ship", x, 100, 1, 0, 0, 0, Array.Empty<TrainingViewData>());
            var world = new WorldViewData(camp, new[] { actor }, new[] { building }, Array.Empty<WorksiteViewData>(),
                Array.Empty<ProjectileViewData>(), expedition: expedition);
            return new SessionViewData(publication, epoch, 1, tick, 1, false, 1, 1, false, false, 1, 0, world);
        }
    }
}
