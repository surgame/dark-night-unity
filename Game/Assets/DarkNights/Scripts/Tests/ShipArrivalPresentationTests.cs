using System;
using System.Reflection;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;
using DarkNights.View;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>到达连续性和船载插值的定向回归；验证冻结身份、显露屏障与屏幕锚点，不替代真实航程画面或跨进程验收。</summary>
    public sealed class ShipArrivalPresentationTests
    {
        [Test]
        public void ArrivalRequiresSameJourneyShipMapAndNextEpoch()
        {
            var transit = Frame(1, JourneyPhase.Transit);
            Assert.That(JourneyContinuity.IsArrival(transit, Frame(2, JourneyPhase.ArrivalSync)), Is.True);
            Assert.That(JourneyContinuity.IsArrival(transit, Frame(2, JourneyPhase.Descent)), Is.True);
            Assert.That(JourneyContinuity.IsArrival(transit, Frame(1, JourneyPhase.ArrivalSync)), Is.False);
            Assert.That(JourneyContinuity.IsArrival(transit, Frame(3, JourneyPhase.ArrivalSync)), Is.False);
            Assert.That(JourneyContinuity.IsArrival(transit, Frame(2, JourneyPhase.ArrivalSync, journeyId: "other")), Is.False);
            Assert.That(JourneyContinuity.IsArrival(transit, Frame(2, JourneyPhase.ArrivalSync, mapId: "other")), Is.False);
            Assert.That(JourneyContinuity.IsArrival(Frame(1, JourneyPhase.Landed), Frame(2, JourneyPhase.Landed)), Is.False);
        }

        [Test]
        public void ArrivalStaysInSpaceUntilDestinationDrawsAndPauseStopsReveal()
        {
            var clock = new JourneyArrivalPresentation();
            clock.Observe(Frame(1, JourneyPhase.Transit));
            clock.Observe(Frame(2, JourneyPhase.ArrivalSync));
            clock.Advance(8, false, false, 110);
            Assert.That(clock.SurfaceAmount, Is.Zero);
            clock.Advance(.6, true, false, 110);
            Assert.That(clock.SurfaceAmount, Is.EqualTo(.5).Within(.00001));
            clock.Observe(Frame(2, JourneyPhase.Descent));
            clock.Advance(8, true, true, 110);
            Assert.That(clock.SurfaceAmount, Is.EqualTo(.5).Within(.00001));
            clock.Advance(.6, true, false, 0);
            Assert.That(clock.SurfaceAmount, Is.EqualTo(1));
            Assert.That(clock.Complete, Is.True);
        }

        [Test]
        public void StarTravelSurvivesPhaseChangesAndFreshGroundLoadDoesNotReplayArrival()
        {
            var clock = new JourneyArrivalPresentation();
            clock.Observe(Frame(1, JourneyPhase.Preparing)); clock.Advance(4, false, false, 1.5);
            double before = clock.StarTravel;
            clock.Observe(Frame(1, JourneyPhase.Transit));
            Assert.That(clock.StarTravel, Is.EqualTo(before));
            clock.Observe(Frame(2, JourneyPhase.ArrivalSync));
            Assert.That(clock.StarTravel, Is.EqualTo(before));
            clock.Advance(20, false, false, 110);
            Assert.That(clock.StarTravel, Is.GreaterThan(1), "不同视差层分别循环，不把公共位移归零。");
            clock.Observe(null); clock.Observe(Frame(5, JourneyPhase.Descent));
            Assert.That(clock.Complete, Is.True);
            Assert.That(clock.SurfaceAmount, Is.EqualTo(1));
        }

        [Test]
        public void CrewAndShipKeepTheirRelativePositionAtTheSameInterpolationTime()
        {
            var first = Frame(1, JourneyPhase.Descent, x: 100, height: 50, tick: 0, publication: 1);
            var next = Frame(1, JourneyPhase.Descent, x: 200, height: 150, tick: 6, publication: 2);
            var timeline = new PresentationTimeline(); timeline.Push(first, 0); timeline.Push(next, .1);
            Assert.That(timeline.X(next.World.Buildings[0], .15), Is.EqualTo(150).Within(.0001));
            Assert.That(timeline.Height(next.World.Expedition.Devices[0], .15), Is.EqualTo(100).Within(.0001));
            Assert.That(timeline.X(next.World.Actors[0], .15) - timeline.X(next.World.Buildings[0], .15), Is.EqualTo(20).Within(.0001));
            Assert.That(timeline.Height(next.World.Actors[0], .15) - timeline.Height(next.World.Expedition.Devices[0], .15), Is.EqualTo(10).Within(.0001));
            var arrival = Frame(2, JourneyPhase.ArrivalSync, x: 568, height: 176, publication: 3);
            timeline.Push(arrival, .16);
            Assert.That(timeline.X(arrival.World.Buildings[0], .16), Is.EqualTo(568));
            Assert.That(timeline.Height(arrival.World.Expedition.Devices[0], .16), Is.EqualTo(176));
        }

        [Test]
        public void CameraRebasePreservesViewportPoseZoomAndFollowVelocity()
        {
            var root = new GameObject("Ship camera continuity fixture");
            try
            {
                var camera = root.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 3;
                camera.transform.position = new Vector3(4, 2, -10);
                var stage = root.AddComponent<PinewatchStage>();
                Set(stage, "sceneCamera", camera); Set(stage, "cameraInitialized", true);
                Set(stage, "cameraX", 400f); Set(stage, "cameraHeight", 200f);
                Set(stage, "cameraXVelocity", 19f); Set(stage, "cameraHeightVelocity", -7f);
                stage.FocusShip(new Vector3(5, 1, 0));
                var oldPose = camera.WorldToViewportPoint(new Vector3(5, 1, 0));
                var delta = new Vector3(2, 1.76f, 0);
                stage.RebaseShipCamera(delta);
                Assert.That(Vector3.Distance(oldPose, camera.WorldToViewportPoint(new Vector3(5, 1, 0) + delta)), Is.LessThan(.00001));
                Assert.That(camera.orthographicSize, Is.EqualTo(3));
                Assert.That(Get(stage, "cameraXVelocity"), Is.EqualTo(19));
                Assert.That(Get(stage, "cameraHeightVelocity"), Is.EqualTo(-7));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Set(PinewatchStage stage, string name, object value) =>
            typeof(PinewatchStage).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stage, value);
        private static float Get(PinewatchStage stage, string name) =>
            (float)typeof(PinewatchStage).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);

        private static SessionViewData Frame(int epoch, JourneyPhase phase, string journeyId = "journey", string mapId = "map",
            float x = 568, float height = 0, long tick = 0, long publication = 1)
        {
            var journey = new JourneyViewData(true, journeyId, 1, phase, "planet", "seed", mapId, "content", 0, "", Array.Empty<PlanetDefinition>());
            var ship = new ExpeditionShipData(2, 3, 1, 0, 0, 0, 568, 0);
            var device = new ExpeditionDeviceData(2, height, 0, 0, 3, 0, 0, 0, true);
            var expedition = new ExpeditionViewData(1, 0, 0, 0, false, 0, 0, 0, 0, 0,
                Array.Empty<ExpeditionActorData>(), new[] { device }, ship: ship, journey: journey);
            var camp = new CampViewData(default, 1, 4, 0, 0, "Day", 60, 0, "Playing", 0, 0, default);
            var actor = new ActorViewData(1, "worker", "crew", false, x + 20, 100, "Move", 0, 1, true, 0, 0, 0, height: height + 10);
            var building = new BuildingViewData(2, "ship", x, 100, 1, 0, 0, 0, Array.Empty<TrainingViewData>());
            var world = new WorldViewData(camp, new[] { actor }, new[] { building }, Array.Empty<WorksiteViewData>(),
                Array.Empty<ProjectileViewData>(), expedition: expedition);
            return new SessionViewData(publication, epoch, 1, tick, 1, false, 1, 1, false, false, 1, 0, world);
        }
    }
}
