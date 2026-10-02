using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;

namespace DarkNights.Tests
{
    /// <summary>生成星球的真实权威角色往返验收；从选择、自动着陆、下船、下洞到回船只提交可信输入，不注入状态。</summary>
    public sealed class JourneyWalkwayTests
    {
        [UnityTest]
        public IEnumerator DefaultDockWalksFromLeftRampIntoCaveAndBack() =>
            UniTask.ToCoroutine(() => RoundTrip(false));

        [UnityTest]
        public IEnumerator HighDockJumpsAboveOldCeilingWalksIntoCaveAndBack() =>
            UniTask.ToCoroutine(() => RoundTrip(true));

        private static async UniTask RoundTrip(bool high)
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = JourneyScenario.Create(scope, config =>
            {
                // 短距离固定路线使本项聚焦原速移动，往返不依赖威胁数值的测试特判。
                config.Planets[0].Seed = "FLOW-PURE-12";
                if (high) { config.Planets[0].DockRow = 28; config.Planets[0].MaximumLift = 256; }
            });
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0);
            int originalActorId = Hero(world, 0).Id;
            await JourneyScenario.Descent(authority, world, host);
            JourneyScenario.Land(authority, world, host);
            var hero = Hero(world, 0); var ship = Ship(world); var planet = world.Flow.ActivePlanet;
            Assert.That(hero.Id, Is.EqualTo(originalActorId));
            Assert.That(ship.CaptureState().PilotId, Is.Zero, "自动着陆应释放驾驶席，测试不发送 land 或 pilot 补救命令。");
            Assert.That(ship.CaptureState().Height, Is.EqualTo(planet.DockHeight).Within(.01));
            Assert.That(planet.DockHeight, Is.EqualTo(high ? 192 : 0));
            Assert.That(hero.CaptureState().Boarded, Is.True);
            var map = world.Terrain.Capture();
            Assert.That(map.Seed, Is.EqualTo("FLOW-PURE-12"));
            Assert.That(JourneyWalkwayRoute.Find(map, planet, out float targetX, out float targetHeight), Is.True,
                "冻结地图探针必须先找到通向洞室的坡路，后续角色仍由真实 Runtime 运动执行。");
            float rampToe = ship.X + ShipGeometry.RampToe, exitX = rampToe - 12;
            TestContext.WriteLine($"dockRow={planet.DockRow}, seed={map.Seed}, exit=({exitX},{planet.DockHeight}), cave=({targetX},{targetHeight})");

            await WalkTo(authority, host, world, hero, exitX, false, false);
            Assert.That(hero.CaptureState().Boarded, Is.False, "必须从左侧坡道实际离船。");
            Assert.That(hero.CaptureState().Height, Is.EqualTo(planet.DockHeight).Within(1));
            await JumpOnPlatform(authority, host, world, hero, planet.DockHeight);

            // 正常 S 输入经过坡道脚而不进入船舱；禁止调用登船命令或直接写 ShipEntryBlocked。
            await WalkTo(authority, host, world, hero, targetX, true, true);
            var atCave = hero.CaptureState();
            Assert.That(atCave.Height, Is.EqualTo(targetHeight).Within(2.1));
            Assert.That(atCave.Height, Is.LessThanOrEqualTo(planet.DockHeight - 8 * 16 + 2.1));
            Assert.That(JourneyWalkwayRoute.InRoom(map, planet, atCave.X, atCave.Height), Is.True,
                "必须到达生成洞室的站立支撑区域，不能只站在地表或通道入口。");
            AssertBodyClear(world, hero);
            TestContext.WriteLine($"cave reached tick={authority.ServerTick}, position=({atCave.X},{atCave.Height})");

