using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 当前地形工作台的只读打开入口及旧场景归组工具；显式执行才移动资源，保留 GUID 和人工场景内容。
    /// Play、编译或未保存场景期间拒绝整理；旧场景仍供回归使用，不删除其共享脚本和美术。
    /// </summary>
    public static class TerrainWorkbenchScenes
    {
        public static void Open(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new FileNotFoundException("场景不存在；打开入口不会生成或覆盖场景。", path);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path);
        }

        public static void OrganizeOldScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("请等待编译并退出 Play。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("存在未保存场景，整理未执行。");
            string[,] entries = {
                { TerrainScenePaths.Workbenches + "/TerrainDebugBootstrap.unity", TerrainScenePaths.TerrainDebugBootstrap, "078f8b061cf736f47ba98e7fd9f906dc" },
                { TerrainScenePaths.Workbenches + "/CaveExploration.unity", TerrainScenePaths.CaveExploration, "56096e47abdc4ed4ba4d0beb210ac3fc" }
            };
            for (int i = 0; i < entries.GetLength(0); i++)
            {
                string current = AssetDatabase.GUIDToAssetPath(entries[i, 2]);
                if (current != entries[i, 0] && current != entries[i, 1])
                    throw new InvalidOperationException("旧场景来源不匹配：" + current);
                if (current != entries[i, 1] && File.Exists(entries[i, 1]))
                    throw new IOException("目标场景已存在，拒绝覆盖：" + entries[i, 1]);
                if (EditorBuildSettings.scenes.Any(scene => scene.path == current))
                    throw new InvalidOperationException("旧工作台在构建列表中，停止整理：" + current);
            }
            if (!AssetDatabase.IsValidFolder(TerrainScenePaths.OldWorkbenches))
                AssetDatabase.CreateFolder(TerrainScenePaths.Workbenches, "(old)");
            for (int i = 0; i < entries.GetLength(0); i++)
            {
                if (AssetDatabase.GUIDToAssetPath(entries[i, 2]) == entries[i, 1]) continue;
                string error = AssetDatabase.MoveAsset(entries[i, 0], entries[i, 1]);
                if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                if (AssetDatabase.AssetPathToGUID(entries[i, 1]) != entries[i, 2])
                    throw new InvalidOperationException("移动后 GUID 不匹配：" + entries[i, 1]);
            }
        }
    }
}
