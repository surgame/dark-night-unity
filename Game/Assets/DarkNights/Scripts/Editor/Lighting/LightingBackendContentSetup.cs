using System;
using System.Linq;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DarkNights.Editor.Lighting
{
    /// <summary>有限安装 URP 回退模板及共用光效引用；通过 Editor 序列化 API 保存锁定 URP 配置，保留作者资产和已有 GUID。</summary>
    public static class LightingBackendContentSetup
    {
        public const string Root = "Assets/DarkNights/Res/Shared/Lighting/";
        public const string LightPath = Root + "UrpEnvironmentLight.prefab";
        public const string ShadowPath = Root + "UrpTerrainShadow.prefab";

        [MenuItem("Dark Nights/Tools/安装照明后端资源")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("退出 Play 并等待编译完成后安装照明后端。");
            EnsureTemplates(LightPath, ShadowPath);
            var contents = PrefabUtility.LoadPrefabContents(ReusableLightContentSetup.EffectPath);
            try
            {
                var emitter = contents.GetComponent<LightEnvironmentEmitter>();
                NativePrefabBuilder.SetReference(emitter, "urpLightTemplate", AssetDatabase.LoadAssetAtPath<GameObject>(LightPath).GetComponent<Light2D>());
                NativePrefabBuilder.SetReference(emitter, "urpShadowTemplate", AssetDatabase.LoadAssetAtPath<GameObject>(ShadowPath).GetComponent<ShadowCaster2D>());
                PrefabUtility.SaveAsPrefabAsset(contents, ReusableLightContentSetup.EffectPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets(); Validate();
        }

        public static void EnsureTemplates(string lightPath, string shadowPath)
        {
            int[] layers = SortingLayer.layers.Select(layer => layer.id).ToArray();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(lightPath) == null)
            {
                var root = new GameObject("UrpEnvironmentLight"); root.SetActive(false);
                try
                {
                    var light = root.AddComponent<Light2D>(); light.lightType = Light2D.LightType.Sprite;
                    light.targetSortingLayers = layers; light.blendStyleIndex = 0; light.shadowsEnabled = true;
                    var serialized = new SerializedObject(light);
                    serialized.FindProperty("m_NormalMapQuality").intValue = 1;
                    serialized.FindProperty("m_NormalMapDistance").floatValue = 3;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, lightPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(shadowPath) == null)
            {
                var root = new GameObject("UrpTerrainShadow"); root.layer = 2;
                try
                {
                    var collider = root.AddComponent<PolygonCollider2D>();
                    collider.isTrigger = true; collider.excludeLayers = ~0; collider.callbackLayers = 0;
                    collider.pathCount = 0;
                    var caster = root.AddComponent<ShadowCaster2D>(); caster.selfShadows = false;
                    var serialized = new SerializedObject(caster);
                    var targetLayers = serialized.FindProperty("m_ApplyToSortingLayers"); targetLayers.arraySize = layers.Length;
                    for (int n = 0; n < layers.Length; n++) targetLayers.GetArrayElementAtIndex(n).intValue = layers[n];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    root.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root, shadowPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }

        public static void Validate()
        {
            var light = AssetDatabase.LoadAssetAtPath<GameObject>(LightPath)?.GetComponent<Light2D>();
            var shadow = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowPath)?.GetComponent<ShadowCaster2D>();
            var collider = shadow != null ? shadow.GetComponent<PolygonCollider2D>() : null;
            if (light == null || light.normalMapQuality == Light2D.NormalMapQuality.Disabled || collider == null || !collider.isTrigger || collider.excludeLayers.value != ~0)
                throw new InvalidOperationException("URP 后端模板缺少法线受光或隔离的遮挡几何来源。");
            var effect = AssetDatabase.LoadAssetAtPath<GameObject>(ReusableLightContentSetup.EffectPath).GetComponent<LightEffect>();
            if (effect.Environment.UrpLightTemplate != light || effect.Environment.UrpShadowTemplate != shadow)
                throw new InvalidOperationException("共用光效没有绑定本批 URP 后端模板。");
        }
    }
}
