using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;

namespace DarkNights.Tests
{
    /// <summary>可步入飞船的权威切片验收；覆盖真实通路、双人席位、设备归队、输入失效和空中保存恢复。</summary>
    public sealed class ShipTests
    {
        [UnityTest]
        public IEnumerator WalkIntoCockpitAndBackToTerrainWithoutTeleport() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            var hero = Hero(world, 0); float x = Ship(world).X;
            Assert.That(hero.CaptureState().Boarded, Is.False);
            Walk(authority, host, hero, x - 40);
            Assert.That(hero.CaptureState().Boarded, Is.True); Assert.That(hero.CaptureState().Height, Is.EqualTo(40));
            Walk(authority, host, hero, x + ShipGeometry.PilotX);
            Assert.That(hero.CaptureState().Height, Is.EqualTo(80));
            Walk(authority, host, hero, x - 180);
            Assert.That(hero.CaptureState().Boarded, Is.False); Assert.That(hero.CaptureState().Height, Is.EqualTo(0).Within(1));
        });

        [UnityTest]
        public IEnumerator DownThroughRampKeepsTheGroundRouteUsable() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var hero = Hero(world, 0);
            float destination = Ship(world).X + 184;
            for (int i = 0; i < 1600 && hero.X < destination; i++) Input(authority, host, hero, 1, down: true);
            Assert.That(hero.X, Is.GreaterThanOrEqualTo(destination)); Assert.That(hero.CaptureState().Boarded, Is.False);
        });

        [UnityTest]
        public IEnumerator SeatHasOneOwnerAndRejectsForgedOrOldLease() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            var first = Hero(world, 0); var second = Hero(world, 1); var ship = Ship(world);
            Assert.That(Send(authority, host, 1, "pilot", first), Is.EqualTo(SessionResultCode.NoEffect));
            Walk(authority, host, first, ship.X + 96); Walk(authority, guest, second, ship.X + 96);
            int previousLease = first.CaptureState().ControlLease;
            Assert.That(Send(authority, host, 2, "pilot", first), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(Send(authority, guest, 1, "pilot", second), Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(Send(authority, guest, 2, "takeoff", first), Is.EqualTo(SessionResultCode.PermissionDenied));
            Assert.That(Send(authority, host, 3, "takeoff", first, previousLease), Is.EqualTo(SessionResultCode.PermissionDenied));
            Assert.That(ship.CaptureState().PilotId, Is.EqualTo(first.Id));
        });

        [UnityTest]
        public IEnumerator FlightWaitsForPassengerAndCarriesThemInTheSameStep() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            var pilot = Hero(world, 0); var passenger = Hero(world, 1); var ship = Ship(world);
            Walk(authority, host, pilot, ship.X + 96); Send(authority, host, 1, "pilot", pilot);
            Assert.That(Send(authority, host, 2, "takeoff", pilot), Is.EqualTo(SessionResultCode.Applied));
            for (int i = 0; i < 90; i++) authority.Tick();
            Assert.That(ship.CaptureState().ShipPhase, Is.EqualTo(1));
            Walk(authority, guest, passenger, ship.X - 40);
            for (int i = 0; i < 90; i++) authority.Tick();
            Assert.That(ship.CaptureState().ShipPhase, Is.EqualTo(3));
            for (int i = 0; i < 100; i++) Input(authority, host, pilot, up: true);
            Assert.That(ship.CaptureState().Height, Is.GreaterThan(20));
            Assert.That(passenger.CaptureState().Height - ship.CaptureState().Height, Is.EqualTo(40).Within(.01));
            Assert.That(Send(authority, host, 3, "pilot", pilot), Is.EqualTo(SessionResultCode.NoEffect), "空中不可离座打开舱门。");
            var codec = new ProjectionCodec(world.Catalog, world.Layout);
            var projection = codec.Decode(codec.Encode(authority.CaptureProjection()));
            Assert.That(projection.World.Expedition.Ship.PilotId, Is.EqualTo(pilot.Id));
            Assert.That(projection.World.Expedition.Ship.Phase, Is.EqualTo(3));
        });

        [UnityTest]
        public IEnumerator ExpiredInputDisconnectAndAirborneLoadDoNotRetainThrust() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            var pilot = Hero(world, 0); var ship = Ship(world);
            Walk(authority, host, pilot, ship.X + 96); Send(authority, host, 1, "pilot", pilot); Send(authority, host, 2, "takeoff", pilot);
            for (int i = 0; i < 90; i++) authority.Tick();
            for (int i = 0; i < 90; i++) Input(authority, host, pilot, up: true);
            for (int i = 0; i < 90; i++) authority.Tick();
            Assert.That(ship.CaptureState().ShipVelocityY, Is.Zero);
            float height = ship.CaptureState().Height; string save = world.SaveCodec.Serialize(world.CaptureWorld());
            authority.Disconnect(host, closeHostedSession: false); authority.Tick(); Assert.That(ship.CaptureState().PilotId, Is.Zero);
            world.Restore(save); ship = Ship(world); host = Connect(authority, 0); pilot = Hero(world, 0);
            Assert.That(ship.CaptureState().Height, Is.EqualTo(height)); Assert.That(ship.CaptureState().PilotId, Is.Zero);
            Assert.That(pilot.CaptureState().Height, Is.EqualTo(height + 80).Within(.01));
            Assert.That(Send(authority, host, 3, "pilot", pilot), Is.EqualTo(SessionResultCode.Applied));
            for (int i = 0; i < 600 && ship.CaptureState().Height > 1; i++) Input(authority, host, pilot, down: true);
            for (int i = 0; i < 35; i++) Input(authority, host, pilot);
            Assert.That(Send(authority, host, 4, "land", pilot), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(ship.CaptureState().Height, Is.EqualTo(0)); Assert.That(ship.CaptureState().ShipPhase, Is.Zero);
        });

        [UnityTest]
        public IEnumerator WorkingRobotAndScoutReturnBeforeDoorsClose() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            // 只注入已购买舱段；部署、运动、携物和归队仍走正式事务与 AI。
            var seed = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld())); seed["world"]["expedition"]["RobotModule"] = 1;
            world.Restore(seed.ToString()); host = Connect(authority, 0); var pilot = Hero(world, 0); var ship = Ship(world);
            Send(authority, host, 1, "depart");
            Walk(authority, host, pilot, ship.X + 96); Send(authority, host, 2, "pilot", pilot);
            for (int i = 0; i < 1000; i++) authority.Tick();
            var robot = world.Index.Actors.Single(a => a.RuleKey == "hauler"); var drone = world.Index.Actors.Single(a => a.RuleKey == "scout-drone");
            Assert.That(drone.CaptureState().Boarded, Is.False); Assert.That(drone.CaptureState().TaskPhase, Is.EqualTo(2));
            Assert.That(world.CaptureView().Expedition.Devices.Any(d => d.Id != ship.Id && d.Stage is 2 or 3), Is.True);
            Send(authority, host, 3, "takeoff", pilot);
            Assert.That(ship.CaptureState().ShipPhase, Is.EqualTo(1));
            for (int i = 0; i < 15000 && ship.CaptureState().ShipPhase != 3; i++) authority.Tick();
            Assert.That(ship.CaptureState().ShipPhase, Is.EqualTo(3), "受阻须停在收舱，不可超时瞬移；此泊位应能实际归队。");
            Assert.That(robot.CaptureState().Boarded && drone.CaptureState().Boarded, Is.True);
            Assert.That(world.CaptureView().Expedition.Devices.Where(d => d.Id != ship.Id).All(d => d.Stage == 6), Is.True);
        });

        [UnityTest]
        public IEnumerator ThrustCannotCrossTerrainOrTheTrialEnvelope() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            var pilot = Hero(world, 0); var ship = Ship(world);
            Walk(authority, host, pilot, ship.X + 96); Send(authority, host, 1, "pilot", pilot); Send(authority, host, 2, "takeoff", pilot);
            for (int i = 0; i < 90; i++) authority.Tick();
            for (int i = 0; i < 600; i++) Input(authority, host, pilot, up: true);
            Assert.That(ship.CaptureState().Height, Is.InRange(1, 192));
            for (int i = 0; i < 600; i++) Input(authority, host, pilot, 1);
            var end = ship.CaptureState();
            Assert.That(end.X, Is.GreaterThan(end.DockX)); Assert.That(end.X - end.DockX, Is.LessThanOrEqualTo(128));
            for (int i = 0; i < 90; i++) Input(authority, host, pilot, 1);
            Assert.That(ship.X, Is.EqualTo(end.X).Within(.01), "持续推力不能越过岩壁或边界。");
        });

        [UnityTest]
        public IEnumerator InvalidShipSaveDoesNotReplaceTheActiveWorld() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = Create(scope);
            using var authority = new SessionAuthority(world); Connect(authority, 0); var map = world.Terrain.Map;
            var saved = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            saved["world"]["expedition"]["Ship"]["PilotId"] = Hero(world, 0).Id;
            Assert.Throws<FormatException>(() => world.Restore(saved.ToString())); Assert.That(world.Terrain.Map, Is.SameAs(map));
            saved = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            saved["world"]["expedition"]["Ship"]["DockX"] = 999;
            Assert.Throws<FormatException>(() => world.Restore(saved.ToString())); Assert.That(world.Terrain.Map, Is.SameAs(map));
        });
    }
}
