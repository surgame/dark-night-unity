using System;
using System.IO;
using AnyRules.Next.Authoring;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 创建一次性的独立调试场景与原生观察角色 Prefab，后续打开或构建不会覆盖人工编辑。
    /// 场景不包含 LevelLayoutAuthoring，因此直接 Play，不重定向正式 Bootstrap 或修改其构建列表。
    /// </summary>
    public static class TerrainDebugSetup
    {
        public const string Root = "Assets/DarkNights/Res/Terrain/DebugBootstrap";
        public const string ScenePath = TerrainScenePaths.TerrainDebugBootstrap;

        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Create();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            if (Directory.Exists(Root)) throw new IOException("调试资产目录已存在，拒绝覆盖。");
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(TerrainTestAssets.DefinitionPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/DarkNights/Res/Art/Original/sprites/spr_worker_idle/spr_worker_idle_0.png");
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/DarkNights/Res/UI/Shared/UIFont.fontsettings");
            if (definition == null || sprite == null || font == null) throw new InvalidOperationException("缺少调试地形或原生美术引用。");
            Directory.CreateDirectory(Root); AssetDatabase.ImportAsset(Root);
            Directory.CreateDirectory(TerrainScenePaths.Workbenches); AssetDatabase.ImportAsset(TerrainScenePaths.Workbenches);
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Debug Follow Camera").AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 12;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f, .095f, .09f);
                camera.transform.position = new Vector3(97.5f, -88.75f, -10);
                var character = new GameObject("Debug Flyer");
                var flyer = character.AddComponent<TerrainDebugFlyer>();
                var art = new GameObject("Worker art").AddComponent<SpriteRenderer>();
                art.transform.SetParent(character.transform, false);
                art.sprite = sprite; art.sortingOrder = 20000;
                art.transform.localScale = Vector3.one * (sprite.pixelsPerUnit / 8);
                art.transform.localPosition = new Vector3(-sprite.rect.width / 16, sprite.rect.height / 8, 0);
                flyer.Art = art;
                var prefab = PrefabUtility.SaveAsPrefabAsset(character, Root + "/DebugFlyer.prefab");
                UnityEngine.Object.DestroyImmediate(character);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                flyer = instance.GetComponent<TerrainDebugFlyer>();
                flyer.ViewCamera = camera; flyer.Teleport(new Vector2(97.5f, -89.5f));
                var bootstrap = new GameObject("Terrain Debug Bootstrap").AddComponent<TerrainDebugBootstrap>();
                bootstrap.Definition = definition; bootstrap.Flyer = flyer;
                var panel = bootstrap.gameObject.AddComponent<TerrainDebugPanel>();
                panel.Bootstrap = bootstrap; panel.Font = font;
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("调试场景保存失败。");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (original.IsValid()) SceneManager.SetActiveScene(original);
            }
        }

        public static void Build()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("请先创建调试场景。");
            TerrainPlayerBuild.BuildMono(Path.GetFullPath("../artifacts/terrain-debug/player-mono-" +
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "/TerrainDebug.exe"), new[] { ScenePath });
        }
    }
}
