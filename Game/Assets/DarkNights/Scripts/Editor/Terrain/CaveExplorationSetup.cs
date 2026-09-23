using System;
using System.IO;
using DarkNights.Core.Config.Terrain;
using DarkNights.Entry.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>天然洞穴实验的独立场景入口；首次从现有调试场景派生，后续打开不覆盖人工编辑。</summary>
    public static class CaveExplorationSetup
    {
        public const string ScenePath = TerrainScenePaths.CaveExploration;

        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Create();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            string directory = Path.GetDirectoryName(ScenePath);
            if (File.Exists(ScenePath)) throw new IOException("实验场景已存在，拒绝覆盖人工资源。");
            Directory.CreateDirectory(directory);
            AssetDatabase.ImportAsset(directory);
            if (!AssetDatabase.CopyAsset(TerrainDebugSetup.ScenePath, ScenePath))
                throw new IOException("复制调试场景失败。");
            var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var bootstrap in root.GetComponentsInChildren<TerrainDebugBootstrap>(true))
                    {
                        bootstrap.Settings = new TerrainGenerationSettings
                        {
                            Seed = "CAVE-EXPLORATION-01", Surface = "rolling",
                            ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile
                        };
                        bootstrap.name = "Cave Exploration Bootstrap";
                        EditorUtility.SetDirty(bootstrap);
                    }
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("实验场景保存失败。");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (original.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
            }
        }
    }
}
