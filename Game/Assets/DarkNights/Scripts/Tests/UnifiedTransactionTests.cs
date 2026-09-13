using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// U2 提交边界的故障回归，验证跨对象通知一致性、异常清理和客户端候选失败。
    /// 所有断言运行在真实 YYGC 状态／资源路径，不把 DTO 或私有旧实体当作运行世界。
    /// </summary>
    [Category("UnifiedSlice")]
    public sealed class UnifiedTransactionTests
    {
        [UnityTest]
        public IEnumerator NotificationsObserveAllStatesAndCannotRetireSibling() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            ActorBehaviour worker = f.World.Index.Actors[0];
            bool notified = false;
            using var subscription = f.World.Economy.ReactiveState.Subscribe(state =>
            {
                if (state?.Wood != 75) return;
                notified = true;
                BuildingBehaviour house = f.World.Index.Buildings.Last();
                Assert.That(worker.CaptureState().TargetId, Is.EqualTo(house.Id));
                Assert.That(house.CaptureState().WorkerId, Is.EqualTo(worker.Id));
                Assert.Throws<InvalidOperationException>(worker.Object.Retire);
                Assert.Throws<InvalidOperationException>(f.World.EntityContext.Dispose);
                Assert.Throws<InvalidOperationException>(f.World.Dispose);
            });
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(2);
            int id = f.World.PlaceBuilding("house", 184, new[] { worker.Id });
            Assert.That(notified, Is.True);
            Assert.That(f.World.Index.Find<BuildingBehaviour>(id).WorkerId, Is.EqualTo(worker.Id));
            Assert.That(worker.Object.IsActive, Is.True);
            f.Step(1);
        });

        [UnityTest]
        public IEnumerator ThrowingNotificationDoesNotUndoOnlyPartOfPayment() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            using var subscription = f.World.Economy.ReactiveState.Subscribe(state =>
            {
                if (state?.Wood == 75) throw new InvalidOperationException("u2-observer-failure");
            });
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(2);
            LogAssert.Expect(LogType.Exception, new Regex("u2-observer-failure"));
            int worker = f.World.Index.Actors[0].Id;
            int house = f.World.PlaceBuilding("house", 184, new[] { worker });
            Assert.That(f.World.Economy.Stock.Wood, Is.EqualTo(75));
            Assert.That(f.World.Index.Find<BuildingBehaviour>(house).WorkerId, Is.EqualTo(worker));
            Assert.That(f.World.Index.Find<ActorBehaviour>(worker).TargetId, Is.EqualTo(house));
            Assert.That(f.World.Mutations.IsOpen, Is.False);
            f.World.SetTime(true, 1);
        });

        [UnityTest]
        public IEnumerator RollbackExceptionsStillRunRemainingCleanupAndUnlockBatch() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            MethodInfo add = typeof(ObjectMutationBatch).GetMethod("OnRollback", BindingFlags.Instance | BindingFlags.NonPublic);
            bool remaining = false;
            LogAssert.Expect(LogType.Exception, new Regex("u2-rollback-failure"));
            Assert.Throws<ArgumentException>(() => f.World.Mutations.Run<int>(() =>
            {
                add.Invoke(f.World.Mutations, new object[] { (Action)(() => remaining = true) });
                add.Invoke(f.World.Mutations, new object[] { (Action)(() => throw new InvalidOperationException("u2-rollback-failure")) });
                throw new ArgumentException("u2-abort");
            }));
            Assert.That(remaining, Is.True);
            Assert.That(f.World.Mutations.IsOpen, Is.False);
            f.World.SetTime(true, 1);
            Assert.That(f.World.Paused, Is.True);
        });

        [UnityTest]
        public IEnumerator FailedReplicaEpochKeepsCurrentObjectsAndAllowsRetry() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            using var replica = new ObjectReplica(f.Resources, f.Placements, null);
            var frame = f.World.CaptureView();
            f.ExpectRestoreActivation();
            replica.Apply(frame, 1);
            int worker = frame.Actors[0].Id;
            var old = replica.View(worker).Owner;
            var context = old.SessionContext;
            int beforeHouse = f.World.Index.Actors.Sum(a => a.Object.GetBehaviourCount()) +
                f.World.Index.Buildings.TakeWhile(b => b.RuleKey != "house").Sum(b => b.Object.GetBehaviourCount());
            f.BreakHouse();
            try
            {
                FormalObjectContentTests.ExpectRegistrationWithoutRuntime(beforeHouse);
                Assert.Throws<InvalidOperationException>(() => replica.Apply(frame, 2));
                Assert.That(replica.Count, Is.EqualTo(f.World.Index.Count));
                Assert.That(replica.View(worker).Owner, Is.SameAs(old));
                Assert.That(context.IsAlive, Is.True);
                Assert.That(old.GetBehaviour<ActorBehaviour>().CaptureState().X, Is.EqualTo(frame.Actors[0].X));
            }
            finally { f.RepairHouse(); }
            f.ExpectRestoreActivation();
            replica.Apply(frame, 2);
            Assert.That(context.IsAlive, Is.False);
            Assert.That(replica.Count, Is.EqualTo(f.World.Index.Count));
        });

        [UnityTest]
        public IEnumerator UnsupportedSaveVersionsHaveExplicitErrors() => UniTask.ToCoroutine(async () =>
        {
            using var f = await UnifiedSliceFixture.Create();
            string before = f.World.SaveCodec.Serialize(f.World.CaptureWorld());
            foreach (string legacy in new[] { "{\"schema_version\":1}",
                "{\"format\":\"dark-nights.world\",\"format_version\":1}" })
            {
                var error = Assert.Throws<FormatException>(() => f.World.Restore(legacy));
                Assert.That(error.Message, Is.EqualTo("不支持的存档版本。"));
            }
            Assert.That(f.World.SaveCodec.Serialize(f.World.CaptureWorld()), Is.EqualTo(before));
        });
    }
}
