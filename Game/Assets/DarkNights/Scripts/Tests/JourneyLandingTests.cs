using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;
using static DarkNights.Tests.JourneyScenario;

namespace DarkNights.Tests
{
    /// <summary>真实权威输入的缓降、自动着陆与开舱回归；验证驾驶租约释放和保存中的舱门计时，不注入船体运动状态。</summary>
    public sealed class JourneyLandingTests
    {
        [Test]
        public void IdleDescentRuleRejectsNonFiniteAndUnsafeValues()
        {
            Assert.That(new ShipFlightDefinition().IdleDescentSpeed, Is.EqualTo(6));
            foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, 9f })
                Assert.Throws<ArgumentOutOfRangeException>(() => new ShipFlightDefinition(idleDescentSpeed: value));
        }

        [UnityTest]
        public IEnumerator IdleInputDescendsAndFastGroundContactCannotFakeSafeLanding() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            await Descent(authority, world, host); var hero = Hero(world, 0); var ship = Ship(world);
            float height = ship.CaptureState().Height;
            for (int i = 0; i < 60; i++) Input(authority, host, hero);
            Assert.That(ship.CaptureState().Height, Is.LessThan(height - 5));
            Assert.That(ship.CaptureState().ShipVelocityY, Is.EqualTo(-world.Catalog.Balance.Expedition.Ship.IdleDescentSpeed));
            Assert.That(Send(authority, host, 2, "land", hero), Is.EqualTo(SessionResultCode.NoEffect));
            for (int i = 0; i < 400; i++) Input(authority, host, hero, down: true);
            Assert.That(ship.CaptureState().Height - ship.CaptureState().DockHeight, Is.LessThan(1));
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent), "持续加速下降不能利用碰撞清零冒充安全速度。");
            int oldLease = hero.CaptureState().ControlLease;
            Land(authority, world, host, false);
            Assert.That(hero.CaptureState().ControlLease, Is.GreaterThan(oldLease));
            Assert.That(hero.CaptureState().DropPending, Is.False); Assert.That(hero.CaptureState().Horizontal, Is.Zero);
            Assert.That(authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, hero.Id, oldLease, authority.ServerTick + 1, authority.ServerTick,
                1, true, false, false, false)), Is.False);
        });

        [UnityTest]
        public IEnumerator AutomaticLandingOpensAfterConfiguredDelayAndBothPlayersCanWalkOut() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0); var guest = Connect(authority, 1);
            await Descent(authority, world, host, guest);
            var pilot = Hero(world, 0); var passenger = Hero(world, 1); var ship = Ship(world);
            Walk(authority, guest, passenger, ship.X + ShipGeometry.RampHinge + 8);
            Land(authority, world, host, false);
            Assert.That(ship.CaptureState().ShipDoorClock, Is.EqualTo(world.Catalog.Balance.Expedition.Ship.DoorSeconds));
            Assert.That(ship.CaptureState().PilotId, Is.Zero);
            int revision = world.Journey.Capture().Revision;
            world.SetTime(true, 1); double clock = ship.CaptureState().ShipDoorClock;
            for (int i = 0; i < 20; i++) authority.Tick();
            Assert.That(ship.CaptureState().ShipDoorClock, Is.EqualTo(clock)); world.SetTime(false, 1);
            for (int i = 0; i < 12; i++) Input(authority, guest, passenger, -1);
            Assert.That(passenger.CaptureState().Boarded, Is.True);
            Assert.That(passenger.X - ship.X, Is.EqualTo(ShipGeometry.RampHinge + 8).Within(.01));
            for (int i = 0; i < 62 && ship.CaptureState().ShipDoorClock > 0; i++) authority.Tick();
            Assert.That(ship.CaptureState().ShipDoorClock, Is.Zero);
            Walk(authority, guest, passenger, ship.X + ShipGeometry.RampToe - 12);
            Walk(authority, host, pilot, ship.X + ShipGeometry.RampToe - 20);
            Assert.That(passenger.CaptureState().Boarded, Is.False); Assert.That(pilot.CaptureState().Boarded, Is.False);
            Assert.That(world.Journey.Capture().Revision, Is.EqualTo(revision));
            Assert.That(world.CaptureView().Expedition.Run, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator OpeningDoorSurvivesProjectionAndSaveRestoreWithoutReleasingEarly() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            await Descent(authority, world, host); Land(authority, world, host, false);
            for (int i = 0; i < 15; i++) authority.Tick();
            double remaining = Ship(world).CaptureState().ShipDoorClock;
            Assert.That(remaining, Is.GreaterThan(0));
            var codec = new ProjectionCodec(world.Catalog, world.Layout);
            Assert.That(codec.Decode(codec.Encode(authority.CaptureProjection())).World.Expedition.Ship.DoorClock, Is.EqualTo(remaining));
            string json = world.SaveCodec.Serialize(world.CaptureWorld()); world.Restore(json);
            Assert.That(Ship(world).CaptureState().ShipDoorClock, Is.EqualTo(remaining));
            Assert.That(Ship(world).CaptureState().PilotId, Is.Zero);
            for (int i = 0; i < 10; i++) world.Advance(1.0 / 60);
            Assert.That(Ship(world).CaptureState().ShipDoorClock, Is.GreaterThan(0));
            for (int i = 0; i < 60; i++) world.Advance(1.0 / 60);
            Assert.That(Ship(world).CaptureState().ShipDoorClock, Is.Zero);
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Landed));
        });

        [UnityTest]
        public IEnumerator MissingSupportAndBlockedHullOrRampRejectLandingUntilRepaired() => UniTask.ToCoroutine(async () =>
        {
            foreach (string obstruction in new[] { "left support", "right support", "ramp exit", "hull", "ramp head" })
            {
                using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
                using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
                await Descent(authority, world, host);
                var pilot = Hero(world, 0); var ship = Ship(world);
                for (int i = 0; i < 40; i++) Input(authority, host, pilot, 1);
                for (int i = 0; i < 40; i++) Input(authority, host, pilot);
                Assert.That(Math.Abs(ship.X - ship.CaptureState().DockX),
                    Is.GreaterThan(world.Catalog.Balance.Expedition.Ship.LandingTolerance));
                var state = ship.CaptureState();
                var baseline = world.Terrain.Capture();
                float x = state.X, height = state.DockHeight;
                switch (obstruction)
                {
                    case "left support": ReplaceFixtureCell(world, baseline, x - 56, height - 1, 0); break;
                    case "right support": ReplaceFixtureCell(world, baseline, x + 104, height - 1, 0); break;
                    case "ramp exit": ReplaceFixtureCell(world, baseline, x + ShipGeometry.RampToe - 8, height - 1, 0); break;
                    case "hull": ReplaceFixtureCell(world, baseline, x + 104, height + 100, 1); break;
                    default: ReplaceFixtureCell(world, baseline, x + ShipGeometry.RampToe, height + 16, 1); break;
                }
                for (int i = 0; i < 400; i++) Input(authority, host, pilot, down: true);
                for (int i = 0; i < 160; i++) Input(authority, host, pilot);
                Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent), obstruction);
                Assert.That(ship.CaptureState().PilotId, Is.EqualTo(pilot.Id), obstruction);
                Assert.That(ship.CaptureState().ShipDoorClock, Is.Zero, obstruction);
                world.Terrain.Replace(world.Terrain.Prepare(baseline), baseline);
                for (int i = 0; i < 1200 && world.Flow.Phase != JourneyPhase.Landed; i++) Input(authority, host, pilot);
                Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Landed), obstruction + " repair");
            }
        });

        [UnityTest]
        public IEnumerator LateralDescentLandsAtCurrentSafePositionAndRestores() => UniTask.ToCoroutine(async () =>
        {
            foreach (int direction in new[] { -1, 1 })
            {
                using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
                using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
                await Descent(authority, world, host);
                var pilot = Hero(world, 0); var ship = Ship(world);
                for (int i = 0; i < 40; i++) Input(authority, host, pilot, direction);
                Assert.That(Math.Abs(ship.X - ship.CaptureState().DockX),
                    Is.GreaterThan(world.Catalog.Balance.Expedition.Ship.LandingTolerance));
                for (int i = 0; i < 400; i++) Input(authority, host, pilot, down: true);
                Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Descent), "持续快降不能利用碰撞后的零速度直接着陆。");
                float stoppedX = ship.X;
                for (int i = 0; i < 1200 && world.Flow.Phase != JourneyPhase.Landed; i++) Input(authority, host, pilot);
                Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Landed));
                Assert.That(ship.X, Is.EqualTo(stoppedX).Within(.01f), "着陆必须保持当前横向落点。");
                Assert.That(Math.Abs(ship.X - ship.CaptureState().DockX),
                    Is.GreaterThan(world.Catalog.Balance.Expedition.Ship.LandingTolerance));
                Assert.That(ship.CaptureState().PilotId, Is.Zero);
                string save = world.SaveCodec.Serialize(world.CaptureWorld());
                world.Restore(save);
                Assert.That(Ship(world).X, Is.EqualTo(stoppedX).Within(.01f), "星球上的原地落点应可保存和恢复。");
            }
        });

        [UnityTest]
        public IEnumerator MaximumSpeedAvoidsSlopeAndConfiguredFlightEnvelope() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create(); var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world); var host = Connect(authority, 0);
            await Descent(authority, world, host);
            var pilot = Hero(world, 0); var ship = Ship(world); var start = ship.CaptureState();
            var baseline = world.Terrain.Capture();
            ReplaceFixtureCell(world, baseline, start.X + ShipGeometry.HalfWidth + 100, start.Height + 80, 1, 1);
            float peakSpeed = 0;
            for (int i = 0; i < 180; i++)
            {
                Input(authority, host, pilot, 1);
                var state = ship.CaptureState(); peakSpeed = Math.Max(peakSpeed, state.ShipVelocityX);
                Assert.That(Math.Abs(state.X - state.DockX), Is.LessThanOrEqualTo(world.Flow.ActivePlanet.HorizontalRange + .001f));
                AssertHullDoesNotOverlap(world, state.X, state.Height);
            }
            Assert.That(peakSpeed, Is.GreaterThan(world.Catalog.Balance.Expedition.Ship.HorizontalSpeed * .9f));
            Assert.That(ship.CaptureState().X - start.X, Is.LessThan(150), "船壳必须在坡形障碍前停止。");
            world.Terrain.Replace(world.Terrain.Prepare(baseline), baseline);
            for (int i = 0; i < 240; i++) Input(authority, host, pilot, 1);
            Assert.That(ship.CaptureState().X - ship.CaptureState().DockX,
                Is.EqualTo(world.Flow.ActivePlanet.HorizontalRange).Within(.01f));
            for (int i = 0; i < 500; i++) Input(authority, host, pilot, up: true);
            Assert.That(ship.CaptureState().Height - ship.CaptureState().DockHeight,
                Is.EqualTo(world.Flow.ActivePlanet.MaximumLift).Within(.01f));
            AssertHullDoesNotOverlap(world, ship.CaptureState().X, ship.CaptureState().Height);
        });

        private static void AssertHullDoesNotOverlap(ObjectSession world, float x, float height)
        {
            for (float px = -ShipGeometry.HalfWidth; px <= ShipGeometry.HalfWidth; px += 4)
                for (float py = 1; py <= ShipGeometry.Roof; py += 4)
                    if (ShipGeometry.Hull(px, py))
                        Assert.That(TerrainHeroMotion.Solid(world.Terrain.Map, x + px, height + py), Is.False,
                            $"船壳与真实坡形重叠：({px}, {py})");
        }

        private static void ReplaceFixtureCell(ObjectSession world, PlayableTerrain baseline, float x, float height,
            byte material, byte shape = 0)
        {
            var cells = baseline.CopyMaterials(); var protectedCells = baseline.CopyProtection();
            var softRock = baseline.CopySoftRock(); var shapes = baseline.CopyShapes();
            int column = (int)Math.Floor(x / PlayableTerrain.CellPixels + .5f);
            int row = (int)Math.Floor((PlayableTerrain.OriginY - height) / PlayableTerrain.CellPixels + .5f);
            int index = row * TerrainGenerationSettings.Width + column;
            Assert.That(column, Is.InRange(1, TerrainGenerationSettings.Width - 2));
            Assert.That(row, Is.InRange(1, TerrainGenerationSettings.Height - 2));
            cells[index] = material; protectedCells[index] = false; softRock[index] = false; shapes[index] = shape;
            var changed = new PlayableTerrain(baseline.WorldId, baseline.Seed, cells, protectedCells, softRock,
                baseline.Rooms.ToArray(), baseline.Deposits.ToArray(), shapes, baseline.Expedition, baseline.Background);
            world.Terrain.Replace(world.Terrain.Prepare(changed), changed);
        }
    }
}
