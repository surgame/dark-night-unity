using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证主角武器仍使用职业原前摇、一次点击与原伤害范围，并将高度纳入近战距离。
    /// 独立临时世界把角色移到远离友军处，敌人通过原 YYGC 创建入口生成，不改冻结关卡或期望规则。
    /// </summary>
    public sealed class HeroCombatTests
    {
        [UnityTest]
        public IEnumerator ClickUsesOriginalWindupAndDoesNotRepeatAfterRelease() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create();
            MoveAside(f);
            f.Command(SessionOperation.ClaimHero);
            var enemy = UnifiedGameplayProbe.Spawn(f.World, "zombie", f.Actor.X + 1);
            double hp = enemy.Hp;
            Assert.That(f.Command(SessionOperation.UseHeroItem, target: enemy.Id, kind: "weapon",
                value: f.State.SelectionRevision).Code, Is.EqualTo(SessionResultCode.Applied));
            Assert.That(enemy.Hp, Is.EqualTo(hp));
            int ticks = Math.Max(0, (int)Math.Ceiling(f.State.Windup * 60) - 1);
            f.Step(ticks); Assert.That(enemy.Hp, Is.EqualTo(hp), "Original windup must elapse before damage.");
            // 原前摇逐步相减可能在数学边界残留正浮点尾数；允许原实现的一步量化，不改攻击时机。
            f.Step(2);
            double damage = hp - enemy.Hp;
            Assert.That(damage, Is.InRange(f.Actor.Definition.Damage[0], f.Actor.Definition.Damage[1]));
            f.Step(60);
            Assert.That(enemy.Hp, Is.EqualTo(hp - damage), "A released click cannot start a second attack.");
        });

        [UnityTest]
        public IEnumerator AirborneMeleeCannotHitGroundTargetOutsideVerticalReach() => UniTask.ToCoroutine(async () =>
        {
            using var f = await HeroTestSession.Create(); MoveAside(f);
            f.Command(SessionOperation.ClaimHero);
            f.Input(jumpPressed: true); f.Step(14);
            Assert.That(f.State.Height, Is.GreaterThan(f.Actor.Definition.Range));
            var enemy = UnifiedGameplayProbe.Spawn(f.World, "zombie", f.Actor.X);
            double hp = enemy.Hp;
            Assert.That(f.Command(SessionOperation.UseHeroItem, target: enemy.Id, kind: "weapon",
                value: f.State.SelectionRevision).Code, Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(enemy.Hp, Is.EqualTo(hp));
        });

        private static void MoveAside(HeroTestSession f)
        {
            var save = JObject.Parse(f.World.SaveCodec.Serialize(f.World.CaptureWorld()));
            var actor = save["world"]["actors"][0];
            actor["x"] = actor["move_x"] = actor["rally_x"] = f.World.Layout.WorldWidth - 40;
            f.World.Restore(save.ToString());
        }
    }
}
