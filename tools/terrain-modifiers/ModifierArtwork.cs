using System;
using System.IO;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>把本批实际原生截图保存为新的派生 Editor 预览，保留旧预览字节；保存重开场景验证 Sprite 引用。</summary>
public static class ModifierArtwork
{
    public static string Run()
    {
        const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        const string target = root + "ReferenceChamberModifiersPreview.png";
        if (Application.isPlaying || File.Exists(target)) throw new InvalidOperationException("先停止 Play，且仅在空目标创建预览。");
        File.Copy("../artifacts/terrain-modifiers/fixed-rounded-native.png", target);
        AssetDatabase.ImportAsset(target);
        var importer = (TextureImporter)AssetImporter.GetAtPath(target);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 8; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
        importer.sRGBTexture = true; importer.SaveAndReimport();
        var scene = EditorSceneManager.OpenScene(root + "ReferenceChamber.unity");
        var artwork = UnityEngine.Object.FindAnyObjectByType<TerrainEditorArtwork>();
        if (artwork == null || artwork.Artwork == null) throw new Exception("缺少派生场景预览的明确引用。");
        artwork.Artwork.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(root + "ReferenceChamber.unity");
        var reopened = UnityEngine.Object.FindAnyObjectByType<TerrainEditorArtwork>();
        if (AssetDatabase.GetAssetPath(reopened.Artwork.sprite) != target) throw new Exception("预览引用重开失败。");
        return "New native 504x312 preview imported, scene saved/reopened; original artwork retained.";
    }
}
