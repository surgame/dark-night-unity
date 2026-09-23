using System;
using System.IO;
using System.Linq;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
/// <summary>把实际初始样板的原生分辨率渲染保存为 EditorOnly 布局预览；只显式创建本任务的新资产。</summary>
public static class InstallEditorArtwork
{
    public static string Run()
    {
        const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        const string asset = root + "ReferenceChamberPreview.png";
        if (Application.isPlaying || File.Exists(asset)) throw new InvalidOperationException("需要停止 Play 且预览资产不存在。");
        File.Copy("../artifacts/contour/strata-editor-preview.png", asset);
        AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(asset); importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 8; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.sRGBTexture = true; importer.SaveAndReimport();
        var scene = EditorSceneManager.OpenScene(DarkNights.Editor.Terrain.TerrainScenePaths.ReferenceChamber);
        var boot = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        boot.FixedMap.SpawnCell = new Vector2Int(69, 51); EditorUtility.SetDirty(boot.FixedMap);
        boot.Flyer.Teleport(new Vector2(69, -51.5f));
        var actorPrefab = PrefabUtility.LoadPrefabContents(root + "Explorer.prefab");
        try
        {
            var flyer = actorPrefab.GetComponent<TerrainDebugFlyer>();
            flyer.WalkFrames = flyer.WalkFrames.OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
            PrefabUtility.SaveAsPrefabAsset(actorPrefab, root + "Explorer.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(actorPrefab); }
        var preview = new GameObject("Editor reference layout"); preview.tag = "EditorOnly";
        preview.transform.position = new Vector3(71, -55, 1);
        var renderer = preview.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset); renderer.sortingOrder = -100;
        preview.AddComponent<TerrainEditorArtwork>().Artwork = renderer;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(DarkNights.Editor.Terrain.TerrainScenePaths.ReferenceChamber); return "Saved and reopened native fixed-chamber editor artwork";
    }
}
