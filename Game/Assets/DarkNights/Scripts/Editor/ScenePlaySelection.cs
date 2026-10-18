using System;
using System.IO;
using System.Linq;
using DarkNights.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 将当前已保存关卡作为 Bootstrap 后加载的试玩目标，支持未加入构建列表的关卡副本。
    /// CL02的无内容差异脏标记通过原生检查副本识别；真实编辑不自动保存或丢弃。
    /// 只保存本机 SessionState，不改正式 Bootstrap 或 Player 场景设置；不自动进入 Play。
    /// </summary>
    [InitializeOnLoad]
    public static class ScenePlaySelection
    {
        static ScenePlaySelection()
        {
            EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Configure();
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.delayCall += Configure;
        }

        private static bool IsLevel(Scene scene) => scene.IsValid() && scene.isLoaded &&
            scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<LevelLayoutAuthoring>(true) != null ||
                root.GetComponentInChildren<Entry.Terrain.GeologyLabEntry>(true) != null);

        private static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (IsLevel(scene))
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentValidation.ScenePath);
            else if (EditorSceneManager.playModeStartScene != null &&
                AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == EnvironmentValidation.ScenePath)
                EditorSceneManager.playModeStartScene = null;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            Scene scene = SceneManager.GetActiveScene();
            SessionState.EraseString("DarkNights.PlayScene");
            if (!IsLevel(scene)) return;
            if (string.IsNullOrEmpty(scene.path) || scene.isDirty && !MatchesSavedGeologyScene(scene))
            {
                Debug.LogError("当前关卡有未保存的内容修改，或无法核对已保存版本；请保存后再通过 Bootstrap 试玩。");
                EditorApplication.isPlaying = false;
                return;
            }
            SessionState.SetString("DarkNights.PlayScene", scene.path);
        }

        /// <summary>仅核对现行CL02的已保存内容，供试玩和工作台切换共用；检查副本保留，不清除真实草稿。</summary>
        internal static bool MatchesSavedGeologyScene(Scene scene)
        {
            if (scene.path != Entry.Terrain.GeologyLabEntry.ScenePath || !File.Exists(scene.path)) return false;
            try
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                    "../../artifacts/scene-play-checks"));
                Directory.CreateDirectory(directory);
                string copy = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") +
                    "-" + Guid.NewGuid().ToString("N") + ".unity");
                if (!EditorSceneManager.SaveScene(scene, copy, true)) return false;
                // saveAsCopy保留原路径、原文件和脏标记；副本原位保留供诊断，不删除作者草稿。
                bool matches = File.ReadAllBytes(scene.path).SequenceEqual(File.ReadAllBytes(copy));
                if (matches) Debug.Log("CL02仅有未保存标记，内容与已保存场景一致，可继续使用已保存版本。");
                return matches;
            }
            catch (Exception error)
            {
                Debug.LogError("CL02试玩前内容核对失败，原场景保持：" + error.Message);
                return false;
            }
        }

    }
}
