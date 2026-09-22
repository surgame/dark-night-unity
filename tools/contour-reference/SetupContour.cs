using System;
using System.IO;
using System.Linq;
using DarkNights.View.Terrain;
using DarkNights.Entry.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>在独立空目录建立候选风格和工作台场景；保留已有手工资源、场景与 GUID，普通导入不执行制作。</summary>
public static class SetupContour
{
    public static string Run()
    {
        const string dir = "Assets/DarkNights/Res/Terrain/CaveContourStatic";
        if (AssetDatabase.IsValidFolder(dir)) throw new InvalidOperationException("候选资源目录已存在，拒绝重建。");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("当前场景有未保存修改。");
        string original = EditorSceneManager.GetActiveScene().path;
        AssetDatabase.CreateFolder("Assets/DarkNights/Res/Terrain", "CaveContourStatic");
        var background = ScriptableObject.CreateInstance<CaveBackgroundStyle>();
        background.SourceMaskAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DarkNights/Res/Art/Custom/CaveContourStatic/background-mask-v16.png");
        AssetDatabase.CreateAsset(background, dir + "/Background.asset");
        var originalStyle = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>("Assets/DarkNights/Res/Terrain/CaveExploration/Style/CaveStyle.asset");
        var candidate = UnityEngine.Object.Instantiate(originalStyle); candidate.Background = background;
        AssetDatabase.CreateAsset(candidate, dir + "/CaveStyle.asset");
        string scene = dir + "/CaveContourStatic.unity";
        if (!AssetDatabase.CopyAsset("Assets/DarkNights/Res/Terrain/CaveExploration/CaveExploration.unity", scene))
            throw new InvalidOperationException("复制候选场景失败。");
        var opened = EditorSceneManager.OpenScene(scene);
        var bootstrap = opened.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TerrainDebugBootstrap>(true)).Single();
        bootstrap.CaveStyle = candidate;
        EditorSceneManager.MarkSceneDirty(opened); EditorSceneManager.SaveScene(opened);
        EditorSceneManager.OpenScene(scene);
        if (UnityEngine.Object.FindFirstObjectByType<TerrainDebugBootstrap>().CaveStyle != candidate) throw new Exception("候选场景重开引用失败。");
        var expedition = EditorSceneManager.OpenScene(RandomLevelEntry.ExpeditionScenePath);
        var template = expedition.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RandomLevelTemplate>(true)).Single();
        template.StaticBackgroundStyle = candidate;
        EditorSceneManager.MarkSceneDirty(expedition); EditorSceneManager.SaveScene(expedition);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(background.SourceMaskAtlas));
        importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.sRGBTexture = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        return "Created and reopened candidate scene: " + scene;
    }
}
