using System;
using System.IO;
using System.Linq;
using AnyRules.Next.Authoring;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>显式安装洞穴样式及工作台绑定；新样式只初建一次，保留原始 Prefab、GUID 与其它场景，后续打开不再生成。</summary>
    public static class CaveWorkshopSetup
    {
        public const string StylePath = CaveTerrainAssets.Root + "/CaveStyle.asset";
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出 Play。");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("当前场景有未保存编辑。");
            if (!File.Exists(CaveTerrainAssets.DefinitionPath)) CaveTerrainAssets.Create();
            const string rockPath = "Assets/DarkNights/Res/Art/Custom/CaveExploration/cave-rock.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(rockPath);
            importer.textureType = TextureImporterType.Default; importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
            importer.sRGBTexture = true; importer.wrapMode = TextureWrapMode.Repeat; importer.SaveAndReimport();
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(StylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<CaveTerrainStyle>();
                style.Shader = Shader.Find("DarkNights/CavePixelRock"); style.Rock = AssetDatabase.LoadAssetAtPath<Texture2D>(rockPath);
                if (style.Shader == null || style.Rock == null) throw new InvalidOperationException("洞穴素材未导入。");
                AssetDatabase.CreateAsset(style, StylePath);
            }
            var scene = EditorSceneManager.OpenScene(CaveExplorationSetup.ScenePath);
            var bootstrap = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TerrainDebugBootstrap>(true)).Single();
            bootstrap.CaveStyle = style;
            bootstrap.Definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(CaveTerrainAssets.DefinitionPath);
            bootstrap.BalanceJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DarkNights/Res/Config/balance.json");
            bootstrap.LevelJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/DarkNights/Res/Config/pinewatch.json");
            var input = bootstrap.gameObject.GetComponent<CaveWorkshopInput>();
            if (input == null) input = bootstrap.gameObject.AddComponent<CaveWorkshopInput>();
            input.Bootstrap = bootstrap; input.Walking = true;
            var flyer = bootstrap.Flyer; flyer.CameraDistance = 10;
            flyer.UsePixelsPerCell(CaveWorkshopInput.ArtPixelsPerCell);
            EditorUtility.SetDirty(bootstrap); EditorUtility.SetDirty(input);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(CaveExplorationSetup.ScenePath);
        }
    }
}
