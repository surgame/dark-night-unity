using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>运行时工作台的编辑器保存适配；只保存正式生成参数和岩壁样式，不写入单局格子。</summary>
    [InitializeOnLoad]
    public static class TerrainWorkbenchAssetBridge
    {
        private static readonly Dictionary<Type, ScriptableObject[]> Catalog = new Dictionary<Type, ScriptableObject[]>();
        static TerrainWorkbenchAssetBridge()
        {
            TerrainWorkbenchAssets.Query = Query;
            TerrainWorkbenchAssets.SaveStyle = SaveStyle;
            TerrainWorkbenchAssets.SaveGeneration = SaveGeneration;
            EditorApplication.projectChanged += Catalog.Clear;
        }
        private static ScriptableObject[] Query(Type type)
        {
            if (!Catalog.TryGetValue(type, out var assets))
            {
                assets = AssetDatabase.FindAssets("t:" + type.Name, new[] { "Assets/DarkNights/Res/Terrain" })
                    .Select(guid => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type) as ScriptableObject)
                    .Where(asset => asset != null).OrderBy(asset => asset.name).ToArray();
                Catalog.Add(type, assets);
            }
            return assets;
        }
        public static void SaveGeneration(ObjectDefinition source, string baseline, TerrainGenerationSettings settings)
        {
            var config = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            if (JsonUtility.ToJson(config.CaveMap) != baseline)
                throw new InvalidOperationException("正式地图配置已变化；请重新打开工作台后再保存。");
            var copy = settings.CopyValidated();
            if (copy.ResourceProfile != TerrainGenerationSettings.CaveExplorationProfile)
                throw new InvalidOperationException("正式地图必须使用洞穴生成。");
            Undo.RecordObject(source, "应用共用洞穴生成参数");
            config.CaveMap = copy;
            EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source);
        }

        public static void SaveStyle(CaveStyleDraft runtime)
        {
            foreach (var item in runtime.Items)
                if (!AssetDatabase.Contains(item.Source)) throw new InvalidOperationException("运行时临时资产不能作为原资产保存。");
            using var drafts = new TerrainStyleDrafts();
            drafts.Import(runtime); drafts.Apply();
            foreach (var item in runtime.Items)
            {
                EditorUtility.CopySerialized(item.Source, item.Working);
                EditorUtility.CopySerialized(item.Source, item.Original);
                EditorUtility.CopySerialized(item.Source, item.Baseline);
                item.Working.hideFlags = item.Original.hideFlags = item.Baseline.hideFlags = HideFlags.HideAndDontSave;
            }
        }
    }
}
