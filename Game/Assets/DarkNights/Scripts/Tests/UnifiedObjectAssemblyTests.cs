using System;
using System.Linq;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using GameCore.Objects.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YY.Features.Players.View;

namespace DarkNights.Tests
{
    /// <summary>U1 真实装配回归，核验独立状态、可信会话、配置约束、失败清理和池化重入。</summary>
    public sealed class UnifiedObjectAssemblyTests
    {
        [Test]
        public void PreparedObjectsOwnIndependentStateAndActivateExactlyOnce()
        {
            using var session = Context(1);
            var definition = Definition();
            var first = Create(definition, session);
            var second = Create(definition, session);
            try
            {
                var a = first.GetBehaviour<UnifiedObjectProbeBehaviour>();
                var b = second.GetBehaviour<UnifiedObjectProbeBehaviour>();
                Assert.That(first.IsActive, Is.False);
                Assert.That(a.StartCount, Is.Zero);
                a.Change(25);
                Assert.That(a.State.Number, Is.EqualTo(25));
                Assert.That(b.State.Number, Is.EqualTo(11));
                Assert.That(((UnifiedObjectProbeConfig)definition.SharedConfigs.Single()).Number, Is.EqualTo(10));
                Assert.Throws<InvalidOperationException>(first.Activate);
                session.Activate();
                FormalObjectContentTests.ExpectRegistrationWithoutRuntime(1);
                first.Activate();
                first.Activate();
                Assert.That(a.StartCount, Is.EqualTo(1));
                session.Dispose();
                Assert.That(first.IsActive, Is.False);
                Assert.That(a.State, Is.Null);
                Assert.That(a.CanWrite, Is.False);
                Assert.Throws<ObjectDisposedException>(() => first.Initialize("retired", definition, session: session));
            }
            finally { Destroy(first, second); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void ReplicaCannotWriteAndCopiesIncomingSnapshot()
        {
            using var session = Context(2, false);
            var definition = Definition();
            var instance = Create(definition, session);
            try
            {
                var behaviour = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                LogAssert.Expect(LogType.Error, "MutateState requires a current authoritative session.");
                behaviour.Change(9);
                Assert.That(behaviour.State, Is.Null);
                var incoming = new UnifiedObjectProbeState { Number = 5 };
                incoming.Items.Add(3);
                behaviour.ApplySessionState(session, incoming);
                incoming.Items.Clear();
                Assert.That(behaviour.State.Items, Is.EqualTo(new[] { 3 }));
                using var other = Context(3, false);
                Assert.Throws<InvalidOperationException>(() => behaviour.ApplySessionState(other, incoming));
            }
            finally { Destroy(instance); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void ReentryRefreshesDependenciesAndDropsOldSubscriptions()
        {
            using var session = Context(1);
            using var next = Context(8);
            var definition = Definition();
            var instance = Create(definition, session);
            try
            {
                var before = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                int callbacks = 0;
                before.ObserveInSession(_ => callbacks++);
                instance.Retire();
                int retiredCount = callbacks;
                instance.Initialize("same", definition, session: session, activate: false);
                Assert.That(instance.GetBehaviour<UnifiedObjectProbeBehaviour>(), Is.SameAs(before));
                Assert.That(callbacks, Is.EqualTo(retiredCount));
                instance.Initialize("next", definition, session: next, activate: false);
                Assert.That(instance.GetBehaviour<UnifiedObjectProbeBehaviour>().State.Number, Is.EqualTo(18));
                Assert.That(instance.SessionContext, Is.SameAs(next));
                Assert.That(callbacks, Is.EqualTo(retiredCount));
            }
            finally { Destroy(instance); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void FailedMutationRollsBackAndFrozenListsSurvivePooling()
        {
            using var session = Context(1);
            var definition = Definition();
            var instance = Create(definition, session);
            try
            {
                var behaviour = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                var frozen = behaviour.CaptureState();
                Assert.Throws<InvalidOperationException>(behaviour.ChangeThenThrow);
                Assert.That(behaviour.State.Number, Is.EqualTo(11));
                bool blocked = false;
                behaviour.ObserveInSession(state =>
                {
                    if (state == null || state.Number != 20) return;
                    try { behaviour.Change(21); }
                    catch (InvalidOperationException) { blocked = true; }
                });
                behaviour.Change(20);
                Assert.That(blocked, Is.True);
                instance.Retire();
                Assert.That(frozen.Items, Is.EqualTo(new[] { 11 }));
                Assert.That(frozen.Number, Is.EqualTo(11));
            }
            finally { Destroy(instance); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void MutationOpenedBeforeReassemblyCannotCommitIntoNewLifecycle()
        {
            using var session = Context(1);
            var definition = Definition();
            var instance = Create(definition, session);
            try
            {
                var behaviour = instance.GetBehaviour<UnifiedObjectProbeBehaviour>();
                Assert.Throws<InvalidOperationException>(() => behaviour.ChangeAcrossLifecycle(() =>
                    instance.Initialize("reentry", definition, session: session, activate: false)));
                Assert.That(behaviour.State.Number, Is.EqualTo(11));
                behaviour.Change(12);
                Assert.That(behaviour.State.Number, Is.EqualTo(12));
            }
            finally { Destroy(instance); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void MissingDependenciesReleaseFailedAssemblyContainer()
        {
            var container = new DIContainer();
            container.Initialize();
            using var session = ObjectSessionContext.CreateAuthority(container, () => true);
            var definition = Definition();
            var instance = new GameObject("FailedAssembly").AddComponent<ObjectInstance>();
            int containers = DIContainer.ActiveContainers.Count;
            try
            {
                Assert.Throws<InvalidOperationException>(() => instance.Initialize("failed", definition, session: session));
                Assert.That(instance.IsAssembled, Is.False);
                Assert.That(instance.GetBehaviourCount(), Is.Zero);
                Assert.That(instance.gameObject.activeSelf, Is.False);
                Assert.That(DIContainer.ActiveContainers.Count, Is.EqualTo(containers));
            }
            finally { Destroy(instance); UnityEngine.Object.DestroyImmediate(definition); container.OnReturnToPool(); }
        }

        [Test]
        public void MissingConfigDuplicateConfigAndCapabilityFailBeforeActivation()
        {
            var definition = Definition();
            var archetype = ScriptableObject.CreateInstance<ObjectArchetype>();
            try
            {
                definition.SharedConfigs.Clear();
                Assert.Throws<InvalidOperationException>(() => ObjectAssemblyValidation.Validate(definition, null));
                definition.SharedConfigs.Add(new UnifiedObjectProbeConfig());
                definition.SharedConfigs.Add(new UnifiedObjectProbeConfig());
                Assert.Throws<InvalidOperationException>(() => ObjectAssemblyValidation.Validate(definition, null));
                definition.SharedConfigs.RemoveAt(1);
                archetype.RequiredCapabilityInterfaces.Add("System.IComparable");
                definition.Archetype = archetype;
                Assert.Throws<InvalidOperationException>(() => ObjectAssemblyValidation.Validate(definition, null));
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); UnityEngine.Object.DestroyImmediate(archetype); }
        }

        [Test]
        public void MissingGeneratedViewBindingRejectsRealWorkerPrefab()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/Worker/Worker.asset");
            var root = PrefabUtility.LoadPrefabContents("Assets/DarkNights/Res/Objects/Worker/Worker.prefab");
            try
            {
                root.GetComponent<ObjectView>().EditorSetBindings(Array.Empty<ViewComponentBinding>(), false);
                Assert.Throws<InvalidOperationException>(() => root.GetComponent<ObjectInstance>().Initialize("bad-binding", definition));
                Assert.That(root.GetComponent<ObjectInstance>().GetBehaviourCount(), Is.Zero);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void CachedRequirementsStillRejectChangedConfigsAndBindings()
        {
            var definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                "Assets/DarkNights/Res/Objects/Worker/Worker.asset"));
            var root = PrefabUtility.LoadPrefabContents("Assets/DarkNights/Res/Objects/Worker/Worker.prefab");
            try
            {
                var view = root.GetComponent<ObjectView>();
                ObjectAssemblyValidation.Validate(definition, view);
                ObjectAssemblyValidation.Validate(definition, view);
                var movement = definition.SharedConfigs.OfType<DarkNights.Runtime.Objects.MovementConfig>().Single();
                definition.SharedConfigs.Remove(movement);
                Assert.Throws<InvalidOperationException>(() => ObjectAssemblyValidation.Validate(definition, view));
                definition.SharedConfigs.Add(movement);
                ObjectAssemblyValidation.Validate(definition, view);
                var bindings = view.Bindings.ToArray();
                view.EditorSetBindings(Array.Empty<ViewComponentBinding>(), false);
                Assert.Throws<InvalidOperationException>(() => ObjectAssemblyValidation.Validate(definition, view));
                view.EditorSetBindings(bindings.Concat(bindings).ToArray(), false);
                Assert.Throws<InvalidOperationException>(() => ObjectAssemblyValidation.Validate(definition, view));
                view.EditorSetBindings(bindings, false);
                ObjectAssemblyValidation.Validate(definition, view);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); UnityEngine.Object.DestroyImmediate(definition); }
        }

        private static ObjectSessionContext Context(int number, bool authority = true)
        {
            var container = new DIContainer();
            container.Initialize();
            container.Register(new UnifiedObjectProbeDependency(number));
            var context = authority ? ObjectSessionContext.CreateAuthority(container, () => true) : ObjectSessionContext.CreateReplica(container);
            context.Lifetime.Register(container.OnReturnToPool);
            return context;
        }

        private static ObjectDefinition Definition()
        {
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.EditorSetIdentity(Guid.NewGuid().ToString("N"), "test.unified-object", true);
            definition.UseMonolithicPooling = true;
            definition.BehaviourTypes.Add(typeof(UnifiedObjectProbeBehaviour).AssemblyQualifiedName);
            definition.SharedConfigs.Add(new UnifiedObjectProbeConfig());
            return definition;
        }

        private static ObjectInstance Create(ObjectDefinition definition, ObjectSessionContext session)
        {
            var instance = new GameObject("UnifiedObjectTest").AddComponent<ObjectInstance>();
            instance.Initialize("test", definition, session: session, activate: false);
            return instance;
        }

        private static void Destroy(params ObjectInstance[] instances)
        {
            foreach (var instance in instances) { instance.Release(); UnityEngine.Object.DestroyImmediate(instance.gameObject); }
        }
    }
}
