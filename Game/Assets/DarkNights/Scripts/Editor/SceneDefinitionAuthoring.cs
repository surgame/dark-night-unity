using System;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Editor.Objects.Runner;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>
    /// 为 YYGC 的 Definition 拖拽补充关卡实例参数、布局分组与被动外观预览。
    /// 只在显式创建时配置；验证发现 PrefabRef 过期时拒绝，不覆盖人工 Prefab 或场景修改。
    /// </summary>
    [InitializeOnLoad]
    public static class SceneDefinitionAuthoring
    {
        static SceneDefinitionAuthoring()
        {
            ObjectDefinitionDragHandler.SceneObjectCreated += OnCreated;
        }

        public static LevelPlacementMarker Create(ObjectDefinition definition, Transform parent, Vector3 position,
            string name, int order, int variant, string actorName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID));
            if (prefab == null) throw new InvalidOperationException("Definition PrefabRef is missing: " + definition.Key);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Create definition placement");
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localScale = Vector3.one * NativeVisual.PixelsPerUnit;
            ObjectDefinitionLoader loader = Undo.AddComponent<ObjectDefinitionLoader>(instance);
            loader.EditorConfigure(definition);
            return Configure(loader, order, variant, actorName);
        }

        public static LevelPlacementMarker Configure(ObjectDefinitionLoader loader, int order, int variant, string actorName, bool refreshPreview = true)
        {
            ObjectView view = loader.GetComponent<ObjectView>();
            if (view == null) throw new InvalidOperationException("Scene prefab is missing ObjectView.");
            var placement = loader.GetComponent<LevelPlacementMarker>();
            if (placement == null) placement = Undo.AddComponent<LevelPlacementMarker>(loader.gameObject);
            Undo.RecordObject(placement, "Configure scene placement");
            placement.EditorConfigure(loader, view, order, variant, actorName);
            ValidatePlacement(placement);
            if (refreshPreview) view.Get<NativeVisual>("visual").Preview(order, variant);
            EditorUtility.SetDirty(placement);
            PrefabUtility.RecordPrefabInstancePropertyModifications(loader.transform);
            foreach (SpriteRenderer sprite in loader.GetComponentsInChildren<SpriteRenderer>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(sprite);
            return placement;
        }

        public static void ValidatePlacement(LevelPlacementMarker placement)
        {
            if (placement.Loader == null || placement.View == null ||
                placement.Loader.gameObject != placement.gameObject || placement.View.gameObject != placement.gameObject)
                throw new InvalidOperationException("Placement must reference its own Loader and ObjectView: " + placement.name);
            DefinitionRuleIndex.Kind(placement.Loader.ResolveDefinition());
            if (!placement.Loader.EditorPrefabMatchesDefinition())
                throw new InvalidOperationException("Definition PrefabRef changed; explicitly update the scene instance: " + placement.name);
            if (placement.View.Get<NativeVisual>("visual") == null)
                throw new InvalidOperationException("Scene instance is missing its explicit visual binding: " + placement.name);
        }

        private static void OnCreated(ObjectDefinitionLoader loader)
        {
            ObjectDefinition definition = loader.ResolveDefinition();
            if (definition == null || DefinitionRuleIndex.TypeForKey(definition.Key) == GameCore.Objects.Types.ObjectType.None) return;
            LevelLayoutAuthoring[] layouts = loader.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelLayoutAuthoring>(true)).ToArray();
            if (layouts.Length != 1) throw new InvalidOperationException("Place a game definition in a scene with exactly one level authoring root.");
            LevelLayoutAuthoring layout = layouts[0];
            Transform group = layout.PlacementGroup(definition.Type);
            int order = group.GetComponentsInChildren<LevelPlacementMarker>(true).Select(value => value.SpawnOrder).DefaultIfEmpty(-1).Max() + 1;
            Undo.SetTransformParent(loader.transform, group, "Group definition placement");
            Vector3 point = loader.transform.position;
            point.y = layout.GroundPoint.y;
            loader.transform.position = point;
            loader.transform.localScale = Vector3.one * NativeVisual.PixelsPerUnit;
            Configure(loader, order, 0, "");
        }
    }
}
