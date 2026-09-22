using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>借用当前 Local 唯一 Editor 前的有限保护；拒绝 Play、编译、构建或脏场景，不保存或丢弃用户内容。</summary>
public static class EditorSafety
{
    public static string Inspect() => new JObject
    {
        ["playing"] = EditorApplication.isPlayingOrWillChangePlaymode,
        ["compiling"] = EditorApplication.isCompiling,
        ["building"] = BuildPipeline.isBuildingPlayer,
        ["scenes"] = new JArray(Enumerable.Range(0, SceneManager.sceneCount).Select(i =>
        {
            var s = SceneManager.GetSceneAt(i);
            return new JObject { ["path"] = s.path, ["dirty"] = s.isDirty };
        }))
    }.ToString();
    public static string Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer ||
            Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Local Editor has active work or unsaved scenes; cannot borrow it.");
        AssetDatabase.DisallowAutoRefresh(); return "Auto-refresh held for one authorized branch checkout.";
    }
    public static string End()
    {
        AssetDatabase.AllowAutoRefresh(); AssetDatabase.Refresh(); return "Auto-refresh released; one resource/code batch submitted.";
    }
}
