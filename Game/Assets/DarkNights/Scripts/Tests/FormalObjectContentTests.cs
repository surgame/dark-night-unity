using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Network;
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
            Assert.That(BehaviourTypeResolver.GetFactoryFor(typeof(WorldSessionBehaviour)), Is.Not.Null);
        }

        [Test]
        public void WorkerDefinitionRoundTripsThroughFormalInitializerAndBindings()
        {
            ObjectDefinition worker = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                Editor.FormalObjectContentSetup.WorkerDefinitionPath);
            ObjectDefinitionDatabase database = ObjectDefinitionDatabase.Instance;
            database.RebuildLookup();
            var map = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                Editor.FormalObjectContentSetup.SessionDefinitionPath).SharedConfigs.OfType<ContentDefinitionMap>().Single();
            Assert.That(map.GetRequired(FormalObjectCatalog.WorkerContentId, database), Is.SameAs(worker));

            GameObject root = PrefabUtility.LoadPrefabContents(Editor.FormalObjectContentSetup.WorkerPrefabPath);
            try
            {
                ObjectView view = root.GetComponent<ObjectView>();
                ObjectDefinitionInitialization.Initialize(view.Initializer, worker);
                ObjectInstance instance = root.GetComponent<ObjectInstance>();
                Assert.That(instance.Definition, Is.SameAs(worker));
                Assert.That(instance.InstanceId, Is.EqualTo("LocalInstance_" + FormalObjectCatalog.WorkerKey));
                Assert.That(view.Get<Transform>("art_offset").name, Is.EqualTo("ArtOffset"));
                Assert.That(view.Get<Transform>("facing").name, Is.EqualTo("Facing"));
                Assert.That(view.Get<Transform>("status_anchor").name, Is.EqualTo("StatusAnchor"));
                Assert.That(view.Get<Transform>("selection_anchor").name, Is.EqualTo("SelectionAnchor"));
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
                LogAssert.Expect(
                    LogType.Error,
                    "[BehaviourUpdateManager] Register failed because ObjectV2 runtime has not created BehaviourUpdateManager.");
                instance.Initialize("editor-session-contract", session, root.GetComponent<StateSynchronizer>());
                Assert.That(instance.GetBehaviour<WorldSessionBehaviour>(), Is.Not.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
