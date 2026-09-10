using System;
using System.IO;
using System.Linq;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using GameCore.Objects.Singletons;
using GameCore.Objects.Types;
using GameCore.UI.UGUI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YY.Features.Players.View;
#if !YYGC_IDENTITY_BASELINE
using GameCore.Editor.Objects.Definition;
#endif

namespace YYGC.IdentityValidation.Editor
{
    /// <summary>只在隔离宿主的空目录创建受控夹具；构建真实非 Development Player，不覆盖冻结旧夹具。</summary>
    public static class IdentityPlayerBuild
    {
        public const string Root = "Assets/IdentityPlayerProbe";
        private const string ScenePath = Root + "/probe.unity";

        public static void ConfigureV1() => SetSymbols(false, false);
        public static void ConfigureV2() => SetSymbols(true, false);
        public static void ConfigureBaseline() => SetSymbols(false, true);
        public static void ConfigureWarnings() => SetSymbols(false, false, true);
        public static void PrepareLegacyComparison()
        {
#if !YYGC_IDENTITY_BASELINE
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var probe = UnityEngine.Object.FindFirstObjectByType<IdentityPlayerProbe>();
            probe.Network.Id = 1001;
            EditorUtility.SetDirty(probe.Network);
            AssetDatabase.SaveAssetIfDirty(probe.Network);
            var loader = new SerializedObject(probe.SceneObject.GetComponent<ObjectDefinitionLoader>());
            loader.FindProperty("_definitionId").intValue = 1001;
            loader.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(probe.SceneObject.GetComponent<ObjectDefinitionLoader>());
            EditorSceneManager.SaveScene(scene);
            probe.Database.EditorConfigure(DefinitionIdentityMode.LegacyCompatible, false);
            AssetDatabase.SaveAssetIfDirty(probe.Database);
            SetSymbols(false, true);
#endif
        }
        public static void PrepareAndBuild() { Prepare(); Build(); }

        /// <summary>补全尚未验收的 Player 测试输入；只允许空配置，保留原有资产 GUID 和冻结旧夹具。</summary>
        public static void CompleteProbeConfiguration()
        {
#if !YYGC_IDENTITY_BASELINE
            string[] names = { "local-a", "local-b" };
            for (int index = 0; index < names.Length; index++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(Root + "/" + names[index] + ".asset");
                if (definition == null) throw new InvalidOperationException("缺少待验证定义。");
                int expected = (index + 1) * 11;
                if (definition.SharedConfigs.Count == 0)
                {
                    definition.SharedConfigs.Add(new IdentityProbeConfig { Marker = expected });
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                }
                if (!(definition.SharedConfigs.Single() is IdentityProbeConfig config) || config.Marker != expected)
                    throw new InvalidOperationException("测试输入与固定期望不符，拒绝覆盖。");
            }
#endif
        }

        public static void CompleteAndBuild() { CompleteProbeConfiguration(); Build(); }

