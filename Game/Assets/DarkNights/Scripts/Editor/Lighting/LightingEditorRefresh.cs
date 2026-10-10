using System;
using DarkNights.Entry;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>照明制作改动的 Editor 更新入口；合并资产与 Undo 通知，更新已运行会话的冻结配置，不启动会话、不访问存档或业务状态。</summary>
    [InitializeOnLoad]
    public static class LightingEditorRefresh
    {
        private static bool pending;
        static LightingEditorRefresh()
        {
            Undo.undoRedoPerformed += Request;
            EditorApplication.projectChanged += Request;
            EditorApplication.update += Tick;
        }

        public static void Request() => pending = true;

        private static void Tick()
        {
            if (!pending || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            pending = false;
            foreach (var target in UnityEngine.Object.FindObjectsByType<HeroLightPresentation>(FindObjectsSortMode.None))
                if (target.isActiveAndEnabled)
                {
                    try { target.RefreshLightingAssets(LoadPreset); }
                    catch (Exception error) { Debug.LogException(error, target); }
                }
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        public static LightProfile LoadPreset(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            return AssetDatabase.LoadAssetAtPath<LightProfile>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
