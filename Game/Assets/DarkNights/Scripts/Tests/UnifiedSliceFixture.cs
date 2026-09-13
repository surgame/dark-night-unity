using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Behaviours;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>
    /// 用真实正式 Prefab、Addressables 租约和 YYGC 装配创建 U2 回归会话。
    /// 仅 House 定义使用可丢弃内存副本供失败注入；清理先释放对象再归还资源。
    /// </summary>
    public sealed class UnifiedSliceFixture : IDisposable
    {
        private GameObject root;
        private GameObject sceneWorker;
        private ObjectDefinition house;
        private ObjectDefinition archer;
        private GameObject updateRoot;
        public ObjectSessionResources Resources { get; private set; }
        public ObjectSession World { get; private set; }
        public SessionAuthority Authority { get; private set; }
        public SessionConnection Host { get; private set; }
        public SessionConnection Client { get; private set; }
        public ObjectPlacement[] Placements { get; private set; }
        public ObjectInstance SceneWorker => sceneWorker == null ? null : sceneWorker.GetComponent<ObjectInstance>();

        public static async UniTask<UnifiedSliceFixture> Create(bool withSceneWorker = false, bool full = false)
        {
            var result = new UnifiedSliceFixture();
            try
            {
                RuleScenario.RepositoryRoot = Path.GetFullPath("..");
                GameCatalog catalog = RuleScenario.Catalog();
                LevelLayout layout = RuleScenario.Layout();
                var directory = new DefinitionRuleIndex(ObjectDefinitionDatabase.Instance);
                result.house = UnityEngine.Object.Instantiate(directory.GetRequired("house"));
                if (full)
                {
                    result.archer = UnityEngine.Object.Instantiate(directory.GetRequired("archer"));
                    if (BehaviourUpdateManager.Instance == null)
                    {
                        result.updateRoot = new GameObject("U3 real YYGC update lifecycle");
                        var updates = result.updateRoot.AddComponent<BehaviourUpdateManager>();
                        typeof(BehaviourUpdateManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(updates, null);
                    }
                }
                string[] keys = full ? catalog.Balance.Buildings.Keys.Concat(catalog.Balance.Worksites.Keys)
                    .Concat(catalog.Balance.Units.Keys).ToArray() : new[] { "tavern", "house", "wood", "worker" };
                result.Resources = await ObjectSessionResources.Prepare(keys.Select(key =>
                    key == "house" ? result.house : key == "archer" && full ? result.archer : directory.GetRequired(key)).ToArray(), default);
                result.World = new ObjectSession(catalog, layout, result.Resources, () => true);
                result.root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    Editor.FormalObjectContentSetup.SessionPrefabPath));
                var instance = result.root.GetComponent<ObjectInstance>();
                instance.Initialize("u2-fixture", ObjectDefinitionDatabase.Instance.GetDefinitionByKey(FormalObjectCatalog.SessionKey),
                    session: result.World.Context, activate: false);
                ObjectDefinitionLoader loader = null;
                if (withSceneWorker)
                {
                    result.sceneWorker = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                        Editor.FormalObjectContentSetup.WorkerPrefabPath));
                    loader = result.sceneWorker.AddComponent<ObjectDefinitionLoader>();
                    loader.EditorConfigure(directory.GetRequired("worker"));
                }
                var records = layout.Buildings.Concat(layout.Worksites).Concat(layout.Actors)
                    .Where(p => full || p.Kind == "house" || p.Kind == "tavern" || p.Kind == "wood" || p.Kind == "worker").ToArray();
                bool assignedScene = false;
                var placements = records.Select((p, index) =>
                {
                    ObjectDefinitionLoader current = p.Kind == "worker" && !assignedScene ? loader : null;
                    if (current != null) assignedScene = true;
                    return new ObjectPlacement("u2-placement-" + index, result.Resources.Find(p.Kind), p.X,
                        p.Variant, p.Name, current);
                }).ToArray();
                result.Placements = placements;
                result.World.Prepare(instance, placements);
                ExpectActivation(result.World);
                result.Authority = new SessionAuthority(result.World);
                result.Host = result.Authority.Connect(0);
                result.Client = result.Authority.Connect(1);
                result.Ready();
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        public void Ready()
        {
            Authority.AcknowledgeReady(Host, Authority.Epoch, Authority.Revision);
            Authority.AcknowledgeReady(Client, Authority.Epoch, Authority.Revision);
        }

        public static void ExpectActivation(ObjectSession session)
        {
            int count = session.Index.FreezeOrder().Sum(e => e.Object.GetBehaviourCount()) + session.Camp.Object.GetBehaviourCount();
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(count);
        }

        public void ExpectRestoreActivation()
        {
            FormalObjectContentTests.ExpectRegistrationWithoutRuntime(World.Index.FreezeOrder().Sum(e => e.Object.GetBehaviourCount()));
        }

        public SessionRequest Request(SessionOperation operation, long sequence, int[] ids = null,
            int target = 0, float x = 0, string kind = "", int value = 0, int? policy = null) =>
            new SessionRequest(operation, SessionAuthority.ProtocolVersion, Authority.Epoch,
                policy ?? Authority.PolicyRevision, sequence, ids, target, x, kind, value);

        public void Step(double seconds)
        {
            for (int index = 0; index < (int)Math.Round(seconds * 60); index++) Authority.Tick();
        }

        public void BreakHouse() => house.BehaviourTypes.Add("Missing.U2.Capability");
        public void RepairHouse() => house.BehaviourTypes.Remove("Missing.U2.Capability");
        public void BreakArcher() => archer.BehaviourTypes.Add("Missing.U3.Capability");
        public void RepairArcher() => archer.BehaviourTypes.Remove("Missing.U3.Capability");

        public void Dispose()
        {
            if (Authority != null) Authority.Dispose();
            else World?.Dispose();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (sceneWorker != null) UnityEngine.Object.DestroyImmediate(sceneWorker);
            Resources?.Dispose();
            if (house != null) UnityEngine.Object.DestroyImmediate(house);
            if (archer != null) UnityEngine.Object.DestroyImmediate(archer);
            if (updateRoot != null)
            {
                typeof(BehaviourUpdateManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(updateRoot.GetComponent<BehaviourUpdateManager>(), null);
                UnityEngine.Object.DestroyImmediate(updateRoot);
            }
        }
    }
}
