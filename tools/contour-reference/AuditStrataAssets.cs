using System;
using System.IO;
using System.Linq;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
/// <summary>只读审查新场景依赖、样式与保存后的原生预览；不改写场景或人工资产。</summary>
public static class AuditStrataAssets
{
    public static string Run()
    {
        const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        if (Application.isPlaying) throw new InvalidOperationException("需要停止 Play。");
        var dependencies = AssetDatabase.GetDependencies(new[] { root + "ReferenceChamber.unity", root + "RandomCave.unity" }, true);
        var old = dependencies.Where(p => p.Contains("/Res/Art/Custom/CaveExploration/") ||
            p.Contains("/Res/Scenes/Pinewatch/") || p.Contains("/Res/Scenes/RandomPinewatch/") ||
            p.Contains("/Res/Terrain/CaveExploration/")).ToArray();
        if (old.Length != 0) throw new Exception("新场景依赖旧地形：" + string.Join(",", old));
        EditorSceneManager.OpenScene(root + "ReferenceChamber.unity");
        var boot = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        var preview = UnityEngine.Object.FindAnyObjectByType<TerrainEditorArtwork>();
        if (boot.FixedMap == null || preview == null || preview.Artwork.sprite == null || !preview.Artwork.enabled)
            throw new Exception("保存后的固定样板缺少编辑器布局。");
        boot.Definition.LoadGameplayCatalog();
        var result = new JObject { ["oldTerrainDependencies"] = new JArray(old), ["dependencies"] = dependencies.Length,
            ["scene"] = root + "ReferenceChamber.unity", ["style"] = boot.CaveStyle.VisualIdentity,
            ["spawn"] = boot.FixedMap.SpawnCell.ToString(), ["editorPreview"] = true, ["linear"] = QualitySettings.activeColorSpace.ToString() };
        File.WriteAllText("../artifacts/contour/assets-audit.json", result.ToString()); return result.ToString();
    }
}
