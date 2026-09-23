using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkNights.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

/// <summary>本批单一 Local Editor 的安装、场景切换与 Mono 构建入口；所有阶段有独立回执，不覆盖已有构建结果。</summary>
public static class ModifierEditor
{
    private static string Root => Path.GetFullPath("../artifacts/terrain-modifiers");
    public static string Inspect() => "compiling=" + EditorApplication.isCompiling + "; playing=" + Application.isPlaying +
        "; modifier=" + typeof(DarkNights.View.Terrain.CaveTerrainStyle).GetField("Modifiers") + "; color=" + QualitySettings.activeColorSpace;
    public static string Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer ||
            Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Editor 有活动工作。");
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, "original-scenes.json"), new JArray(Enumerable.Range(0, SceneManager.sceneCount)
            .Select(i => SceneManager.GetSceneAt(i).path)).ToString());
        TerrainModifierInstaller.Install();
        string path = DarkNights.Editor.Terrain.TerrainScenePaths.ReferenceChamber;
        EditorSceneManager.OpenScene(path); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); EditorSceneManager.OpenScene(path);
        return "Modifier assets saved; fixed scene saved/reopened; " + Inspect();
    }
    public static string Play(string scene = "ReferenceChamber")
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("已有 Play。");
        if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("场景尚未保存。");
        string path = scene == "ReferenceChamber" ? DarkNights.Editor.Terrain.TerrainScenePaths.ReferenceChamber :
            scene == "RandomCave" ? DarkNights.Editor.Terrain.TerrainScenePaths.RandomCave :
            throw new ArgumentException("未知地形工作台场景。", nameof(scene));
        EditorSceneManager.OpenScene(path);
        EditorApplication.isPlaying = true; return "Play requested: " + scene;
    }
    public static string Stop() { EditorApplication.isPlaying = false; return "Stop requested"; }
    public static string Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("先退出 Play 并检查场景。");
        var paths = JArray.Parse(File.ReadAllText(Path.Combine(Root, "original-scenes.json"))).Values<string>()
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p == TerrainModifierInstaller.Root + "ReferenceChamber.unity" ? DarkNights.Editor.Terrain.TerrainScenePaths.ReferenceChamber :
                p == TerrainModifierInstaller.Root + "RandomCave.unity" ? DarkNights.Editor.Terrain.TerrainScenePaths.RandomCave : p).ToArray();
        for (int i = 0; i < paths.Length; i++) EditorSceneManager.OpenScene(paths[i], i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
        return "Original Editor scenes restored";
    }
    public static string Build()
    {
        string result = Path.Combine(Root, "build-result.json");
        if (File.Exists(result) || BuildPipeline.isBuildingPlayer || Application.isPlaying) throw new InvalidOperationException("构建入口已有任务或结果。");
        Directory.CreateDirectory(Root);
        var report = new JObject { ["startedUtc"] = DateTime.UtcNow.ToString("O"), ["backend"] = "Mono" };
        File.WriteAllText(Path.Combine(Root, "build-started.json"), report.ToString());
        try
        {
            string output = Path.Combine(Root, "player-mono/DarkNights.exe");
            typeof(GamePlayerBuild).GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { ScriptingImplementation.Mono2x, "mono", BuildOptions.Development, output });
            report["success"] = true; report["player"] = output;
        }
        catch (Exception error) { report["success"] = false; report["error"] = error.ToString(); }
        report["finishedUtc"] = DateTime.UtcNow.ToString("O");
        File.WriteAllText(result + ".tmp", report.ToString()); File.Move(result + ".tmp", result);
        return report.ToString();
    }
}
