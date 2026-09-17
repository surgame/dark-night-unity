using System;
using System.IO;
using System.Linq;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using GameCore.NetworkCommands;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Terrain
{
    /// <summary>独立地图验收场景与 Mono 构建；初建和构建分开，保护已有资产并恢复构建前项目设置。</summary>
    public static class TerrainPlayerBuild
    {
        public const string Root = TerrainTestAssets.Root + "/NetworkTest";
        public const string ScenePath = Root + "/TerrainNetworkTest.unity";
        [MenuItem("Dark Nights/Terrain/Create network test scene")]
        public static void Create()
        {
            if (Directory.Exists(Root)) throw new IOException("网络测试资产已存在。");
            Directory.CreateDirectory(Root); AssetDatabase.ImportAsset(Root);
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var sender = new GameObject("Terrain command endpoint");
                var networkObject = sender.AddComponent<NetworkObject>(); sender.AddComponent<NetworkCommandSender>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(sender, Root + "/Endpoint.prefab");
                UnityEngine.Object.DestroyImmediate(sender);
                var senderAsset = prefab.GetComponent<NetworkObject>();
                var prefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>(); prefabs.AddObject(senderAsset, true);
                AssetDatabase.CreateAsset(prefabs, Root + "/SpawnablePrefabs.asset");
                var manager = new GameObject("Terrain NetworkManager").AddComponent<NetworkManager>(); manager.SpawnablePrefabs = prefabs;
                var probe = new GameObject("Explicit terrain network probe").AddComponent<TerrainNetworkProbe>();
                probe.Manager = manager; probe.SenderPrefab = senderAsset;
                probe.Map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(TerrainTestAssets.Root + "/Maps/GreypineTest.asset");
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("网络测试场景保存失败。");
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (original.IsValid()) SceneManager.SetActiveScene(original); }
        }

        [MenuItem("Dark Nights/Terrain/Build test Mono")]
        public static void Mono()
        {
            BuildMono(Path.GetFullPath("../artifacts/terrain/player-mono-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "/TerrainTest.exe"));
        }

        public static void BuildMono(string output, string[] scenes = null)
        {
            output = Path.GetFullPath(output);
            if (Directory.Exists(Path.GetDirectoryName(output)) && Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(output)).Any())
                throw new IOException("使用新的空 Player 输出目录。");
            byte[] settings = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
            var target = NamedBuildTarget.Standalone; var backend = PlayerSettings.GetScriptingBackend(target);
            var preload = PlayerSettings.GetPreloadedAssets();
            try
            {
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
                PlayerSettings.SetPreloadedAssets(Array.Empty<UnityEngine.Object>());
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes ?? new[] { ScenePath, TerrainTestAssets.Root + "/Maps/TerrainTest.unity" },
                    locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-result.json"), "{\"result\":\"" + result.summary.result +
                    "\",\"bytes\":" + result.summary.totalSize + ",\"errors\":" + result.summary.totalErrors + "}");
                if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("地图 Mono 构建失败。");
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, backend); PlayerSettings.SetPreloadedAssets(preload);
                File.WriteAllBytes("ProjectSettings/ProjectSettings.asset", settings);
            }
        }
    }
}
