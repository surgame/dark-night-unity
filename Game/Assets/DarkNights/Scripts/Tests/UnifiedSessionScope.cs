using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>
    /// 为原会话断言提供真实 YYGC 装配范围：一次预加载正式 Prefab，再同步创建独立对象会话。
    /// 只持有测试租约与清理清单；没有模拟状态替身，所有规则和恢复由正式 Behaviour 执行。
    /// </summary>
    public sealed class UnifiedSessionScope : IDisposable
    {
        private readonly List<(ObjectSession World, GameObject Root)> worlds = new List<(ObjectSession, GameObject)>();
        private ObjectSessionResources resources;
        private GameObject updates;
        public static UnifiedSessionScope Current { get; private set; }

        public static async UniTask<UnifiedSessionScope> Create()
        {
            if (Current != null) throw new InvalidOperationException("A real-object test scope is already active.");
            using var loading = new EditorAssetLoading();
            var scope = new UnifiedSessionScope();
            try
            {
                RuleScenario.RepositoryRoot = Path.GetFullPath("..");
                if (BehaviourUpdateManager.Instance == null)
                {
                    scope.updates = new GameObject("YYGC integration test updates");
                    var manager = scope.updates.AddComponent<BehaviourUpdateManager>();
                    typeof(BehaviourUpdateManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
                }
                GameCatalog catalog = RuleScenario.Catalog();
                var directory = new DefinitionRuleIndex(ObjectDefinitionDatabase.Instance);
                string[] keys = catalog.Balance.Units.Keys.Concat(catalog.Balance.Buildings.Keys).Concat(catalog.Balance.Worksites.Keys).ToArray();
                scope.resources = await ObjectSessionResources.Prepare(keys.Select(directory.GetRequired).ToArray(), loading.CancellationToken);
                Current = scope;
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        private ObjectPlacement[] Placements(LevelLayout layout) => layout.Buildings.Concat(layout.Worksites).Concat(layout.Actors)
            .Select((p, i) => new ObjectPlacement("scenario-placement-" + i, resources.Find(p.Kind), p.X, p.Variant, p.Name, null)).ToArray();

        public ObjectWorldSaveJson Codec(GameCatalog catalog, LevelLayout layout) => new ObjectWorldSaveJson(catalog, layout,
            resources.Definitions.ToDictionary(ObjectSessionResources.Rule, d => d.Guid.ToString()),
            Placements(layout).ToDictionary(p => p.PlacementKey, p => ObjectSessionResources.Rule(p.Definition)));

        public ObjectSession NewWorld(GameCatalog catalog, LevelLayout layout, bool activate = true,
            float debugHeroSpeedMultiplier = 1)
        {
            var world = new ObjectSession(catalog, layout, resources, () => true,
                debugHeroSpeedMultiplier: debugHeroSpeedMultiplier);
            GameObject root = null;
            try
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Editor.FormalObjectContentSetup.SessionPrefabPath));
                var instance = root.GetComponent<ObjectInstance>();
                instance.Initialize("integration-session", ObjectDefinitionDatabase.Instance.GetDefinitionByKey(FormalObjectCatalog.SessionKey),
                    session: world.Context, activate: false);
                world.Prepare(instance, Placements(layout));
                if (activate) world.Activate();
                worlds.Add((world, root));
                return world;
            }
            catch
            {
                world.Dispose();
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public SessionAuthority NewAuthority(GameCatalog catalog, LevelLayout layout) => new SessionAuthority(NewWorld(catalog, layout, false));

        public void Dispose()
        {
            foreach (var entry in worlds)
            {
                entry.World.Dispose();
                if (entry.Root != null) UnityEngine.Object.DestroyImmediate(entry.Root);
            }
            worlds.Clear();
            resources?.Dispose();
            if (updates != null)
            {
                typeof(BehaviourUpdateManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(updates.GetComponent<BehaviourUpdateManager>(), null);
                UnityEngine.Object.DestroyImmediate(updates);
            }
            if (ReferenceEquals(Current, this)) Current = null;
        }
    }
}
