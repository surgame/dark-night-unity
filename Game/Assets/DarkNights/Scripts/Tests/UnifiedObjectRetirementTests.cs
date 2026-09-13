using System;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>验证真实状态回调中的退休、动态权威撤销及场景原实例接管；退出不能遗留发布状态或调度。</summary>
    public sealed class UnifiedObjectRetirementTests
    {
        [Test]
        public void DefinitionChangeReassemblesCapabilitiesWithoutKeepingOldState()
        {
            Run((instance, session) =>
            {
                var changed = ScriptableObject.CreateInstance<ObjectDefinition>();
                changed.EditorSetIdentity(Guid.NewGuid().ToString("N"), "test.changed", true);
                changed.BehaviourTypes.Add(typeof(DarkNights.Runtime.Diagnostics.AssemblyProbeBehaviour).FullName);
                changed.SharedConfigs.Add(new DarkNights.Runtime.Diagnostics.AssemblyProbeConfig { Value = 19 });
                var previous = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                try
                {
                    instance.Initialize("changed", changed, session: session, activate: false);
                    Assert.That(previous.State, Is.Null);
                    Assert.That(instance.GetBehaviour<UnifiedObjectProbeBehaviour>(), Is.Null);
                    Assert.That(instance.GetBehaviour<DarkNights.Runtime.Diagnostics.AssemblyProbeBehaviour>().Observed, Is.EqualTo(19));
                    Assert.That(instance.GetBehaviourCount(), Is.EqualTo(1));
                }
                finally { instance.Release(); UnityEngine.Object.DestroyImmediate(changed); }
            });
        }

        [Test]
        public void FailedRetirementCannotReuseTheDamagedBehaviour()
        {
            Run((instance, session) =>
            {
                instance.Definition.UseMonolithicPooling = true;
                var before = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                before.FailRetirement = true;
                LogAssert.Expect(LogType.Exception, "InvalidOperationException: Expected retirement failure");
                LogAssert.Expect(LogType.Exception, "InvalidOperationException: Expected retirement failure");
                instance.Initialize("replacement", instance.Definition, session: session, activate: false);
                var after = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                Assert.That(after, Is.Not.SameAs(before));
                Assert.That(after.State.Number, Is.EqualTo(11));
                after.Change(12);
                Assert.That(after.State.Number, Is.EqualTo(12));
            });
        }

        [Test]
        public void RetirementDuringStateNotificationClearsAfterDelivery()
        {
            Run((instance, session) =>
            {
                var behaviour = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                behaviour.ObserveInSession(state => { if (state?.Number == 22) session.Dispose(); });
                behaviour.Change(22);
                Assert.That(behaviour.State, Is.Null);
                Assert.That(session.IsAlive, Is.False);
                Assert.That(instance.IsActive, Is.False);
                Assert.That(behaviour.CanWrite, Is.False);
            });
        }

        [Test]
        public void RevokedAuthorityCannotWriteEvenBeforeContextDisposal()
        {
            bool current = true;
            Run((instance, session) =>
            {
                var behaviour = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                current = false;
                LogAssert.Expect(LogType.Error, "MutateState requires a current authoritative session.");
                behaviour.Change(50);
                Assert.That(behaviour.State.Number, Is.EqualTo(11));
                Assert.Throws<InvalidOperationException>(instance.Activate);
            }, () => current);
        }

        private static void Run(Action<ObjectInstance, ObjectSessionContext> action, Func<bool> authority = null)
        {
            var container = new DIContainer();
            container.Initialize();
            container.Register(new UnifiedObjectProbeDependency(1));
            using var session = ObjectSessionContext.CreateAuthority(container, authority ?? (() => true));
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.EditorSetIdentity(Guid.NewGuid().ToString("N"), "test.retirement", true);
            definition.BehaviourTypes.Add(typeof(UnifiedObjectProbeBehaviour).AssemblyQualifiedName);
            definition.SharedConfigs.Add(new UnifiedObjectProbeConfig());
            var instance = new GameObject("RetirementTest").AddComponent<ObjectInstance>();
            try
            {
                instance.Initialize("test", definition, session: session, activate: false);
                action(instance, session);
            }
            finally
            {
                instance.Release();
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
                UnityEngine.Object.DestroyImmediate(definition);
                session.Dispose();
                container.OnReturnToPool();
            }
        }
    }
}
