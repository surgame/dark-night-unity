using System;
using System.IO;
using DarkNights.Samples.LanCoop.Bootstrap;
using DarkNights.Samples.LanCoop.Runtime;
using DarkNights.Samples.LanCoop.Presentation;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using GameCore.NetworkCommands;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using YY.Features.Players.View;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DarkNights.Samples.LanCoop.Editor
{
    /// <summary>只在指定空目录创建第一版原生资产；普通构建只读取场景，不覆盖人类保存的 Prefab 和布局。</summary>
    public static class SampleBuilder
    {
        public const string Root = "Assets/Samples/LanCoop/Content";
        public const string ScenePath = Root + "/LanCoop.unity";

        [MenuItem("Dark Nights/Samples/LAN/Create Initial Assets")]
        public static void Create()
        {
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("Sample output must be empty. Existing authored assets are never overwritten.");
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var definition = Definition("Session", 929101, true);
            definition.BehaviourTypes.Add(typeof(CampBehaviour).FullName);
            EditorUtility.SetDirty(definition);
            var localDefinition = Definition("Worksite", 929102, false);

            var session = new GameObject("CampSession");
            session.AddComponent<NetworkObject>();
            session.AddComponent<ObjectInstance>();
            session.AddComponent<StateSynchronizer>();
            var sessionPrefab = Save(session, "CampSession");
            var sender = new GameObject("OwnedEndpoint");
            sender.AddComponent<NetworkObject>();
            sender.AddComponent<NetworkCommandSender>();
            sender.AddComponent<SampleEndpoint>();
            var senderPrefab = Save(sender, "OwnedEndpoint");

            var worksite = new GameObject("SharedWorksite");
            var instance = worksite.AddComponent<ObjectInstance>();
            var view = worksite.AddComponent<ObjectView>();
            var binding = new SerializedObject(instance);
            binding.FindProperty("_view").objectReferenceValue = view;
            binding.ApplyModifiedPropertiesWithoutUndo();
            var art = GameObject.CreatePrimitive(PrimitiveType.Cube);
            art.name = "ArtOffset";
            art.transform.SetParent(worksite.transform, false);
            art.transform.localPosition = new Vector3(0, .5f, 0);
            UnityEngine.Object.DestroyImmediate(art.GetComponent<Collider>());
            var worksitePrefab = Save(worksite, "SharedWorksite");

            var prefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
            prefabs.AddObject(sessionPrefab.GetComponent<NetworkObject>(), true);
            prefabs.AddObject(senderPrefab.GetComponent<NetworkObject>(), true);
            AssetDatabase.CreateAsset(prefabs, Root + "/SpawnablePrefabs.asset");
            var manager = new GameObject("Sample NetworkManager").AddComponent<NetworkManager>();
            manager.SpawnablePrefabs = prefabs;
            new GameObject("YYGC Behaviour Updates").AddComponent<BehaviourUpdateManager>();
            var root = new GameObject("LAN Sample");
            var network = root.AddComponent<SampleNetwork>();
            network.Manager = manager;
            network.SessionDefinition = definition;
            network.SessionPrefab = sessionPrefab.GetComponent<NetworkObject>();
            network.SenderPrefab = senderPrefab.GetComponent<NetworkObject>();
            var panel = root.AddComponent<SamplePanel>();
            var token = (GameObject)PrefabUtility.InstantiatePrefab(worksitePrefab);
            token.transform.position = new Vector3(2, 0, 0);
            panel.WorksiteVisual = token.transform;
            var composition = root.AddComponent<SampleComposition>();
            composition.Network = network;
            composition.Panel = panel;
            composition.Worksite = token.GetComponent<ObjectInstance>();
            composition.WorksiteDefinition = localDefinition;
            root.AddComponent<SampleAutomation>().Network = network;

            var camera = new GameObject("Sample Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 5, -9);
            camera.transform.LookAt(new Vector3(0, .5f, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .05f, .08f);
            var light = new GameObject("Sample Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40, -30, 0);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            // 重开已保存场景：验证资产引用可序列化，不把内存对象当成交付物。
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("LAN_SAMPLE_ASSETS_CREATED");
        }

        private static ObjectDefinition Definition(string name, int id, bool network)
        {
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Id = id;
            definition.Name = "LAN sample " + name;
            definition.NetType = network ? NetworkType.Network : NetworkType.Local;
            definition.EditorSetIdentity(network ? "f1aee01796e9465d993c64b073f526d1" :
                "c5a7f9a0187944339fe233f509d21a0c", "lan_sample." + name.ToLowerInvariant(), true);
            AssetDatabase.CreateAsset(definition, Root + "/" + name + ".asset");
            return definition;
        }
        private static GameObject Save(GameObject value, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(value, Root + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(value);
            return prefab;
        }

        [MenuItem("Dark Nights/Samples/LAN/Build Windows Player")]
        public static void Build()
        {
            BuildPlayer(ScriptingImplementation.Mono2x, "player", BuildOptions.Development);
        }

        [MenuItem("Dark Nights/Samples/LAN/Build Windows IL2CPP Player")]
        public static void BuildIl2Cpp()
        {
            BuildPlayer(ScriptingImplementation.IL2CPP, "player-il2cpp", BuildOptions.None);
        }

        private static void BuildPlayer(ScriptingImplementation requestedBackend, string folder, BuildOptions options)
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Create Sample assets first", ScenePath);
            var target = NamedBuildTarget.Standalone;
            var backend = PlayerSettings.GetScriptingBackend(target);
            var stripping = PlayerSettings.GetManagedStrippingLevel(target);
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            var previousContentBuild = addressables.BuildAddressablesWithPlayerBuild;
            try
            {
                // Sample 的原生资产直接随场景打包，不构建正式游戏 Addressables。
                addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                PlayerSettings.SetScriptingBackend(target, requestedBackend);
                // Release + High 同时验证 AOT 和代码裁剪；退出时恢复正式项目设置。
                if (requestedBackend == ScriptingImplementation.IL2CPP)
                    PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
                string output = Path.GetFullPath("../artifacts/lan-sample/" + folder + "/LanCoop.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = options
                });
                if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Sample build failed");
                Debug.Log("LAN_SAMPLE_BUILD=" + output);
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, backend);
                PlayerSettings.SetManagedStrippingLevel(target, stripping);
                addressables.BuildAddressablesWithPlayerBuild = previousContentBuild;
            }
        }
    }
}
