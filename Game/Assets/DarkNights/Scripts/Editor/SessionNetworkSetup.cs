using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Network;
using FishNet.Managing.Object;
using FishNet.Object;
using GameCore.NetworkCommands;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using GameCore.Objects.Views;
using Runtime.AppStartup;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 一次性创建正式 owned 连接定义，并定向补上会话 Prefab 的生命周期绑定与启动模块。
    /// 新连接资源只输出到空目录，已有 WorldSession 只添加缺少的组件；日常验证或构建不调用此迁移。
    /// </summary>
    public static class SessionNetworkSetup
    {
        public const string Root = "Assets/DarkNights/Res/Objects/PlayerConnection";
        public const string PrefabPath = Root + "/PlayerConnection.prefab";
        public const string DefinitionPath = Root + "/PlayerConnection.asset";
        public const string Pinewatch = "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity";

        public static void Install()
        {
            EnvironmentValidation.Validate();
            if (Directory.Exists(Root) && Directory.GetFileSystemEntries(Root).Length != 0)
                throw new InvalidOperationException("Connection initialization requires an empty directory.");
            AssetDatabase.CreateFolder("Assets/DarkNights/Res/Objects", "PlayerConnection");
            var root = new GameObject("PlayerConnection");
            GameObject prefab;
            try
            {
                root.AddComponent<NetworkObject>();
                var instance = root.AddComponent<ObjectInstance>();
                var sync = root.AddComponent<StateSynchronizer>();
                var view = root.AddComponent<ObjectView>();
                var sender = root.AddComponent<NetworkCommandSender>();
                var endpoint = root.AddComponent<PlayerEndpoint>();
                Reference(instance, "_view", view);
                Reference(sync, "_objectInstance", instance);
                Reference(endpoint, "sender", sender);
                view.EditorSetBindings(new[] { new ViewComponentBinding("endpoint", endpoint) }, false);
                view.ForceRefreshAllReferences();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Name = "玩家连接";
            definition.NetType = NetworkType.Network;
            definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(PrefabPath));
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            definition.EditorSetIdentity(AssetDatabase.AssetPathToGUID(DefinitionPath), "connection.pinewatch", false);
            var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
            database.AddDefinition(definition);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(database);
            var spawnables = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>("Assets/DefaultPrefabObjects.asset");
            spawnables.AddObject(prefab.GetComponent<NetworkObject>(), true);
            EditorUtility.SetDirty(spawnables);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), settings.DefaultGroup).address = "dark_nights.object.player_connection";
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(settings.DefaultGroup);
            InstallSessionLink();
            var startup = AssetDatabase.LoadAssetAtPath<AppStartupSettings>(EnvironmentValidation.StartupPath);
            startup.EnsureDiscoveredModules();
            EditorUtility.SetDirty(startup);
            foreach (var asset in new UnityEngine.Object[] { definition, database, spawnables, settings, settings.DefaultGroup, startup })
                AssetDatabase.SaveAssetIfDirty(asset);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == Pinewatch)) scenes.Add(new EditorBuildSettingsScene(Pinewatch, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Validate();
            Debug.Log("DARK_NIGHTS_NETWORK_ASSETS_INSTALLED definitions=3 protocol=2");
        }

        private static void InstallSessionLink()
        {
            var root = PrefabUtility.LoadPrefabContents(FormalObjectContentSetup.SessionPrefabPath);
            try
            {
                var link = root.GetComponent<SessionObjectLink>();
                if (link != null) NativeObjectContracts.ValidateSessionLink(root, false);
                if (link == null) link = root.AddComponent<SessionObjectLink>();
                Reference(link, "instance", root.GetComponent<ObjectInstance>());
                Reference(link, "synchronizer", root.GetComponent<StateSynchronizer>());
                PrefabUtility.SaveAsPrefabAsset(root, FormalObjectContentSetup.SessionPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Connection Prefab missing.");
            var endpoint = prefab.GetComponent<PlayerEndpoint>();
            var view = prefab.GetComponent<ObjectView>();
            if (endpoint == null || endpoint.Sender == null || view.Get<PlayerEndpoint>("endpoint") != endpoint)
                throw new InvalidOperationException("Connection bindings are incomplete.");
            var session = AssetDatabase.LoadAssetAtPath<GameObject>(FormalObjectContentSetup.SessionPrefabPath);
            NativeObjectContracts.ValidateSessionLink(session);
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(DefinitionPath);
            if (definition == null || definition.Key != "connection.pinewatch" || definition.Id != 0 || !definition.isNetwork ||
                definition.GuidString != AssetDatabase.AssetPathToGUID(DefinitionPath)) throw new InvalidOperationException("Invalid connection definition.");
            EnvironmentValidation.RequireAddress(PrefabPath, "dark_nights.object.player_connection");
        }

        private static void Reference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
