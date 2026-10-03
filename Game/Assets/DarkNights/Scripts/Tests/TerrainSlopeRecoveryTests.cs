using System;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 回归上坡落地后的脚底浅层卡入、双向坡面通行和低顶碰撞；直接执行正式运动函数。
    /// 合成只读格子隔离运动边界，角色仍使用真实 ActorState；不替代正式场景、联机或画面验收。
    /// </summary>
    public sealed class TerrainSlopeRecoveryTests
    {
        private const double Delta = 1.0 / 60;
        private static readonly HeroControlDefinition Rules = new HeroControlDefinition(
            160, 320, 1000, 70, 2, 2, .3, 16, 11.0 / 7, 112);
        // 用只读探针检查完整身体净空，不为测试扩大 Runtime 内部碰撞类型的公开边界。
        private static readonly Func<IReadOnlyGrid, float, float, float, float, bool> BodyBlocked =
            (Func<IReadOnlyGrid, float, float, float, float, bool>)typeof(TerrainHeroMotion).Assembly
                .GetType("DarkNights.Runtime.Terrain.TerrainBodyCollision", true).GetMethod("Blocked")
                .CreateDelegate(typeof(Func<IReadOnlyGrid, float, float, float, float, bool>));

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public void UphillLandingPhasesRemainClearAndCanJumpAgain(bool steep, bool sprint, bool mirror)
        {
            var map = Ramp(steep, mirror);
            int direction = mirror ? -1 : 1;
            for (int offset = 0; offset < 32; offset++) for (int jumpTick = -1; jumpTick < 61; jumpTick++)
            {
                float x = (sprint ? 1380 : 1480) + offset;
                var actor = Actor(mirror ? 5104 - x : x, -952);
                for (int tick = 0; tick < 90; tick++) Step(map, actor, 0);
                bool blocked = false;
                for (int tick = 0; tick < 120; tick++)
                {
                    Step(map, actor, direction, tick == jumpTick, sprint);
                    blocked |= Blocked(map, actor);
                }
                for (int tick = 0; tick < 120; tick++) Step(map, actor, 0);
                string detail = $"offset={offset}, jumpTick={jumpTick}, x={actor.X}, h={actor.Height}";
                Assert.That(blocked, Is.False, detail);
                Assert.That(actor.SupportPlatform, Is.Zero, detail);
                float previous = actor.Height;
                Step(map, actor, 0, true);
                Assert.That(actor.VerticalSpeed, Is.GreaterThan(0), detail);
                Assert.That(actor.Height, Is.GreaterThan(previous), detail);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ShallowOverlapRecoversBeforeJumpWithoutMovingDownhill(bool mirror, bool jumpImmediately)
        {
            var map = Ramp(false, mirror);
            float x = mirror ? 5104 - 1590.1353f : 1590.1353f;
            var actor = Actor(x, -957.86664f);
            TerrainHeroMotion.Tick(map, actor, Rules, Delta, jumpImmediately, false);
            Assert.That(Blocked(map, actor), Is.False);
            Assert.That(actor.X, Is.EqualTo(x));
            if (!jumpImmediately)
            {
                Assert.That(actor.SupportPlatform, Is.Zero);
                TerrainHeroMotion.Tick(map, actor, Rules, Delta, true, false);
            }
            Assert.That(actor.VerticalSpeed, Is.GreaterThan(0));
            Assert.That(actor.Height, Is.GreaterThan(-956.934f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WalkingUpAndDownKeepsSupport(bool sprint)
        {
            var map = Ramp(false, false);
            var actor = Actor(1568, -960);
            for (int tick = 0; tick < 50; tick++)
            {
                Step(map, actor, 1, false, sprint);
                Assert.That(actor.SupportPlatform, Is.Zero);
                Assert.That(Blocked(map, actor), Is.False);
            }
            Assert.That(actor.Height, Is.EqualTo(-944).Within(.002));
            for (int tick = 0; tick < 50; tick++)
            {
                Step(map, actor, -1, false, sprint);
                Assert.That(actor.SupportPlatform, Is.Zero);
                Assert.That(Blocked(map, actor), Is.False);
            }
            Assert.That(actor.Height, Is.EqualTo(-960).Within(.002));
        }

        [Test]
        public void CeilingCollisionFallsBackToGroundAndDoesNotGrantAirJump()
        {
            var map = Ramp(false, false);
            for (int x = 80; x < 99; x++) map.Set(x, 96);
            var actor = Actor(1480, -960);
            Step(map, actor, 0, true);
            bool hitCeiling = false;
            for (int tick = 0; tick < 120; tick++)
            {
                Step(map, actor, 0, actor.SupportPlatform < 0);
                hitCeiling |= actor.SupportPlatform < 0 && actor.VerticalSpeed == 0;
                Assert.That(Blocked(map, actor), Is.False);
            }
            Assert.That(hitCeiling, Is.True);
            Assert.That(actor.SupportPlatform, Is.Zero);
            Assert.That(actor.Height, Is.EqualTo(-960).Within(.002));
        }

        [Test]
        public void UphillLowCeilingStopsWithoutPushingBodyIntoRock()
        {
            var map = Ramp(false, false);
            for (int x = 95; x < 130; x++) map.Set(x, 96);
            var actor = Actor(1568, -960);
            for (int tick = 0; tick < 120; tick++)
            {
                Step(map, actor, 1, false, true);
                Assert.That(Blocked(map, actor), Is.False);
                Assert.That(actor.SupportPlatform, Is.Zero);
            }
            Assert.That(actor.X, Is.LessThan(1600));
            Assert.That(actor.Height, Is.LessThanOrEqualTo(-956 + .002));
        }

        [Test]
        public void RemovingFloorClearsSupportAndAllowsFalling()
        {
            var map = Ramp(false, false);
            var actor = Actor(1480, -960);
            Step(map, actor, 0);
            Assert.That(actor.SupportPlatform, Is.Zero);
            for (int x = 90; x <= 95; x++) for (int row = 100; row < 105; row++) map.Clear(x, row);
            Step(map, actor, 0, true);
            Assert.That(actor.SupportPlatform, Is.EqualTo(-1));
            Assert.That(actor.VerticalSpeed, Is.LessThan(0));
            Assert.That(actor.Height, Is.LessThan(-960));
        }

        private static ActorState Actor(float x, float height)
        {
            var actor = new ActorState();
            // 仅初始化未接入会话的测试状态，不为测试放宽权威状态的 internal setter。
            typeof(ActorState).GetProperty(nameof(ActorState.X)).SetValue(actor, x);
            typeof(ActorState).GetProperty(nameof(ActorState.Height)).SetValue(actor, height);
            typeof(ActorState).GetProperty(nameof(ActorState.ManualControl)).SetValue(actor, true);
            typeof(ActorState).GetProperty(nameof(ActorState.SupportPlatform)).SetValue(actor, -1);
            return actor;
        }

        private static void Step(Grid map, ActorState actor, int direction, bool jump = false, bool sprint = false)
        {
            TerrainHeroMotion.MoveHorizontal(map, actor, actor.X + direction * Rules.MoveSpeed(sprint) * (float)Delta);
            TerrainHeroMotion.Tick(map, actor, Rules, Delta, jump, false);
        }

        private static bool Blocked(Grid map, ActorState actor) => BodyBlocked(
            map, actor.X, actor.Height, HeroControlDefinition.BodyHalfWidth, HeroControlDefinition.BodyHeight);

        private static Grid Ramp(bool steep, bool mirror)
        {
            var map = new Grid();
            for (int x = 0; x < 320; x++) for (int row = 100; row < 105; row++) map.Set(x, row);
            for (int x = 100; x < 130; x++) map.Set(mirror ? 319 - x : x, 99);
            map.Set(mirror ? 219 : 100, 99, steep ? (mirror ? TerrainCellShape.Fall : TerrainCellShape.Rise)
                : (mirror ? TerrainCellShape.FallLow : TerrainCellShape.RiseLow));
            if (!steep) map.Set(mirror ? 218 : 101, 99, mirror ? TerrainCellShape.FallHigh : TerrainCellShape.RiseHigh);
            return map;
        }

        /// <summary>合成只读运动夹具；数组仅由测试准备阶段编辑，Read 提供正式格编码与地图边界。</summary>
        private sealed class Grid : IReadOnlyGrid
        {
            private readonly GridCell[] cells = new GridCell[320 * 192];
            public WorldIdentity World => default;
            public ulong CommitId => 0;
            public GridSample Read(CellCoord position)
            {
                int row = -position.V;
                if (position.U < 0 || position.U >= 320 || row < 0 || row >= 192) return GridSample.Unknown;
                return GridSample.FromCell(cells[row * 320 + position.U]);
            }
            public void Set(int x, int row, TerrainCellShape shape = TerrainCellShape.Full) =>
                cells[row * 320 + x] = new GridCell(2, 0, TerrainShapeGeometry.Encode(shape, false));
            public void Clear(int x, int row) => cells[row * 320 + x] = default;
        }
    }
}
