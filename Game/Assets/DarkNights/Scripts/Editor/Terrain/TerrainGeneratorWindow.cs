using System;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>旧版地图生成工作台；显式预览和新资产导出，参数变化不触发生成、网络或玩家存档访问。</summary>
    public sealed class TerrainGeneratorWindow : EditorWindow
    {
        private TerrainGenerationSettings settings = new TerrainGenerationSettings();
        private ARDMapDefinition definition;
        private TerrainBlueprint generated;
        private Texture2D preview;
        private string status = "生成预览后可导出新地图。";
        private Vector2 scroll;
        public static void Open() => GetWindow<TerrainGeneratorWindow>("地图生成器（旧版）");
        private void OnEnable() { definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(TerrainTestAssets.DefinitionPath); }
        private void OnDisable() { if (preview != null) DestroyImmediate(preview); }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("地图生成（旧版）· DualGrid / AnyRuleD", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("320×192 逻辑格。地形破坏归服务端；本工具只创建初始地图。预览使用材料色，Play 场景使用真实 AnyRuleD。", MessageType.Info);
            settings.Seed = EditorGUILayout.TextField("种子", settings.Seed);
            settings.Surface = TerrainGenerationSettings.SurfaceNames[EditorGUILayout.Popup("地表算法",
                Math.Max(0, Array.IndexOf(TerrainGenerationSettings.SurfaceNames, settings.Surface)), TerrainGenerationSettings.SurfaceNames)];
            settings.OrganicCaves = EditorGUILayout.Toggle("附加自然洞穴", settings.OrganicCaves);
            settings.Amplitude = EditorGUILayout.Slider("起伏", (float)settings.Amplitude, .3f, 1.6f);
            settings.OreDensity = EditorGUILayout.Slider("矿脉密度", (float)settings.OreDensity, .2f, 2);
            definition = (ARDMapDefinition)EditorGUILayout.ObjectField("AnyRuleD 配置", definition, typeof(ARDMapDefinition), false);
            if (GUILayout.Button("生成预览")) Try(Generate);
            using (new EditorGUI.DisabledScope(generated == null || definition == null))
                if (GUILayout.Button("导出当前预览为新地图…")) Try(Export);
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (preview != null) GUILayout.Label(preview, GUILayout.Width(640), GUILayout.Height(384));
            EditorGUILayout.EndScrollView();
        }
        private void Generate()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew(); generated = TerrainGenerator.Generate(settings);
            if (preview != null) DestroyImmediate(preview);
            preview = new Texture2D(generated.Width, generated.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            string[] colors = { "#183126", "#484538", "#505951", "#3b4542", "#bc9160", "#aab5a7", "#d7ba6c", "#778759", "#2d3734" };
            var palette = new Color[9]; for (int i = 0; i < 9; i++) ColorUtility.TryParseHtmlString(colors[i], out palette[i]);
            var pixels = new Color32[generated.Width * generated.Height];
            for (int y = 0; y < generated.Height; y++) for (int x = 0; x < generated.Width; x++)
                pixels[(generated.Height - y - 1) * generated.Width + x] = palette[generated.MaterialAt(x, y)];
            preview.SetPixels32(pixels); preview.Apply();
            status = generated.Settings.Seed + " · 8 洞室 / 7 连接 · " + watch.ElapsedMilliseconds + " ms（本机编辑器生成）";
        }
        private void Export()
        {
            string path = EditorUtility.SaveFilePanelInProject("导出新地图", "GeneratedTerrain", "asset", "选择新的地形资产路径。", TerrainTestAssets.Root + "/Maps");
            if (string.IsNullOrEmpty(path)) return;
            Selection.activeObject = TerrainMapExporter.Export(generated, definition, path);
            status = "已导出冻结预览：" + path;
        }
        private void Try(Action action)
        {
            try { action(); } catch (Exception e) { status = e.Message; Debug.LogException(e); }
        }
    }
}