            // S 旁路会持续到离开坡道脚 24 单位范围；先按正常输入退到范围外，再右行登船。
            await WalkTo(authority, host, world, hero, rampToe - 28, true, true);
            Assert.That(hero.CaptureState().Height, Is.EqualTo(planet.DockHeight).Within(1));
            Assert.That(hero.CaptureState().ShipEntryBlocked, Is.False, "离开旁路范围后应由正式运动自动解除登船门控。");
            await WalkTo(authority, host, world, hero, ship.X + ShipGeometry.HoldX, false, false);
            var returned = hero.CaptureState();
            Assert.That(returned.Boarded, Is.True, "回船必须再次从左坡道步行进入船舱。");
            Assert.That(returned.Height, Is.EqualTo(planet.DockHeight + ShipGeometry.Floor(ShipGeometry.HoldX)).Within(1));
            Assert.That(returned.Hp, Is.GreaterThan(0));
            Assert.That(hero.Id, Is.EqualTo(originalActorId));
            Assert.That(world.Flow.Phase, Is.EqualTo(JourneyPhase.Landed));
            Assert.That(ship.X, Is.EqualTo(planet.DockX).Within(.01));
            Assert.That(ship.CaptureState().Height, Is.EqualTo(planet.DockHeight).Within(.01));
            var after = world.Terrain.Capture();
            CollectionAssert.AreEqual(map.CopyMaterials(), after.CopyMaterials(), "往返期间不能修改地形来通过验收。");
            CollectionAssert.AreEqual(map.CopyShapes(), after.CopyShapes());
            TestContext.WriteLine($"returned tick={authority.ServerTick}, position=({returned.X},{returned.Height})");
        }

        private static async UniTask JumpOnPlatform(SessionAuthority authority, SessionConnection host,
            ObjectSession world, ActorBehaviour hero, float ground)
        {
            var state = hero.CaptureState();
            Assert.That(authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, hero.Id, state.ControlLease, authority.ServerTick + 1, authority.ServerTick,
                0, true, false, true, false)), Is.True);
            authority.Tick();
            float maximum = hero.CaptureState().Height;
            Assert.That(maximum, Is.GreaterThan(ground));
            for (int i = 0; i < 180; i++)
            {
                Input(authority, host, hero);
                maximum = Math.Max(maximum, hero.CaptureState().Height);
                if (i % 30 == 0) await UniTask.Yield();
            }
            Assert.That(maximum, Is.GreaterThan(ground + 1));
            Assert.That(maximum, Is.LessThanOrEqualTo(Math.Max(world.Catalog.Balance.HeroControl.MaximumHeight,
                ground + world.Catalog.Balance.HeroControl.MaximumHeight) + .01));
            Assert.That(hero.CaptureState().Height, Is.EqualTo(ground).Within(1));
            Assert.That(hero.CaptureState().Boarded, Is.False);
        }

        private static async UniTask WalkTo(SessionAuthority authority, SessionConnection host, ObjectSession world,
            ActorBehaviour hero, float targetX, bool bypassEntry, bool requireOutside)
        {
            float toe = Ship(world).X + ShipGeometry.RampToe;
            float speed = world.Catalog.Balance.HeroControl.WalkSpeed;
            float tolerance = speed / 60f * .51f + .01f;
            int maximumTicks = (int)Math.Ceiling(Math.Abs(targetX - hero.X) / speed * 60) + 180;
            int stalled = 0;
            for (int i = 0; i < maximumTicks && Math.Abs(hero.X - targetX) > tolerance; i++)
            {
                var previous = hero.CaptureState();
                Input(authority, host, hero, Math.Sign(targetX - previous.X),
                    down: bypassEntry && Math.Abs(previous.X - toe) <= 24);
                var current = hero.CaptureState();
                Assert.That(current.Hp, Is.GreaterThan(0), "角色必须存活完成往返，不能用死亡归船代替。");
                Assert.That(Math.Abs(current.X - previous.X), Is.LessThanOrEqualTo(speed / 60f + .01f),
                    "实际行走不能发生瞬移。");
                stalled = Math.Abs(current.X - previous.X) < .001f ? stalled + 1 : 0;
                Assert.That(stalled, Is.LessThan(90), $"真实运动受阻：({current.X},{current.Height}) -> {targetX}");
                if (requireOutside) Assert.That(current.Boarded, Is.False);
                if (i % 30 != 0) continue;
                AssertBodyClear(world, hero);
                await UniTask.Yield();
            }
            Input(authority, host, hero);
            Assert.That(hero.X, Is.EqualTo(targetX).Within(tolerance), "权威角色未能走到路径目标。");
        }

        private static void AssertBodyClear(ObjectSession world, ActorBehaviour hero)
        {
            var state = hero.CaptureState();
            foreach (float side in new[] { -HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHalfWidth })
                for (float head = 1; head <= HeroControlDefinition.BodyHeight; head += 7)
                    Assert.That(TerrainHeroMotion.Solid(world.Terrain.Map, state.X + side, state.Height + head), Is.False,
                        $"角色实体穿入权威坡形：({state.X},{state.Height})。");
        }
    }
}
