using System.IO;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
/// <summary>在原始轮廓替换后把固定样板出生点置于中层平台，并刷新本任务持有的派生预览，保留资源 GUID。</summary>
public static class RefreshStrataPreview
{
    public static string Run()
    {
        const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        if (Application.isPlaying) throw new System.InvalidOperationException("先停止 Play。");
        var scene = EditorSceneManager.OpenScene(root + "ReferenceChamber.unity");
        var boot = Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        boot.FixedMap.SpawnCell = new Vector2Int(69, 51); EditorUtility.SetDirty(boot.FixedMap);
        boot.Flyer.Teleport(new Vector2(69, -51.5f));
        File.Copy("../artifacts/contour/strata-editor-preview.png", root + "ReferenceChamberPreview.png", true);
        AssetDatabase.ImportAsset(root + "ReferenceChamberPreview.png");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(root + "ReferenceChamber.unity"); return "Saved/reopened fixed spawn and derived editor artwork";
    }
}
