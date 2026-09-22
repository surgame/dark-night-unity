using System;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>显式安装本批 modifier 和点缀生成器首版资产；只在空目标创建，不覆盖已有调参，保留 Style 和 Background 原 GUID。</summary>
    public static class TerrainModifierInstaller
    {
        public const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        [MenuItem("Dark Nights/Art/安装地形 Modifier 首版")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            string[] names = { "DownwardRock", "DownwardIce", "RoundedRock", "RoundedDecor", "ContourDecor" };
            foreach (string name in names)
                if (AssetDatabase.LoadMainAssetAtPath(Root + "Modifiers/" + name + ".asset") != null)
                    throw new InvalidOperationException("已有 modifier 资产，停止首版安装以保留人工参数。");
            var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset");
            var background = AssetDatabase.LoadAssetAtPath<CaveBackgroundStyle>(Root + "Background.asset");
            if (style == null || background == null) throw new InvalidOperationException("缺少独立岩层样式。");
            if (!AssetDatabase.IsValidFolder(Root + "Modifiers")) AssetDatabase.CreateFolder(Root.TrimEnd('/'), "Modifiers");
            var rock = Create<DownwardEdgeModifierAsset>("DownwardRock");
            var ice = Create<DownwardEdgeModifierAsset>("DownwardIce");
            ice.Length = 15; ice.Width = 9; ice.Sharpness = 90; ice.Variation = 85;
            var rounded = Create<RoundedClusterModifierAsset>("RoundedRock");
            var decor = Create<RoundedClusterModifierAsset>("RoundedDecor"); decor.Grain = false;
            var generator = Create<ContourBackgroundGeneratorAsset>("ContourDecor");
            style.Modifiers = new CaveModifierAsset[] { rounded };
            background.Generator = generator;
            background.NearModifiers = new CaveModifierAsset[] { decor };
            background.MiddleModifiers = new CaveModifierAsset[] { decor };
            background.DeepModifiers = new CaveModifierAsset[] { decor };
            foreach (var asset in new UnityEngine.Object[] { rock, ice, rounded, decor, generator, style, background }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
        private static T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); asset.name = name;
            AssetDatabase.CreateAsset(asset, Root + "Modifiers/" + name + ".asset"); return asset;
        }
    }
}
