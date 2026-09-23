using System;
using System.IO;
using FishNet.Managing;
using FishNet.Managing.Object;
using Runtime.AppStartup;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 仅在首次准备且目标资产不存在时创建宿主启动资源。
    /// 日常构建只验证并读取已有资源，不重新保存人工维护的场景、Prefab 或配置。
    /// </summary>
    public static class DarkNightsEnvironmentSetup
    {
        private const string AppStartupSettingsPath = "Assets/Addressables/Datas/AppStartup/AppStartupSettings.asset";
        private const string GameCorePrefabPath = "Assets/Addressables/GameResources/GameCore.prefab";
        private const string NetworkManagerPrefabPath = "Assets/Network/NetworkManager.prefab";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        public static void Initialize()
        {
            EnvironmentValidation.RequireEmptyOutputs();
            EnsureFolder("Assets/Addressables");
            EnsureFolder("Assets/Addressables/Datas");
            EnsureFolder("Assets/Addressables/Datas/AppStartup");
            EnsureFolder("Assets/Addressables/GameResources");
            EnsureFolder("Assets/Network");
            EnsureFolder("Assets/Scenes");

            AppStartupSettings startupSettings = EnsureAppStartupSettings();
            EnsureGameCorePrefab(startupSettings);
            DefaultPrefabObjects defaultPrefabObjects = EnsureDefaultPrefabObjects();
            EnsureNetworkManagerPrefab(defaultPrefabObjects);

            AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            AddAddressableEntry(addressableSettings, "Assets/Addressables/Datas/GlobalSO/GlobalScriptableObjectDatabase.asset", "GlobalScriptableObjectDatabase");
            AddAddressableEntry(addressableSettings, "Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset", "ObjectDefinitionDatabase");
            AddAddressableEntry(addressableSettings, "Assets/Addressables/Datas/GlobalSO/ObjectSingletonDatabase.asset", "ObjectSingletonDatabase");
            AddAddressableEntry(addressableSettings, "Assets/Addressables/Datas/GlobalSO/ObjectArchetypeDatabase.asset", "ObjectArchetypeDatabase");
            AddAddressableEntry(addressableSettings, AppStartupSettingsPath, "AppStartupSettings");
            AddAddressableEntry(addressableSettings, GameCorePrefabPath, "GameCore");
            AddAddressableEntry(addressableSettings, NetworkManagerPrefabPath, "NetworkManager");
            addressableSettings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);

            EnsureBootstrapScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DarkNights] Environment initialization completed. Addressables, AppStartup, GameCore, FishNet and Bootstrap are ready.");
        }

        public static void BuildAddressablesContent()
        {
            EnvironmentValidation.Validate();
            bool previousLayout = ProjectConfigData.GenerateBuildLayout;
            try
            {
                // 输出诊断布局，也避免干净环境首次构建弹出可选报告提示，阻塞自动执行。
                ProjectConfigData.GenerateBuildLayout = true;
                AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
                if (!string.IsNullOrEmpty(result.Error))
                    throw new InvalidOperationException($"Addressables content build failed: {result.Error}");
            }
            finally
            {
                ProjectConfigData.GenerateBuildLayout = previousLayout;
            }

            Debug.Log("[DarkNights] Addressables content build completed.");
        }

        private static AppStartupSettings EnsureAppStartupSettings()
        {
            AppStartupSettings settings = AssetDatabase.LoadAssetAtPath<AppStartupSettings>(AppStartupSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<AppStartupSettings>();
                settings.EnsureDiscoveredModules();
                AssetDatabase.CreateAsset(settings, AppStartupSettingsPath);
            }
            else if (settings.EnsureDiscoveredModules())
            {
                EditorUtility.SetDirty(settings);
            }

            return settings;
        }

        private static void EnsureGameCorePrefab(AppStartupSettings settings)
        {
            CreateOrUpdatePrefab(GameCorePrefabPath, "GameCore", root =>
            {
                AppStartup appStartup = root.GetComponent<AppStartup>() ?? root.AddComponent<AppStartup>();
                SerializedObject serialized = new SerializedObject(appStartup);
                SerializedProperty settingsProperty = serialized.FindProperty("_settings");
                if (settingsProperty != null)
                {
                    settingsProperty.objectReferenceValue = settings;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            });
        }

        private static DefaultPrefabObjects EnsureDefaultPrefabObjects()
        {
            DefaultPrefabObjects prefabObjects = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
            if (prefabObjects != null)
            {
                return prefabObjects;
            }

            prefabObjects = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
            AssetDatabase.CreateAsset(prefabObjects, DefaultPrefabObjectsPath);
            return prefabObjects;
        }

        private static void EnsureNetworkManagerPrefab(DefaultPrefabObjects defaultPrefabObjects)
        {
            CreateOrUpdatePrefab(NetworkManagerPrefabPath, "NetworkManager", root =>
            {
                NetworkManager networkManager = root.GetComponent<NetworkManager>() ?? root.AddComponent<NetworkManager>();
                SerializedObject serialized = new SerializedObject(networkManager);
                SerializedProperty prefabsProperty = serialized.FindProperty("_spawnablePrefabs");
                if (prefabsProperty != null)
                {
                    prefabsProperty.objectReferenceValue = defaultPrefabObjects;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            });
        }

        private static void CreateOrUpdatePrefab(string path, string rootName, Action<GameObject> configure)
        {
            GameObject root;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    configure(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return;
            }

            root = new GameObject(rootName);
            try
            {
                configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AddAddressableEntry(AddressableAssetSettings settings, string assetPath, string address)
        {
            if (settings == null || AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                Debug.LogWarning($"[DarkNights] Addressable asset was not found: {assetPath}");
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[DarkNights] Could not resolve asset GUID: {assetPath}");
                return;
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, true);
            entry.address = address;
        }

        private static void EnsureBootstrapScene()
        {
            Scene bootstrap;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) != null)
            {
                bootstrap = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            }
            else
            {
                bootstrap = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            GameObject gameCorePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameCorePrefabPath);
            GameObject networkManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkManagerPrefabPath);
            EnsurePrefabInstance(gameCorePrefab, "GameCore");
            EnsurePrefabInstance(networkManagerPrefab, "NetworkManager");

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), BootstrapScenePath);
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            var ordered = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            ordered.Add(new EditorBuildSettingsScene(BootstrapScenePath, true));
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path != BootstrapScenePath)
                {
                    ordered.Add(scene);
                }
            }

            EditorBuildSettings.scenes = ordered.ToArray();
        }

        private static void EnsurePrefabInstance(GameObject prefab, string name)
        {
            if (prefab == null || GameObject.Find(name) != null)
            {
                return;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.name = name;
                SceneManager.MoveGameObjectToScene(instance, SceneManager.GetActiveScene());
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent ?? "Assets", folder);
        }
    }
}
