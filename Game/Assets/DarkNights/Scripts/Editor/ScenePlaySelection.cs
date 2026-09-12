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
            scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<LevelLayoutAuthoring>(true) != null);

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
            if (string.IsNullOrEmpty(scene.path) || scene.isDirty)
            {
                Debug.LogError("请先保存当前关卡，再通过 Bootstrap 试玩该场景。");
                EditorApplication.isPlaying = false;
                return;
            }
            SessionState.SetString("DarkNights.PlayScene", scene.path);
        }
    }
}
