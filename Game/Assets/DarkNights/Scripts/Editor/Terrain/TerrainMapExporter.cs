using System;
using System.IO;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Terrain
{
    /// <summary>把已验证蓝图导出到新资产与独立场景；不会修改灰松谷、已有 Prefab 或人工地图。</summary>
    public static class TerrainMapExporter
    {
        public static TerrainMapAsset Export(TerrainBlueprint blueprint, ARDMapDefinition definition, string assetPath)
        {
            if (blueprint == null || definition == null) throw new ArgumentNullException(nameof(blueprint));
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/DarkNights/Res/Terrain/", StringComparison.Ordinal) ||
                assetPath.Contains("..") || !assetPath.EndsWith(".asset", StringComparison.Ordinal))
                throw new ArgumentException("输出必须在 Res/Terrain 内的新 .asset 路径。");
            string cellsPath = Path.ChangeExtension(assetPath, ".cells.bytes");
            if (File.Exists(assetPath) || File.Exists(cellsPath)) throw new IOException("输出已存在，请选择新名称。");
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            var bytes = new byte[blueprint.Width * blueprint.Height * 2];
            for (int y = 0; y < blueprint.Height; y++) for (int x = 0; x < blueprint.Width; x++)
            {
                int i = (y * blueprint.Width + x) * 2;
                bytes[i] = blueprint.MaterialAt(x, y); bytes[i + 1] = (byte)(blueprint.IsProtected(x, y) ? 1 : 0);
            }
            File.WriteAllBytes(cellsPath, bytes); AssetDatabase.ImportAsset(cellsPath, ImportAssetOptions.ForceSynchronousImport);
            var asset = ScriptableObject.CreateInstance<TerrainMapAsset>();
            asset.Settings = blueprint.Settings; asset.Definition = definition;
            asset.InitialCells = AssetDatabase.LoadAssetAtPath<TextAsset>(cellsPath);
            AssetDatabase.CreateAsset(asset, assetPath); AssetDatabase.SaveAssets(); return asset;
        }

        public static void CreateTestScene(TerrainMapAsset map, string path)
        {
            if (map == null || !path.StartsWith("Assets/DarkNights/Res/Terrain/", StringComparison.Ordinal) ||
                path.Contains("..") || !path.EndsWith(".unity", StringComparison.Ordinal) || File.Exists(path))
                throw new ArgumentException("只允许新的地形测试场景路径。");
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Terrain Camera").AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 102;
                camera.transform.position = new Vector3(160, -95, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .16f, .16f);
                var preview = new GameObject("Terrain Preview").AddComponent<TerrainPreview>();
                preview.Map = map; preview.ViewCamera = camera;
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("场景保存失败。");
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (original.IsValid()) SceneManager.SetActiveScene(original); }
        }

        [MenuItem("Dark Nights/Terrain/Create default test map and scene")]
        public static void CreateDefault()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(TerrainTestAssets.DefinitionPath);
            var map = Export(TerrainGenerator.Generate(new TerrainGenerationSettings()), definition,
                TerrainTestAssets.Root + "/Maps/GreypineTest.asset");
            CreateTestScene(map, TerrainTestAssets.Root + "/Maps/TerrainTest.unity");
        }
    }
}