        private static void SetSymbols(bool guid, bool baseline, bool warnings = false)
        {
            string[] old = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';');
            var next = old.Where(s => s != "YYGC_GUID_DEFINITION_WIRE_V2" && s != "YYGC_IDENTITY_BASELINE" &&
                s != "YYGC_LEGACY_ID_WARNINGS").Where(s => s.Length > 0).ToList();
            if (guid) next.Add("YYGC_GUID_DEFINITION_WIRE_V2");
            if (baseline) next.Add("YYGC_IDENTITY_BASELINE");
            if (warnings) next.Add("YYGC_LEGACY_ID_WARNINGS");
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, string.Join(";", next));
        }

        public static void Prepare()
        {
#if YYGC_IDENTITY_BASELINE
            throw new InvalidOperationException("旧基线使用预先冻结的同一验证夹具，不生成新期望值。");
#else
            if (Directory.Exists(Root)) throw new InvalidOperationException("验证夹具已存在，拒绝覆盖。");
            AssetDatabase.CreateFolder("Assets", "IdentityPlayerProbe");
            var database = ScriptableObject.CreateInstance<ObjectDefinitionDatabase>();
            AssetDatabase.CreateAsset(database, Root + "/catalog.asset");
            database.EditorConfigure(DefinitionIdentityMode.LegacyCompatible, false);
            var localPrefab = CreatePrefab("local", false, false);
            var uiPrefab = CreatePrefab("ui", false, true);
            var networkPrefab = CreatePrefab("network", true, false);
            var first = CreateDefinition(database, "local-a", 0, ObjectType.Resource_Raw, localPrefab);
            var second = CreateDefinition(database, "local-b", 0, ObjectType.Resource_Raw, localPrefab);
            foreach (var definition in new[] { first, second })
            {
                definition.UseMonolithicPooling = true;
                definition.BehaviourTypes.Add(typeof(IdentityLocalBehaviour).AssemblyQualifiedName);
                definition.SharedConfigs.Add(new IdentityProbeConfig { Marker = definition == first ? 11 : 22 });
            }
            var ui = CreateDefinition(database, "ui", 0, ObjectType.UI_Panel, uiPrefab);
            var network = CreateDefinition(database, "network", 1001, ObjectType.Resource_Raw, networkPrefab);
            network.NetType = NetworkType.Network;
            network.BehaviourTypes.Add(typeof(IdentityStateBehaviour).AssemblyQualifiedName);
            var singleton = CreateDefinition(database, "singleton", 10001, ObjectType.SingletonUtility, null);
            singleton.BehaviourTypes.Add(typeof(IdentityProbeSingleton).AssemblyQualifiedName);
            var singletons = ScriptableObject.CreateInstance<ObjectSingletonDatabase>();
            singletons.SingletonRecords.Add(new SingletonRecord { BehaviourTypeName = typeof(IdentityProbeSingleton).AssemblyQualifiedName,
                DefinitionId = singleton.Id, DefinitionGuid = singleton.GuidString, IsEnabled = false });
            singletons.SingletonRecords.Add(new SingletonRecord { BehaviourTypeName = typeof(IdentityProbeSingleton).AssemblyQualifiedName,
                DefinitionId = singleton.Id, DefinitionGuid = singleton.GuidString, IsEnabled = true });
            AssetDatabase.CreateAsset(singletons, Root + "/singletons.asset");
            var prefabs = ScriptableObject.CreateInstance<SinglePrefabObjects>();
            prefabs.AddObject(networkPrefab.GetComponent<NetworkObject>());
            AssetDatabase.CreateAsset(prefabs, Root + "/network-prefabs.asset");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var probe = new GameObject("IdentityPlayerProbe").AddComponent<IdentityPlayerProbe>();
            probe.Database = database;
            probe.Singletons = singletons;
            probe.LocalA = first; probe.LocalB = second; probe.Ui = ui; probe.Network = network;
            var managerRoot = new GameObject("IdentityNetworkManager");
            probe.Manager = managerRoot.AddComponent<NetworkManager>();
            probe.Manager.SpawnablePrefabs = prefabs;
            managerRoot.AddComponent<FishNet.Managing.Server.ServerManager>().SetStartOnHeadless(false);
            var sceneObject = (GameObject)PrefabUtility.InstantiatePrefab(networkPrefab);
            sceneObject.name = "SceneNetwork";
            probe.SceneObject = sceneObject.GetComponent<StateSynchronizer>();
            var loader = sceneObject.AddComponent<ObjectDefinitionLoader>();
            var serialized = new SerializedObject(loader);
            serialized.FindProperty("_definitionId").intValue = network.Id;
            serialized.FindProperty("_definitionGuid").stringValue = network.GuidString;
            serialized.FindProperty("_initializerComponent").objectReferenceValue = probe.SceneObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var definition in database.Definitions)
            {
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }
            AssetDatabase.SaveAssetIfDirty(database);
            AssetDatabase.SaveAssetIfDirty(singletons);
            AssetDatabase.SaveAssetIfDirty(prefabs);
            EditorSceneManager.SaveScene(scene, ScenePath);
            probe.SceneObject.GetComponent<NetworkObject>().SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            foreach (var prefab in new[] { localPrefab, uiPrefab, networkPrefab })
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
                settings.CreateOrMoveEntry(guid, settings.DefaultGroup).address = "identity-" + prefab.name;
            }
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            foreach (var group in settings.groups.Where(g => g != null)) AssetDatabase.SaveAssetIfDirty(group);
            Debug.Log("IDENTITY_PLAYER_FIXTURES_CREATED");
