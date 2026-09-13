using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Logic.State;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// U2 的真实对象规则和事务回归；经由同一个权限队列验证工位、支付、冻结及恢复。
    /// 断言使用冻结原规则数值，能力缺失只改内存定义，不重新生成期望或覆盖人工资产。
    /// </summary>
    [Category("UnifiedSlice")]
    public sealed class UnifiedSliceTests
    {
        [UnityTest]
        public IEnumerator IndependentMovementAndGatheringPreserveOriginalTiming() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            ActorBehaviour worker = f.World.Index.Actors[0];
            ActorBehaviour other = f.World.Index.Actors[1];
            var frozen = f.Authority.CaptureProjection();
            WorksiteBehaviour site = f.World.Index.Worksites[0];
            Assert.That(f.World.IssueOrders(new[] { worker.Id }, site.Id, site.X), Is.EqualTo(1));
            Assert.That(site.WorkerId, Is.EqualTo(worker.Id));
            f.Step(12);
            Assert.That(f.World.Economy.Stock.Wood, Is.EqualTo(103), "Original travel plus one four-second production batch.");
            Assert.That(other.X, Is.EqualTo(187));
            Assert.That(frozen.World.Actors[0].X, Is.EqualTo(170), "Frozen frame must survive live state replacements.");
            Assert.That(f.World.IssueOrders(new[] { worker.Id }, 0, 200), Is.EqualTo(1));
            Assert.That(site.WorkerId, Is.Zero);
            Assert.That(site.CaptureState().Progress, Is.Zero);
            f.Step(8);
            Assert.That(worker.X, Is.EqualTo(200).Within(0.8), "Original MoveTo stops inside its arrival threshold.");
            Assert.That(worker.Activity, Is.EqualTo(ActorActivity.Idle));
        });

        [UnityTest]
        public IEnumerator HouseCreationPaysOnceAndCompletesWithOriginalCapacity() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            int worker = f.World.Index.Actors[0].Id;
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(2);
            int id = f.World.PlaceBuilding("house", 184, new[] { worker });
            Assert.That(id, Is.GreaterThan(0));
            BuildingBehaviour house = f.World.Index.Find<BuildingBehaviour>(id);
            Assert.That(house.Hp, Is.EqualTo(20));
            Assert.That(house.Progress, Is.Zero);
            Assert.That(f.World.Economy.Stock.Wood, Is.EqualTo(75));
            Assert.That(house.WorkerId, Is.EqualTo(worker));
            f.Step(11);
            Assert.That(house.IsComplete, Is.True);
            Assert.That(house.Hp, Is.EqualTo(100).Within(0.000001));
            Assert.That(house.WorkerId, Is.Zero);
            Assert.That(f.World.Economy.Capacity, Is.EqualTo(12));
            Assert.That(f.World.Catalog.Balance.Buildings["house"].Hp, Is.EqualTo(100));
        });

        [UnityTest]
        public IEnumerator ConcurrentSpendingAndResendsUseOneAuthorityQueue() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            JObject save = JObject.Parse(f.World.SaveCodec.Serialize(f.Authority.CaptureWorld()));
            save["world"]["economy"]["resources"]["wood"] = 25;
            f.ExpectRestoreActivation();
            f.World.Restore(save.ToString());
            var workers = f.World.Index.Actors;
            var first = f.Request(SessionOperation.PlaceBuilding, 1, new[] { workers[0].Id }, x: 184, kind: "house");
            var second = f.Request(SessionOperation.PlaceBuilding, 1, new[] { workers[1].Id }, x: 620, kind: "house");
            f.Authority.Submit(f.Host, first);
            f.Authority.Submit(f.Client, second);
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(2);
            var results = f.Authority.Tick();
            Assert.That(results[0].Code, Is.EqualTo(SessionResultCode.Applied));
            Assert.That(results[1].Code, Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(f.World.Economy.Stock.Wood, Is.Zero);
            Assert.That(f.World.Index.Buildings.Count, Is.EqualTo(3));
            Assert.That(f.Authority.Submit(f.Host, first), Is.SameAs(results[0]));
            Assert.That(f.Authority.Submit(f.Host, f.Request(SessionOperation.PlaceBuilding, 1,
                new[] { workers[0].Id }, x: 220, kind: "house")).Code, Is.EqualTo(SessionResultCode.SequenceConflict));
        });

        [UnityTest]
        public IEnumerator FailedAssemblyPreservesPaymentIdsAndExistingWork() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            f.World.SetTime(true, 1);
            ActorBehaviour worker = f.World.Index.Actors[0];
            WorksiteBehaviour site = f.World.Index.Worksites[0];
            f.World.IssueOrders(new[] { worker.Id }, site.Id, site.X);
            string before = f.World.SaveCodec.Serialize(f.Authority.CaptureWorld());
            f.BreakHouse();
            try
            {
                var request = f.Request(SessionOperation.PlaceBuilding, 1, new[] { worker.Id }, x: 184, kind: "house");
                f.Authority.Submit(f.Host, request);
                Assert.That(f.Authority.Tick().Single().Code, Is.EqualTo(SessionResultCode.ObjectUnavailable));
                Assert.That(f.World.SaveCodec.Serialize(f.Authority.CaptureWorld()), Is.EqualTo(before));
                Assert.That(f.Authority.Closed, Is.False);
            }
            finally { f.RepairHouse(); }
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(2);
            Assert.That(f.World.PlaceBuilding("house", 184, new[] { worker.Id }), Is.EqualTo(11));
        });

        [UnityTest]
        public IEnumerator PolicyPauseAndMalformedTargetsCannotMutateObjects() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            int worker = f.World.Index.Actors[0].Id;
            f.Authority.Submit(f.Host, f.Request(SessionOperation.SetControlMode, 1, value: (int)CampControlMode.HostOnly));
            f.Authority.Submit(f.Client, f.Request(SessionOperation.IssueOrders, 1, new[] { worker }, x: 240, policy: 0));
            var results = f.Authority.Tick();
            Assert.That(results[1].Code, Is.EqualTo(SessionResultCode.PolicyChanged));
            f.Authority.Submit(f.Client, f.Request(SessionOperation.IssueOrders, 2, new[] { worker }, x: 240));
            Assert.That(f.Authority.Tick().Single().Code, Is.EqualTo(SessionResultCode.PermissionDenied));
            f.Authority.Submit(f.Host, f.Request(SessionOperation.IssueOrders, 2, new[] { worker }, target: 900000, x: 240));
            Assert.That(f.Authority.Tick().Single().Code, Is.EqualTo(SessionResultCode.InvalidRequest));
            f.World.SetTime(true, 2);
            double elapsed = f.World.Elapsed;
            f.Step(2);
            Assert.That(f.World.Elapsed, Is.EqualTo(elapsed));
            f.World.SetTime(false, 2);
            f.Step(1);
            Assert.That(f.World.Elapsed, Is.EqualTo(elapsed + 2).Within(0.000001));
        });

        [UnityTest]
        public IEnumerator SaveRestoreKeepsOriginalSceneObjectAndRejectsBadCandidates() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create(true);
            var original = f.SceneWorker;
            int worker = f.World.Index.Actors[0].Id;
            var previousContext = f.World.EntityContext;
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(2);
            int house = f.World.PlaceBuilding("house", 184, new[] { worker });
            f.Step(3);
            string saved = f.World.SaveCodec.Serialize(f.Authority.CaptureWorld());
            f.ExpectRestoreActivation();
            f.World.Restore(saved);
            Assert.That(f.World.Index.Find<ActorBehaviour>(worker).Object, Is.SameAs(original));
            Assert.That(previousContext.IsAlive, Is.False);
            Assert.That(f.World.Index.Find<BuildingBehaviour>(house).Progress, Is.GreaterThan(0).And.LessThan(1));
            Assert.That(f.World.SaveCodec.Serialize(f.Authority.CaptureWorld()), Is.EqualTo(saved));
            JObject broken = JObject.Parse(saved);
            broken["world"]["actors"][0]["target_id"] = 99999;
            Assert.Throws<FormatException>(() => f.World.Restore(broken.ToString()));
            Assert.That(f.World.SaveCodec.Serialize(f.Authority.CaptureWorld()), Is.EqualTo(saved));
            f.BreakHouse();
            try
            {
                Assert.Throws<InvalidOperationException>(() => f.World.Restore(saved));
                Assert.That(f.World.SaveCodec.Serialize(f.Authority.CaptureWorld()), Is.EqualTo(saved));
            }
            finally { f.RepairHouse(); }
            JObject legacy = JObject.Parse(saved);
            legacy["format_version"] = 1;
            Assert.Throws<FormatException>(() => f.World.Restore(legacy.ToString()));
            Assert.That(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(original.gameObject), Is.Not.Null);
        });

        [UnityTest]
        public IEnumerator NewProjectionCopiesIdentityAndStateNotificationsCannotReenter() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            bool blocked = false;
            using var subscription = f.World.Economy.ReactiveState.Subscribe(state =>
            {
                if (state?.Wood != 75) return;
                try { f.World.SetTime(true, 1); }
                catch (InvalidOperationException) { blocked = true; }
                Assert.That(f.World.Index.Buildings.Count, Is.EqualTo(3));
            });
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(2);
            f.World.PlaceBuilding("house", 184, new[] { f.World.Index.Actors[0].Id });
            Assert.That(blocked, Is.True);
            var codec = new ProjectionCodec(f.World.Catalog, f.World.Layout);
            var frame = f.Authority.CaptureProjection();
            var decoded = codec.Decode(codec.Encode(frame));
            Assert.That(decoded.World.Identities.Count, Is.EqualTo(f.World.Index.Count));
            Assert.That(decoded.World.Identities.Select(i => i.DefinitionGuid),
                Is.EqualTo(frame.World.Identities.Select(i => i.DefinitionGuid)));
        });
    }
}
