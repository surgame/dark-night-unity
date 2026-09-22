using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
/// <summary>显式应用本次用户确认的 4/3/20/2 方向；仅修订本分支新资产，不覆盖旧人工地形资产。</summary>
public static class ApplyOutlineProfile
{
    public static string Run()
    {
        var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>("Assets/DarkNights/Res/Terrain/StrataCave/Style.asset");
        style.StoneSize = 4; style.OutlineMode = CaveOutlineMode.HybridB; style.OutlineSeed = "OUTLINE-0921";
        style.OutlineAmplitude = 3; style.OutlineWavelength = 20; style.OutlineQuantization = 2;
        EditorUtility.SetDirty(style);
        foreach (string path in new[] { "StrataCave/Background.asset", "CaveContourStatic/Background.asset" })
        {
            var bg = AssetDatabase.LoadAssetAtPath<CaveBackgroundStyle>("Assets/DarkNights/Res/Terrain/" + path);
            bg.ContentHash = BackgroundBakeDescriptor.StyleContentHash; EditorUtility.SetDirty(bg);
        }
        AssetDatabase.SaveAssets(); return style.VisualIdentity;
    }
}
