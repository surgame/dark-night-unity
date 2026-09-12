using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Network;
using FishNet.Managing.Object;
using FishNet.Object;
using GameCore.Editor.NetworkCommands;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using GameCore.Objects.Views;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 在两个指定空目录中一次性创建首批正式 YYGC 定义和原生 Prefab。
    /// 会话立即配置完整生命周期；Worker 骨架等待原生素材安装后追加表现行为与 visual 绑定。
    /// 日常调用仅验证身份、绑定与生成注册，不覆盖人工维护的资产。
    /// </summary>
    public static class FormalObjectContentSetup
    {
        public const string SessionRoot = "Assets/DarkNights/Res/Objects/WorldSession";
        public const string WorkerRoot = "Assets/DarkNights/Res/Objects/Worker";
        public const string SessionDefinitionPath = SessionRoot + "/WorldSession.asset";
        public const string SessionPrefabPath = SessionRoot + "/WorldSession.prefab";
        public const string WorkerDefinitionPath = WorkerRoot + "/Worker.asset";
        public const string WorkerPrefabPath = WorkerRoot + "/Worker.prefab";
        public const string SessionPrefabAddress = "dark_nights.object.world_session";
        public const string WorkerPrefabAddress = "dark_nights.object.worker";
        private const string DatabasePath = "Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset";
        private const string SpawnablePrefabsPath = "Assets/DefaultPrefabObjects.asset";
        private const string CommandRegistryPath = "Assets/Addressables/Datas/GlobalSO/NetworkCommandInterfaceGenerateRegistry.asset";
        private const string StateRegistryPath = "Assets/Addressables/Datas/GlobalSO/StateDataRegistry.asset";
        private const string GeneratedCommandPath = "Assets/Scripts/Generated/INetworkCommand.generated.cs";
        private const string GeneratedStatePath = "Assets/Scripts/Generated/IStateData.generated.cs";

        [MenuItem("Dark Nights/Content/Create Initial Formal Objects")]
        public static void CreateInitial()
        {
            EnvironmentValidation.Validate();
            RequireEmpty(SessionRoot);
            RequireEmpty(WorkerRoot);
            EnsureFolder(SessionRoot);
            EnsureFolder(WorkerRoot);

            GameObject workerPrefab = CreateWorkerPrefab();
            GameObject sessionPrefab = CreateSessionPrefab();
            ObjectDefinitionDatabase database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>(DatabasePath);
            database.EditorConfigure(DefinitionIdentityMode.GuidFirst, false);
            database.EditorFreezeProjectName();
            ObjectDefinition worker = CreateDefinition(WorkerDefinitionPath, "工人",
                FormalObjectCatalog.WorkerKey, NetworkType.Local, workerPrefab);
            ObjectDefinition session = CreateDefinition(SessionDefinitionPath, "灰松谷会话",
                FormalObjectCatalog.SessionKey, NetworkType.Network, sessionPrefab);
            session.BehaviourTypes.Add(typeof(WorldSessionBehaviour).FullName);
            session.BehaviourTypes.Add(typeof(CampSessionBehaviour).FullName);
            session.SharedConfigs.Add(new ContentDefinitionMap(new[]
            {
                new ContentDefinitionEntry(FormalObjectCatalog.WorkerContentId,
                    new DefinitionReference(worker.Guid))
            }));
            EditorUtility.SetDirty(session);
            database.AddDefinition(worker);
            database.AddDefinition(session);

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            RegisterAddress(settings, WorkerPrefabPath, WorkerPrefabAddress);
            RegisterAddress(settings, SessionPrefabPath, SessionPrefabAddress);
            DefaultPrefabObjects spawnables = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(SpawnablePrefabsPath);
            spawnables.AddObject(sessionPrefab.GetComponent<NetworkObject>(), true);
            EditorUtility.SetDirty(spawnables);

            AssetDatabase.SaveAssetIfDirty(worker);
            AssetDatabase.SaveAssetIfDirty(session);
            AssetDatabase.SaveAssetIfDirty(database);
            AssetDatabase.SaveAssetIfDirty(spawnables);
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings.DefaultGroup);
            ValidateContent(false);
            Debug.Log("DARK_NIGHTS_FORMAL_OBJECTS_CREATED definitions=2 prefabs=2 bindings=4");
        }

        public static void Validate() => ValidateContent(true);

        private static void ValidateContent(bool requireNativeArt)
        {
            ObjectDefinitionDatabase database = Required<ObjectDefinitionDatabase>(DatabasePath);
            database.RebuildLookup();
            if (database.IdentityMode != DefinitionIdentityMode.GuidFirst || database.EnableOnlineIdService ||
                database.LegacyIdMap != null || DefinitionNetworkProfile.WireVersion != 2 ||
                !string.Equals(database.ProjectName, FormalObjectCatalog.RegistryProject, StringComparison.Ordinal))
                throw new InvalidOperationException("Definition identity settings must be frozen to GuidFirst/GuidV2.");
            ObjectDefinition worker = Required<ObjectDefinition>(WorkerDefinitionPath);
            ObjectDefinition session = Required<ObjectDefinition>(SessionDefinitionPath);
            GameObject workerPrefab = Required<GameObject>(WorkerPrefabPath);
            GameObject sessionPrefab = Required<GameObject>(SessionPrefabPath);

            foreach (ObjectDefinition definition in database.Definitions)
            {
                if (definition == null || definition.Id != 0 || definition.LegacyIdAliases.Count != 0)
                    throw new InvalidOperationException("Formal definitions must not contain legacy integer identities.");
            }
            ValidateIdentity(worker, FormalObjectCatalog.WorkerKey, NetworkType.Local, WorkerPrefabPath);
            ValidateIdentity(session, FormalObjectCatalog.SessionKey, NetworkType.Network, SessionPrefabPath);
            if (database.GetDefinitionByKey(worker.Key) != worker || database.GetDefinitionByKey(session.Key) != session)
                throw new InvalidOperationException("Formal definitions are not indexed by the ObjectDefinitionDatabase.");
            CRefactorContentUpgrade.ValidateSession(session);
            ContentDefinitionMap map = session.SharedConfigs.OfType<ContentDefinitionMap>().SingleOrDefault();
            if (map == null || map.GetRequired(FormalObjectCatalog.WorkerContentId, database) != worker)
                throw new InvalidOperationException("Worker ContentId mapping is missing or incorrect.");

            ValidateWorkerPrefab(workerPrefab, requireNativeArt);
            if (requireNativeArt) CRefactorContentUpgrade.ValidateLocal("Worker", worker, workerPrefab);
            ValidateSessionPrefab(sessionPrefab);
            EnvironmentValidation.RequireAddress(WorkerPrefabPath, WorkerPrefabAddress);
            EnvironmentValidation.RequireAddress(SessionPrefabPath, SessionPrefabAddress);
            ValidateGeneratedContracts();
        }

        private static GameObject CreateWorkerPrefab()
        {
            var root = new GameObject("Worker");
            try
            {
                ObjectInstance instance = root.AddComponent<ObjectInstance>();
                LocalObjectInstanceInitializer initializer = root.AddComponent<LocalObjectInstanceInitializer>();
                ObjectView view = root.AddComponent<ObjectView>();
                Transform art = Child(root.transform, "ArtOffset");
                Transform facing = Child(art, "Facing");
                Transform status = Child(root.transform, "StatusAnchor");
                Transform selection = Child(root.transform, "SelectionAnchor");
                view.EditorSetBindings(new[]
                {
                    new ViewComponentBinding("art_offset", art),
                    new ViewComponentBinding("facing", facing),
                    new ViewComponentBinding("status_anchor", status),
                    new ViewComponentBinding("selection_anchor", selection)
                }, false);
                SetReference(instance, "_view", view);
                SetReference(initializer, "_objectInstance", instance);
                view.ForceRefreshAllReferences();
                return PrefabUtility.SaveAsPrefabAsset(root, WorkerPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject CreateSessionPrefab()
        {
            var root = new GameObject("WorldSession");
            try
            {
                root.AddComponent<NetworkObject>();
                ObjectInstance instance = root.AddComponent<ObjectInstance>();
                StateSynchronizer synchronizer = root.AddComponent<StateSynchronizer>();
                ObjectView view = root.AddComponent<ObjectView>();
                SessionObjectLink link = root.AddComponent<SessionObjectLink>();
                SetReference(instance, "_view", view);
                SetReference(synchronizer, "_objectInstance", instance);
                SetReference(link, "instance", instance);
                SetReference(link, "synchronizer", synchronizer);
                view.ForceRefreshAllReferences();
                return PrefabUtility.SaveAsPrefabAsset(root, SessionPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static ObjectDefinition CreateDefinition(string path, string displayName,
            string key, NetworkType networkType, GameObject prefab)
        {
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Name = displayName;
            definition.NetType = networkType;
            definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)));
            AssetDatabase.CreateAsset(definition, path);
            string assetGuid = DefinitionIdentityAuthoring.ReadAssetGuid(definition);
            definition.EditorSetIdentity(assetGuid, key, false);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ValidateIdentity(ObjectDefinition definition, string key,
            NetworkType networkType, string prefabPath)
        {
            string assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(definition));
            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (definition.GuidString != assetGuid || definition.Key != key || definition.Id != 0 ||
                definition.LegacyIdAliases.Count != 0 || definition.NetType != networkType ||
                definition.PrefabRef.AssetGUID != prefabGuid)
                throw new InvalidOperationException("Definition identity or PrefabRef is incorrect: " + definition.name);
        }

        private static void ValidateWorkerPrefab(GameObject prefab, bool requireNativeArt)
        {
            ObjectInstance instance = prefab.GetComponent<ObjectInstance>();
            ObjectView view = prefab.GetComponent<ObjectView>();
            LocalObjectInstanceInitializer initializer = prefab.GetComponent<LocalObjectInstanceInitializer>();
            if (instance == null || view == null || initializer == null || instance.ObjectView != view ||
                initializer.ObjectInstance != instance || view.Initializer != initializer)
                throw new InvalidOperationException("Worker ObjectInstance/ObjectView/initializer binding is invalid.");
            string[] keys = { "art_offset", "facing", "status_anchor", "selection_anchor" };
            int expectedCount = keys.Length + (requireNativeArt ? 1 : 0);
            if (view.Bindings.Count != expectedCount || keys.Any(key => view.Get<Transform>(key) == null) ||
                (requireNativeArt && view.Get<DarkNights.View.NativeVisual>("visual") == null))
                throw new InvalidOperationException("Worker generated component binding table is incomplete.");
        }

        private static void ValidateSessionPrefab(GameObject prefab)
        {
            CRefactorContentUpgrade.ValidateSessionLink(prefab);
            NetworkObject network = prefab.GetComponent<NetworkObject>();
            ObjectInstance instance = prefab.GetComponent<ObjectInstance>();
            ObjectView view = prefab.GetComponent<ObjectView>();
            StateSynchronizer synchronizer = prefab.GetComponent<StateSynchronizer>();
            DefaultPrefabObjects spawnables = Required<DefaultPrefabObjects>(SpawnablePrefabsPath);
            if (network == null || instance == null || view == null || synchronizer == null ||
                instance.ObjectView != view || view.Initializer != synchronizer || network.AssetPathHash == 0 ||
                spawnables.Prefabs.Count(item => item == network) != 1)
                throw new InvalidOperationException("WorldSession Prefab assembly or FishNet registration is invalid.");
        }

        private static void ValidateGeneratedContracts()
        {
            MonoScript command = Required<MonoScript>("Assets/DarkNights/Scripts/Runtime/Network/SetReadyCommand.cs");
            MonoScript state = Required<MonoScript>("Assets/DarkNights/Scripts/Runtime/Network/SessionStatusState.cs");
            var commandRegistry = Required<NetworkCommandInterfaceGenerateRegistry>(CommandRegistryPath);
            var stateRegistry = Required<StateDataRegistry>(StateRegistryPath);
            var commandEntry = commandRegistry.Registrations.SingleOrDefault(value => value.CommandType == command);
            var stateEntry = stateRegistry.Registrations.SingleOrDefault(value => value.StateType == state);
            if (commandEntry == null || stateEntry == null ||
                !File.ReadAllText(GeneratedCommandPath).Contains("Register<global::" + typeof(SetReadyCommand).FullName + ">(" + commandEntry.Tag + ")") ||
                !File.ReadAllText(GeneratedStatePath).Contains("Register<global::" + typeof(SessionStatusState).FullName + ">(" + stateEntry.Tag + ")"))
                throw new InvalidOperationException("Formal network contract registry source is stale or missing.");
        }

        private static void RegisterAddress(AddressableAssetSettings settings, string path, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            foreach (AddressableAssetGroup group in settings.groups)
                if (group != null && group.entries.Any(value => value.address == address && value.guid != guid))
                    throw new InvalidOperationException("Address already belongs to another asset: " + address);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, true);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            return value != null ? value : throw new InvalidOperationException("Required asset is missing: " + path);
        }

        private static void RequireEmpty(string path)
        {
            if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length != 0)
                throw new InvalidOperationException("Initial formal object output must be empty: " + path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
