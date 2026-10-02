using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarkNights.Editor.Terrain;
using DarkNights.Entry;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 一次有限的场景目录迁移；预检全部来源、GUID 和未保存状态，再通过 AssetDatabase 移动。
    /// 保留场景及 meta 字节，更新构建列表，只移除核实为空的旧目录，不删除场景或共享资源。
    /// </summary>
    public static class GameSceneCatalogOrganizer
    {
        private static readonly string[,] Moves =
        {
            { "Assets/DarkNights/Res/Scenes/Workbenches/Terrain/ReferenceChamber.unity", TerrainScenePaths.ReferenceChamber, "03daec6cbfa91d24fbe0c1324beff73a" },
            { "Assets/DarkNights/Res/Scenes/Pinewatch/Pinewatch.unity", GameScenePaths.StaticCamp, "81168bb9d5dd6ce4782167451ad618da" },
            { "Assets/DarkNights/Res/Scenes/RandomPinewatch/Pinewatch.unity", GameScenePaths.RandomCamp, "a002034e2cdb87b4dbc4b877e1207cb2" },
            { "Assets/DarkNights/Res/Scenes/Workbenches/Terrain/(old)/TerrainDebugBootstrap(old).unity", TerrainScenePaths.TerrainDebugBootstrap, "078f8b061cf736f47ba98e7fd9f906dc" },
            { "Assets/DarkNights/Res/Scenes/Workbenches/Terrain/(old)/CaveExploration(old).unity", TerrainScenePaths.CaveExploration, "56096e47abdc4ed4ba4d0beb210ac3fc" },
            { "Assets/DarkNights/Res/Scenes/PendingDeletion/Terrain/CaveContourStatic.unity", TerrainScenePaths.PendingCaveContourStatic, "83de985c436433b4993b518d1e2fee37" }
        };

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("退出 Play 并等待编译完成后整理场景。");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty))
                throw new InvalidOperationException("存在未保存场景，整理未执行。");
            string[,] hashes = new string[Moves.GetLength(0), 2];
            for (int i = 0; i < Moves.GetLength(0); i++)
            {
                string current = AssetDatabase.GUIDToAssetPath(Moves[i, 2]);
                if (current != Moves[i, 0] && current != Moves[i, 1]) throw new InvalidOperationException("场景来源不匹配：" + current);
                if (current != Moves[i, 1] && File.Exists(Moves[i, 1])) throw new IOException("目标已存在，拒绝覆盖：" + Moves[i, 1]);
                hashes[i, 0] = Hash(current); hashes[i, 1] = Hash(current + ".meta");
            }
            for (int i = 0; i < Moves.GetLength(0); i++)
            {
                EnsureFolder(Path.GetDirectoryName(Moves[i, 1]).Replace('\\', '/'));
                string current = AssetDatabase.GUIDToAssetPath(Moves[i, 2]);
                if (current != Moves[i, 1])
                {
                    string error = AssetDatabase.MoveAsset(current, Moves[i, 1]);
                    if (error.Length != 0) throw new IOException(error);
                }
                if (AssetDatabase.AssetPathToGUID(Moves[i, 1]) != Moves[i, 2] ||
                    Hash(Moves[i, 1]) != hashes[i, 0] || Hash(Moves[i, 1] + ".meta") != hashes[i, 1])
                    throw new InvalidOperationException("迁移未保持原场景／meta 字节及 GUID：" + Moves[i, 1]);
            }
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != "Assets/Scenes/SampleScene.unity")
                .Select(s => new EditorBuildSettingsScene(CurrentPath(s.path), s.enabled)).ToArray();
            foreach (var scene in setup) scene.path = CurrentPath(scene.path);
            if (setup.Any(s => s.path != UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).path))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            foreach (string folder in new[] { "Workbenches/Terrain/(old)", "PendingDeletion/Terrain", "PendingDeletion", "Pinewatch", "RandomPinewatch" })
            {
                string path = "Assets/DarkNights/Res/Scenes/" + folder;
                if (AssetDatabase.IsValidFolder(path) && !Directory.EnumerateFileSystemEntries(path).Any()) AssetDatabase.DeleteAsset(path);
            }
            UnityEngine.Debug.Log("DARK_NIGHTS_SCENE_CATALOG scenes=16 moved=6 bytes_preserved=true build_scenes=" + EditorBuildSettings.scenes.Length);
        }

        private static string CurrentPath(string path)
        {
            for (int i = 0; i < Moves.GetLength(0); i++) if (Moves[i, 0] == path) return Moves[i, 1];
            return path;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static string Hash(string path)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
