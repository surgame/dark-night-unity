using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Network;
using DarkNights.View;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YY.Features.Players.View;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证首批正式定义的编辑器往返、生成行为工厂与本地对象装配。
    /// 测试只加载既有资产，不重建定义、Prefab、Addressables 或冻结注册号。
    /// </summary>
    public sealed class FormalObjectContentTests
    {
        [Test]
        public void FormalDefinitionsAndGeneratedRegistriesAreComplete()
        {
            Editor.FormalObjectContentSetup.Validate();
            Editor.CRefactorContentUpgrade.Validate();
            foreach (var type in new[] { typeof(WorldSessionBehaviour), typeof(CampSessionBehaviour),
                typeof(ActorPresentationBehaviour), typeof(BuildingPresentationBehaviour), typeof(WorksitePresentationBehaviour) })
                Editor.CRefactorContentUpgrade.RequireGenerated(type);
            Assert.That(BehaviourTypeResolver.Factories.ContainsKey(typeof(EntityPresentationBehaviour)), Is.False);
        }

        [Test]
        public void WorkerDefinitionRoundTripsThroughFormalInitializerAndBindings()
        {
            ObjectDefinition worker = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                Editor.FormalObjectContentSetup.WorkerDefinitionPath);
            ObjectDefinitionDatabase database = ObjectDefinitionDatabase.Instance;
            database.RebuildLookup();
            var map = new DefinitionRuleIndex(database);
            Assert.That(map.GetRequired(FormalObjectCatalog.WorkerContentId), Is.SameAs(worker));

            GameObject root = PrefabUtility.LoadPrefabContents(Editor.FormalObjectContentSetup.WorkerPrefabPath);
            try
            {
                ObjectView view = root.GetComponent<ObjectView>();
                ExpectRegistrationWithoutRuntime(worker.BehaviourTypes.Count);
                ObjectDefinitionInitialization.Initialize(view.Initializer, worker);
                ObjectInstance instance = root.GetComponent<ObjectInstance>();
                Assert.That(instance.Definition, Is.SameAs(worker));
                Assert.That(instance.InstanceId, Is.EqualTo("LocalInstance_" + FormalObjectCatalog.WorkerKey));
                Assert.That(view.Get<Transform>("art_offset").name, Is.EqualTo("ArtOffset"));
                Assert.That(view.Get<Transform>("facing").name, Is.EqualTo("Facing"));
                Assert.That(view.Get<Transform>("status_anchor").name, Is.EqualTo("StatusAnchor"));
                Assert.That(view.Get<Transform>("selection_anchor").name, Is.EqualTo("SelectionAnchor"));
                var presentation = instance.GetBehaviour<ActorPresentationBehaviour>();
                Assert.That(presentation, Is.Not.Null);
                Assert.That(presentation.Visual, Is.SameAs(view.Get<NativeVisual>("visual")));
                Assert.That(presentation.IsBound, Is.False);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [Test]
        public void SessionDefinitionCreatesItsGeneratedBehaviour()
        {
            ObjectDefinition session = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                Editor.FormalObjectContentSetup.SessionDefinitionPath);
            GameObject root = PrefabUtility.LoadPrefabContents(Editor.FormalObjectContentSetup.SessionPrefabPath);
            try
            {
                ObjectInstance instance = root.GetComponent<ObjectInstance>();
                ExpectRegistrationWithoutRuntime(session.BehaviourTypes.Count);
                instance.Initialize("editor-session-contract", session, root.GetComponent<StateSynchronizer>());
                Assert.That(instance.GetBehaviour<WorldSessionBehaviour>(), Is.Not.Null);
                Assert.That(instance.GetBehaviour<CampSessionBehaviour>(), Is.Not.Null);
                Assert.That(instance.GetAllBehaviors().OfType<IStatefulBehaviour>()
                    .Count(b => b.NetworkMode != SyncMode.Session), Is.EqualTo(1));
                var link = root.GetComponent<SessionObjectLink>();
                Assert.That(link.Instance, Is.SameAs(instance));
                Assert.That(link.Synchronizer, Is.SameAs(root.GetComponent<StateSynchronizer>()));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static void ExpectRegistrationWithoutRuntime(int count)
        {
            if (BehaviourUpdateManager.Instance != null) return;
            for (int i = 0; i < count; i++)
                LogAssert.Expect(LogType.Error,
                    "[BehaviourUpdateManager] Register failed because ObjectV2 runtime has not created BehaviourUpdateManager.");
        }
    }
}
