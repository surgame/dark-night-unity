using System;
using System.Collections;
using AnyRules.Next;
using DarkNights.Core.Logic.Terrain;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>地质切片的定向边界回归；核对真实地图原子移格、触发、占位、暂停、退休及正式挥镐清障。编写不等于执行，联机、手感与视觉仍需另行验收。</summary>
    public sealed class GeologyEventTests
    {
        private GeologyEventFixture fixture;

        [UnityTest]
        public IEnumerator SupportMustDisappearAndLandingPublishesWithCommittedState() => UniTask.ToCoroutine(async () =>
        {
            using var owned = fixture = await GeologyEventFixture.Create();
            fixture.Tick(60); Assert.That(fixture.Event.Capture().Phase, Is.Zero);
            var map = fixture.World.Terrain.Map;
            fixture.Damage(fixture.Config.Support, 1);
            fixture.Tick(); Assert.That(fixture.Event.Capture().Phase, Is.Zero);
            fixture.BreakSupport(); fixture.Tick(); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(1));
            fixture.Tick(60); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(1), "完整前兆窗口结束前不允许落地。");
            int landings = 0;
            map.Changed += change =>
            {
                if (map.Read(fixture.Config.Landing(0)).Cell.IsEmpty) return;
                landings++; Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(4));
                Assert.That(fixture.Event.Capture().SoilMask, Is.EqualTo((1u << fixture.Config.RubbleCount) - 1));
                for (int i = 0; i < fixture.Config.RockCount; i++) Assert.That(map.Read(fixture.Config.Source(i)).Cell.IsEmpty, Is.True);
                for (int i = 0; i < fixture.Config.RubbleCount; i++)
                {
                    Assert.That(TerrainShapeGeometry.Decode(map.Read(fixture.Config.Landing(i)).Cell.Flags), Is.EqualTo(fixture.Config.PileShape(i)));
                    Assert.That(map.Query(fixture.Config.Landing(i)).State.Durability,
                        Is.EqualTo(map.Rules.Business.Get(map.Tiles.ByKey("slate")).MaximumDurability));
                }
            };
            fixture.Tick(360); Assert.That(landings, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator OccupantBlocksInsertionAndWaitIsBounded() => UniTask.ToCoroutine(async () =>
        {
            using var owned = fixture = await GeologyEventFixture.Create();
            fixture.BreakSupport(); fixture.Tick(); fixture.Position(fixture.Config.LandingU * 16);
            fixture.Tick(360); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(3));
            var map = fixture.World.Terrain.Map;
            Assert.That(map.Read(fixture.Config.Landing(0)).Cell.IsEmpty, Is.True);
            fixture.Tick(600); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(6));
            Assert.That(map.Read(fixture.Config.Source(0)).Cell.IsEmpty, Is.True, "岩体开始坠落时已从洞顶脱离，不回填源岩。");
            fixture.Position(704); fixture.Tick(300);
            Assert.That(map.Read(fixture.Config.Landing(0)).Cell.IsEmpty, Is.True);
        });

        [UnityTest]
        public IEnumerator OccupantLeavingAllowsOneLandingAndPausedClockDoesNotAdvance() => UniTask.ToCoroutine(async () =>
        {
            using var owned = fixture = await GeologyEventFixture.Create();
            fixture.BreakSupport(); fixture.Tick(); fixture.Position(fixture.Config.LandingU * 16);
            fixture.Lab.SetPaused(true); fixture.Tick(180);
            Assert.That(fixture.Event.Capture().Clock, Is.Zero);
            Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(1));
            fixture.Lab.SetPaused(false); fixture.Tick(360);
            Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(3));
            fixture.Position(704); fixture.Tick(); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(4));
        });

        [UnityTest]
        public IEnumerator TerrainChangedDuringWarningRetiresWithoutPartialFill() => UniTask.ToCoroutine(async () =>
        {
            using var owned = fixture = await GeologyEventFixture.Create();
            fixture.BreakSupport(); fixture.Tick();
            var map = fixture.World.Terrain.Map;
            fixture.Damage(fixture.Config.Source(1), int.MaxValue);
            fixture.Tick(180); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(6));
            for (int i = 0; i < fixture.Config.RubbleCount; i++) Assert.That(map.Read(fixture.Config.Landing(i)).Cell.IsEmpty, Is.True);
            Assert.That(map.Read(fixture.Config.Source(0)).Cell.IsEmpty, Is.False);
        });

        [UnityTest]
        public IEnumerator AbortedPlacementRestoresTheOriginalMapAndState() => UniTask.ToCoroutine(async () =>
        {
            using var owned = fixture = await GeologyEventFixture.Create();
            fixture.BreakSupport(); fixture.Tick(150);
            var map = fixture.World.Terrain.Map;
            int phase = fixture.Event.Capture().Phase;
            bool sourceEmpty = map.Read(fixture.Config.Source(0)).Cell.IsEmpty;
            ulong commit = map.CommitId;
            Assert.Throws<InvalidOperationException>(() => fixture.World.Mutations.Run<bool>(() =>
            {
                for (int i = 0; i < 300; i++) fixture.Event.Tick(1d / 60);
                throw new InvalidOperationException("注入候选提交失败");
            }));
            Assert.That(map.CommitId, Is.EqualTo(commit));
            Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(phase));
            Assert.That(map.Read(fixture.Config.Source(0)).Cell.IsEmpty, Is.EqualTo(sourceEmpty));
            Assert.That(map.Read(fixture.Config.Landing(0)).Cell.IsEmpty, Is.True);
        });

        [UnityTest]
        public IEnumerator FormalPickaxeClearsRubbleWithoutRearming() => UniTask.ToCoroutine(async () =>
        {
            using var owned = fixture = await GeologyEventFixture.Create();
            fixture.BreakSupport(); fixture.Tick(360); fixture.Position((fixture.Config.Landing(0).U - 1) * 16);
            var map = fixture.World.Terrain.Map;
            for (int frame = 0; frame < 1800; frame++)
            {
                int target = 0;
                while (target < fixture.Config.RubbleCount && map.Read(fixture.Config.Landing(target)).Cell.IsEmpty) target++;
                if (target == fixture.Config.RubbleCount) break;
                var cell = fixture.Config.Landing(target);
                float dx = cell.U * 16 - fixture.Lab.Hero.X;
                float aim = (float)(Math.Atan2(632 + cell.V * 16 - fixture.Lab.Hero.Height - 28, dx) * 180 / Math.PI);
                fixture.Lab.Step(dx > 32 ? 1 : 0, false, false, frame == 0, true, aim);
            }
            fixture.Tick();
            for (int i = 0; i < fixture.Config.RubbleCount; i++) Assert.That(map.Read(fixture.Config.Landing(i)).Cell.IsEmpty, Is.True,
                "剩余=" + fixture.Config.Landing(i) + "; hero=" + fixture.Lab.Hero.X + "," + fixture.Lab.Hero.Height +
                "; target=" + fixture.Lab.MiningTarget);
            Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(5));
            fixture.Tick(180); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(5));
        });

        [UnityTest]
        public IEnumerator RegionUsesLiveHeroAndDoesNotNeedMining() => UniTask.ToCoroutine(async () =>
        {
            using var owned = fixture = await GeologyEventFixture.Create(true);
            fixture.Position((fixture.Config.RegionU + 1) * 16, false); fixture.Tick(); Assert.That(fixture.Event.Capture().Phase, Is.Zero);
            fixture.Position((fixture.Config.RegionU + 1) * 16); fixture.Tick(); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(1));
            fixture.Position(704); fixture.Tick(360); Assert.That(fixture.Event.Capture().Phase, Is.EqualTo(4));
        });
    }
}