#endif
        }

        public static void Build()
        {
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("先在空目录执行 Prepare。");
            string output = IdentityProbeReport.Argument("-identityBuild", "../artifacts/player-v1/IdentityProbe.exe");
            var previousScenes = EditorBuildSettings.scenes;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var sceneProbe = UnityEngine.Object.FindFirstObjectByType<IdentityPlayerProbe>();
            sceneProbe.Manager.gameObject.SetActive(true);
            var serverSettings = sceneProbe.Manager.GetComponent<FishNet.Managing.Server.ServerManager>();
            if (serverSettings == null) serverSettings = sceneProbe.Manager.gameObject.AddComponent<FishNet.Managing.Server.ServerManager>();
            serverSettings.SetStartOnHeadless(false);
            sceneProbe.SceneObject.GetComponent<NetworkObject>().SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
#if !YYGC_IDENTITY_BASELINE
            var probe = UnityEngine.Object.FindFirstObjectByType<IdentityPlayerProbe>();
            int networkId = DefinitionNetworkProfile.WireVersion == 2 ? 0 : 1001;
            var definitionData = new SerializedObject(probe.Network);
            definitionData.FindProperty("Id").intValue = networkId;
            definitionData.FindProperty("legacyIdAliases").ClearArray();
            definitionData.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(probe.Network);
            var loader = new SerializedObject(probe.SceneObject.GetComponent<ObjectDefinitionLoader>());
            loader.FindProperty("_definitionId").intValue = networkId;
            loader.FindProperty("_definitionGuid").stringValue = probe.Network.GuidString;
            loader.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(probe.SceneObject.GetComponent<ObjectDefinitionLoader>());
            EditorSceneManager.SaveScene(scene);
            probe.Database.EditorConfigure(DefinitionNetworkProfile.WireVersion == 2 ? DefinitionIdentityMode.GuidFirst : DefinitionIdentityMode.LegacyCompatible, false);
            AssetDatabase.SaveAssetIfDirty(probe.Database);
#endif
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                EditorUserBuildSettings.development = false;
                AddressableAssetSettingsDefaultObject.Settings.BuildAddressablesWithPlayerBuild =
                    UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult content);
                if (!string.IsNullOrEmpty(content.Error)) throw new InvalidOperationException(content.Error);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },
                    locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Player 构建失败：" + report.summary.result);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build.json"), JsonUtility.ToJson(new BuildEvidence
                { unityVersion = Application.unityVersion, result = report.summary.result.ToString(), development = false,
                    backend = "Mono", bytes = report.summary.totalSize, errors = report.summary.totalErrors }, true));
                Debug.Log("IDENTITY_PLAYER_BUILD_PASSED " + output);
            }
            finally { EditorBuildSettings.scenes = previousScenes; }
        }

#if !YYGC_IDENTITY_BASELINE
        private static ObjectDefinition CreateDefinition(ObjectDefinitionDatabase database, string name, int id, ObjectType type, GameObject prefab)
        {
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.name = name; definition.Name = name; definition.Id = id; definition.Type = type; definition.NetType = NetworkType.Local;
            if (prefab != null) definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)));
            AssetDatabase.CreateAsset(definition, Root + "/" + name + ".asset");
            DefinitionIdentityAuthoring.EnsureIdentity(definition, database);
            database.AddDefinition(definition);
            return definition;
        }

        private static GameObject CreatePrefab(string name, bool network, bool ui)
        {
            var root = ui ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
            try
            {
                if (network) root.AddComponent<NetworkObject>();
                var instance = root.AddComponent<ObjectInstance>();
                MonoBehaviour initializer = network ? (MonoBehaviour)root.AddComponent<StateSynchronizer>() : root.AddComponent<LocalObjectInstanceInitializer>();
                Component view = ui ? (Component)root.AddComponent<UGUIView>() : root.AddComponent<ObjectView>();
                SetReference(instance, "_view", view);
                SetReference(initializer, "_objectInstance", instance);
                if (!ui)
                {
                    SetReference(view, "_ownerInstance", instance);
                    SetReference(view, "_initializer", initializer);
                    if (network) SetReference(view, "network", initializer);
                }
                if (network)
                {
                    root.AddComponent<IdentityNetworkObserver>();
                }
                return PrefabUtility.SaveAsPrefabAsset(root, Root + "/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
#endif
        /// <summary>记录真实构建产物属性；构建成功与运行通过分别报告。</summary>
        [Serializable] private sealed class BuildEvidence
        {
            public string unityVersion, result, backend;
            public bool development;
            public ulong bytes;
            public int errors;
        }
    }
}
