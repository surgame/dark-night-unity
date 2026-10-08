using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using DarkNights.View;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>显式安装共用光效及头部挂载变体；保留现有手电和角色 GUID，不在普通开局或构建中改写人工资源。</summary>
    public static class ReusableLightContentSetup
    {
        public const string EffectPath = "Assets/DarkNights/Res/Shared/Lighting/LightEffect.prefab";
        public const string HeadPath = "Assets/DarkNights/Res/Shared/Lighting/HeadMountedLightEffect.prefab";

        [MenuItem("Dark Nights/Tools/安装可复用照明与手电道具")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("退出 Play 并等待编译完成后安装光效。");
            EnsureEffect(); EnsureHeadVariant();
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(FlashlightContentSetup.DefinitionPath);
            if (definition == null) throw new InvalidOperationException("先安装原生手电资产。");
            if (!definition.SharedConfigs.OfType<EquipmentItemConfig>().Any())
                definition.SharedConfigs.Add(new EquipmentItemConfig { Handheld = HeroEquipmentKind.Flashlight });
            EditorUtility.SetDirty(definition);
            AttachEffect(definition);
            var database = ObjectDefinitionDatabase.Instance;
            foreach (var actor in database.Definitions.Where(value => value != null &&
                value.BehaviourTypes.Contains(typeof(HeroControlBehaviour).FullName))) ConfigureActor(actor);
            AssetDatabase.SaveAssets(); Validate();
        }

        private static void EnsureEffect()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath) != null) return;
            var root = new GameObject("LightEffect");
            try
            {
                var effect = root.AddComponent<LightEffect>();
                var environment = root.AddComponent<LightEnvironmentEmitter>();
                var fill = root.AddComponent<LocalLightFill>();
                var mouth = new GameObject("Environment Origin").transform; mouth.SetParent(root.transform, false);
                var fillOrigin = new GameObject("Local Fill Origin").transform; fillOrigin.SetParent(root.transform, false);
                NativePrefabBuilder.SetReference(environment, "emitter", mouth);
                NativePrefabBuilder.SetReference(environment, "lightingShader", AssetDatabase.LoadAssetAtPath<ComputeShader>(
                    "Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute"));
                NativePrefabBuilder.SetReference(fill, "origin", fillOrigin);
                NativePrefabBuilder.SetReference(effect, "environment", environment);
                NativePrefabBuilder.SetReference(effect, "localFill", fill);
                effect.Validate(); PrefabUtility.SaveAsPrefabAsset(root, EffectPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void EnsureHeadVariant()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HeadPath) != null) return;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath));
            try
            {
                root.name = "HeadMountedLightEffect";
                var effect = root.GetComponent<LightEffect>();
                effect.Environment.Emitter.localPosition = new Vector3(.02f, 0, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(effect.Environment.Emitter);
                PrefabUtility.SaveAsPrefabAsset(root, HeadPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void AttachEffect(ObjectDefinition definition)
        {
            string path = AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<FlashlightView>();
                if (view == null) throw new InvalidOperationException("手电缺少原生主视图。");
                if (view.Effect == null)
                {
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath), root.transform);
                    child.transform.localPosition = Vector3.zero;
                    NativePrefabBuilder.SetReference(view, "effect", child.GetComponent<LightEffect>());
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureActor(ObjectDefinition definition)
        {
            string path = AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<ActorView>();
                if (view == null || view.LightAnchor == null) throw new InvalidOperationException("角色缺少原生视图或照明挂点。");
                if (view.LightFillAnchor != null && view.LightTargets.Length > 0) return;
                if (view.LightFillAnchor == null)
                {
                    var fillAnchor = new GameObject("Light Fill Anchor").transform;
                    fillAnchor.SetParent(view.transform, false); fillAnchor.localPosition = new Vector3(0, .22f, 0);
                    NativePrefabBuilder.SetReference(view, "lightFillAnchor", fillAnchor);
                }
                var serialized = new SerializedObject(view);
                var tint = serialized.FindProperty("tintTargets").FindPropertyRelative("targets");
                var receivers = Enumerable.Range(0, tint.arraySize).Select(index =>
                    (SpriteRenderer)tint.GetArrayElementAtIndex(index).FindPropertyRelative("renderer").objectReferenceValue).ToList();
                var handheld = serialized.FindProperty("handheld").objectReferenceValue;
                if (handheld != null)
                {
                    var binding = new SerializedObject(handheld);
                    receivers.Add((SpriteRenderer)binding.FindProperty("item").objectReferenceValue);
                    receivers.Add((SpriteRenderer)binding.FindProperty("arm").objectReferenceValue);
                }
                if (receivers.Count == 0 || receivers.Any(value => value == null))
                    throw new InvalidOperationException("角色补光必须使用完整的显式 Renderer 绑定。");
                var targets = serialized.FindProperty("lightTargets");
                var unique = receivers.Distinct().ToArray(); targets.arraySize = unique.Length;
                for (int index = 0; index < unique.Length; index++) targets.GetArrayElementAtIndex(index).objectReferenceValue = unique[index];
                serialized.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Validate()
        {
            foreach (string path in new[] { EffectPath, HeadPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<LightEffect>() == null)
                    throw new InvalidOperationException("缺少可复用照明 Prefab：" + path);
                prefab.GetComponent<LightEffect>().Validate();
            }
            FlashlightContentSetup.Validate();
        }
    }
}
