using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Config;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;

namespace DarkNights.Tests
{
    /// <summary>氧气移除的真实会话行为回归；可信双人输入离开船边后推进五分钟，隔离威胁而不修改生产规则或角色生命。</summary>
    public sealed class OxygenRemovalTests
    {
        [UnityTest]
        public IEnumerator TwoCrewStayOutsideForFiveMinutesWithoutDamageOrReturn() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            // 仅提高本用例的威胁阈值，避免把战斗死亡混入生存时间回归；不写作者JSON。
            string config = Path.Combine(RuleScenario.RepositoryRoot, "Game/Assets/DarkNights/Res/Config");
            var balance = JObject.Parse(File.ReadAllText(Path.Combine(config, "balance.json")));
            balance["expedition"]["ThreatSeconds"] = 10000;
            var catalog = GameCatalogJson.Parse(balance.ToString(), File.ReadAllText(Path.Combine(config, "pinewatch.json")));
            var world = Create(scope, catalog);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var guest = Connect(authority, 1);
            Assert.That(Send(authority, host, 1, "depart"), Is.EqualTo(SessionResultCode.Applied));
            var first = Hero(world, 0); var second = Hero(world, 1);
            float outside = Ship(world).X - 220;
            Walk(authority, host, first, outside);
            Walk(authority, guest, second, outside);
            AssertOutside(first, outside); AssertOutside(second, outside);
            double firstHp = first.Hp, secondHp = second.Hp;
            int firstLease = first.CaptureState().ControlLease, secondLease = second.CaptureState().ControlLease;
            for (int tick = 0; tick < 18000; tick++)
            {
                authority.Tick();
                if (tick % 300 == 0) await UniTask.Yield();
            }
            AssertOutside(first, outside); AssertOutside(second, outside);
            Assert.That(first.Hp, Is.EqualTo(firstHp)); Assert.That(second.Hp, Is.EqualTo(secondHp));
            Assert.That(first.CaptureState().ControlLease, Is.EqualTo(firstLease));
            Assert.That(second.CaptureState().ControlLease, Is.EqualTo(secondLease));
            Assert.That(world.CaptureView().Expedition.Phase, Is.EqualTo(1));
            Assert.That(world.CaptureView().Expedition.Clock, Is.GreaterThanOrEqualTo(299.9));
            Assert.That(world.CaptureView().Expedition.Settled, Is.False);
            Assert.That(world.Index.Actors, Has.None.Matches<ActorBehaviour>(a => a.Enemy));
        });

        private static void AssertOutside(ActorBehaviour actor, float x)
        {
            Assert.That(actor.CaptureState().Boarded, Is.False, "必须真实离开船舱");
            Assert.That(actor.X, Is.EqualTo(x).Within(1), "时间推进不能把人物搬回飞船");
        }

        [UnityTest]
        public IEnumerator RemovedCommandAndPreviousContractsAreRejectedWithoutChangingWorld() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0);
            string before = world.SaveCodec.Serialize(world.CaptureWorld());
            var relay = new SessionRequest(SessionOperation.Expedition, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, 1, new[] { hero.Id }, kind: "relay",
                controlLease: hero.CaptureState().ControlLease);
            Assert.That(authority.Submit(host, relay).Code, Is.EqualTo(SessionResultCode.InvalidRequest));
            var previous = new SessionRequest(SessionOperation.Expedition, 22,
                authority.Epoch, authority.PolicyRevision, 2, kind: "depart");
            Assert.That(authority.Submit(host, previous).Code, Is.EqualTo(SessionResultCode.ProtocolMismatch));
            var oldSave = JObject.Parse(before); oldSave["format_version"] = 15;
            Assert.Throws<FormatException>(() => world.Restore(oldSave.ToString()));
            Assert.That(world.SaveCodec.Serialize(world.CaptureWorld()), Is.EqualTo(before),
                "拒绝旧合同及已移除命令不能部分修改当前世界");
        });
    }
}
