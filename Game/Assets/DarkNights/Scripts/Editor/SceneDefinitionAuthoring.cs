using System;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Editor.Objects.Runner;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEngine;

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

        public static ScenePlacement Create(ObjectDefinition definition, Transform parent, Vector3 position,
            string name, int variant, string initialName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID));
            if (prefab == null) throw new InvalidOperationException("Definition PrefabRef is missing: " + definition.Key);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, "Create definition placement");
            instance.name = name;
            instance.transform.localPosition = position;
            instance.transform.localScale = Vector3.one * EntityView.PixelsPerUnit;
            ObjectDefinitionLoader loader = Undo.AddComponent<ObjectDefinitionLoader>(instance);
            loader.EditorConfigure(definition);
            return Configure(loader, variant, initialName);
        }

        public static ScenePlacement Configure(ObjectDefinitionLoader loader, int variant, string initialName, bool refreshPreview = true)
        {
            EntityView view = loader.GetComponent<EntityView>();
            if (view == null) throw new InvalidOperationException("Scene prefab is missing its EntityView primary view.");
            var placement = loader.GetComponent<ScenePlacement>();
            if (placement == null) placement = Undo.AddComponent<ScenePlacement>(loader.gameObject);
            Undo.RecordObject(placement, "Configure scene placement");
            placement.EditorConfigure(loader, view, variant, initialName);
            ValidatePlacement(placement);
            if (refreshPreview) view.Preview(loader.transform.GetSiblingIndex(), variant);
            EditorUtility.SetDirty(placement);
            PrefabUtility.RecordPrefabInstancePropertyModifications(loader.transform);
            foreach (SpriteRenderer sprite in loader.GetComponentsInChildren<SpriteRenderer>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(sprite);
            return placement;
        }

        public static void ValidatePlacement(ScenePlacement placement)
        {
            if (!Guid.TryParseExact(placement.PlacementKey, "N", out _) || placement.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ScenePlacement>(true))
                .Count(other => other.PlacementKey == placement.PlacementKey) != 1)
                throw new InvalidOperationException("Placement key is missing or duplicated: " + placement.name);
            if (placement.Loader == null || placement.View == null ||
                placement.Loader.gameObject != placement.gameObject || placement.View.gameObject != placement.gameObject)
                throw new InvalidOperationException("Placement must reference its own Loader and ObjectView: " + placement.name);
            DefinitionRuleIndex.RuleKey(placement.Loader.ResolveDefinition());
            if (!placement.Loader.EditorPrefabMatchesDefinition())
                throw new InvalidOperationException("Definition PrefabRef changed; explicitly update the scene instance: " + placement.name);
            if (placement.Loader.GetComponent<EntityView>() != placement.View)
                throw new InvalidOperationException("Scene instance does not use its EntityView as the primary view: " + placement.name);
        }

        private static void OnCreated(ObjectDefinitionLoader loader)
        {
            ObjectDefinition definition = loader.ResolveDefinition();
            if (definition == null || !DefinitionRuleIndex.IsEntityType(definition.Type)) return;
            LevelLayoutAuthoring[] layouts = loader.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelLayoutAuthoring>(true)).ToArray();
            if (layouts.Length != 1) throw new InvalidOperationException("Place a game definition in a scene with exactly one level authoring root.");
            LevelLayoutAuthoring layout = layouts[0];
            Transform group = layout.PlacementGroup(definition.Type);
            Undo.SetTransformParent(loader.transform, group, "Group definition placement");
            Vector3 point = loader.transform.position;
            point.y = layout.GroundPoint.y;
            loader.transform.position = point;
            loader.transform.localScale = Vector3.one * EntityView.PixelsPerUnit;
            Configure(loader, 0, "");
        }
    }
}
