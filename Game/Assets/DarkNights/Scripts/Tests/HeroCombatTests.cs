using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 手枪输入、一次命中和有界状态槽的待执行回归；复用真实 YYGC 会话与可信输入入口。
    /// 原职业自动攻击仍由原规则测试覆盖，此处不再要求手持枪执行旧职业近战。
    /// </summary>
    public sealed class HeroCombatTests
    {
        [UnityTest]
        public IEnumerator ShortClickHitsOnceAndDuplicateInputCannotFireAgain() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            MoveAside(f); f.Command(SessionOperation.ClaimHero);
            var enemy = UnifiedGameplayProbe.Spawn(f.World, "zombie", f.Actor.X + 1);
            double hp = enemy.Hp;
            var click = f.Packet(usePressed: true, useReleased: true);
            Assert.That(f.Authority.SubmitInput(f.Host, click), Is.True);
            Assert.That(f.Authority.SubmitInput(f.Host, click), Is.False);
            f.Step(2);
            Assert.That(enemy.Hp, Is.LessThan(hp));
            double after = enemy.Hp;
            f.Step(30);
            Assert.That(enemy.Hp, Is.EqualTo(after));
            Assert.That(f.World.Projectiles.CaptureState().Ballistics.Count(p => p.Kind == 1), Is.Zero);
        });

        [UnityTest]
        public IEnumerator RepeatedFireReusesBoundedSlotsAndRetiresExpiredShots() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero);
            long before = f.World.Projectiles.CaptureState().NextViewId;
            f.Step(300, useHeld: true, keepAlive: true);
            var state = f.World.Projectiles.CaptureState();
            Assert.That(state.Ballistics.Length, Is.EqualTo(128));
            Assert.That(state.NextViewId - before, Is.GreaterThan(10));
            Assert.That(state.Ballistics.Count(p => p.Kind != 0), Is.LessThan(10));
            f.Input(); f.Step(120);
            Assert.That(f.World.Projectiles.CaptureState().Ballistics.All(p => p.Kind == 0), Is.True);
        });

        [UnityTest]
        public IEnumerator NonfiniteAimAndOtherConnectionCannotFire() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            f.Command(SessionOperation.ClaimHero);
            Assert.That(f.Authority.SubmitInput(f.Host, f.Packet(aim: float.NaN, usePressed: true)), Is.False);
            Assert.That(f.Authority.SubmitInput(f.Guest, f.Packet(usePressed: true)), Is.False);
            f.Step(2);
            Assert.That(f.World.Projectiles.CaptureState().Ballistics.All(p => p.Kind == 0), Is.True);
        });

        private static void MoveAside(HeroTestSession f)
        {
            var save = JObject.Parse(f.World.SaveCodec.Serialize(f.World.CaptureWorld()));
            var actor = save["world"]["actors"][0];
            actor["x"] = actor["move_x"] = actor["rally_x"] = f.World.Layout.WorldWidth - 80;
            f.World.Restore(save.ToString());
        }
    }
}
