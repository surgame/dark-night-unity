using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式迁移既有场景的预览实例，保留 Prefab 连接、位置、顺序、名字及变体。
    /// 迁移前检查全部对象，拒绝脏场景；普通导入和构建不执行迁移，也不重建美术资源。
    /// </summary>
    public static class SceneDefinitionUpgrade
    {
        [MenuItem("Dark Nights/Content/Upgrade Scene Definition Loaders")]
        public static void Upgrade()
        {
            string[] paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/DarkNights/Res/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            int changed = 0;
            foreach (string path in paths) changed += UpgradeScene(path);
            Directory.CreateDirectory("../artifacts/scene-definitions");
            File.WriteAllText("../artifacts/scene-definitions/upgrade.txt", "success=true placements=" + changed + "\nregression=not_run\n");
            Debug.Log("DARK_NIGHTS_SCENE_DEFINITIONS_UPGRADED placements=" + changed);
        }

        private static int UpgradeScene(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save scene before explicit migration: " + path);
                var markers = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelPlacementMarker>(true)).ToArray();
                var database = ObjectDefinitionDatabase.Instance;
                database.RebuildLookup();
                var pending = markers.Where(value => value.Loader == null).Select(marker =>
                {
                    ObjectView view = marker.GetComponentsInChildren<ObjectView>(true).Single();
                    string guid = AssetDatabase.AssetPathToGUID(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view));
                    ObjectDefinition definition = database.Definitions.Single(value => value.PrefabRef != null && value.PrefabRef.AssetGUID == guid);
                    DefinitionRuleIndex.Kind(definition);
                    return (Marker: marker, View: view, Definition: definition);
                }).ToArray();
                foreach (var item in pending)
                {
                    var loader = item.View.GetComponent<ObjectDefinitionLoader>();
                    if (loader == null) loader = Undo.AddComponent<ObjectDefinitionLoader>(item.View.gameObject);
                    loader.EditorConfigure(item.Definition);
                    LayoutVisualPreview preview = item.View.GetComponent<LayoutVisualPreview>();
                    if (preview != null) Undo.DestroyObjectImmediate(preview);
                    SceneDefinitionAuthoring.Configure(loader, item.Marker.SpawnOrder, item.Marker.Variant, item.Marker.ActorName, false);
                    if (item.Marker.gameObject != item.View.gameObject) Undo.DestroyObjectImmediate(item.Marker);
                }
                foreach (var marker in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelPlacementMarker>(true)))
                    SceneDefinitionAuthoring.ValidatePlacement(marker);
                if (pending.Length > 0 && !EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Cannot save migrated scene: " + path);
                return pending.Length;
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
