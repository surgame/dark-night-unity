using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using static DarkNights.Tests.ShipScenario;

namespace DarkNights.Tests
{
    /// <summary>远征全员倒下后的事务恢复回归；用低氧存档夹具触发真实死亡、结算、保存和可信移动输入。</summary>
    public sealed class ExpeditionRecoveryTests
    {
        [UnityTest]
        public IEnumerator ExhaustedCrewCanMoveAfterSettlement() => UniTask.ToCoroutine(() => Verify(false));

        [UnityTest]
        public IEnumerator FailedSettlementDoesNotReviveUntilCommit() => UniTask.ToCoroutine(() => Verify(true));

        private static async UniTask Verify(bool failSave)
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = JourneyScenario.Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0);
            await JourneyScenario.Descent(authority, world, host);
            JourneyScenario.Land(authority, world, host);
            int id = Hero(world, 0).Id;
            var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
            var actor = save["world"]["actors"].Single(a => (int)a["id"] == id);
            actor["hp"] = .001;
            var crew = save["world"]["expedition"]["Crew"].Single(a => (int)a["Id"] == id);
            crew["Oxygen"] = 0; crew["Boarded"] = false; crew["Iron"] = 3;
            // 角色仍在舱内坐标，但明确为舱外低氧夹具；下一权威 Tick 先执行缺氧及结算。
            world.Restore(save.ToString()); host = Connect(authority, 0);
            var hero = Hero(world, 0);
            int lease = hero.CaptureState().ControlLease;
            string persisted = null; int writes = 0;
            world.Expedition.CommitSave = snapshot =>
            {
                if (failSave) throw new System.IO.IOException("injected settlement failure");
                persisted = world.SaveCodec.Serialize(snapshot); writes++;
            };
            authority.Tick();
            if (failSave)
            {
                Assert.That(world.Paused, Is.True);
                Assert.That(world.CaptureView().Expedition.Settled, Is.False);
                Assert.That(hero.Hp, Is.EqualTo(.001).Within(.00001), "死亡与复原都必须随结算事务回滚");
                Assert.That(hero.CaptureState().ControlLease, Is.EqualTo(lease));
                failSave = false; world.SetTime(false, 1); authority.Tick();
            }
            Assert.That(world.CaptureView().Expedition.Settled, Is.True);
            Assert.That(world.CaptureView().Expedition.LostCargo, Is.EqualTo(3));
            Assert.That(hero.Hp, Is.EqualTo(hero.MaximumHp));
            Assert.That(hero.CaptureState().Oxygen, Is.GreaterThan(0));
            Assert.That(hero.CaptureState().ControlLease, Is.GreaterThan(lease));
            Assert.That(Hero(world, 0).Id, Is.EqualTo(id));
            Assert.That(writes, Is.EqualTo(1));
            float before = hero.X;
            for (int i = 0; i < 15; i++) Input(authority, host, hero, 1);
            Assert.That(hero.X, Is.GreaterThan(before + 1), "结算后真实输入必须能移动");
            world.Restore(persisted); host = Connect(authority, 0); hero = Hero(world, 0);
            before = hero.X;
            for (int i = 0; i < 15; i++) Input(authority, host, hero, 1);
            Assert.That(hero.X, Is.GreaterThan(before + 1), "保存恢复后仍能移动");
            Assert.That(writes, Is.EqualTo(1), "已结算状态不重复写盘或结算");
        }
    }
}
