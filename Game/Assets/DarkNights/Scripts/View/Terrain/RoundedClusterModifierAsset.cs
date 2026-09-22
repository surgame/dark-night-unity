using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>花菜圆簇可编辑资产；轮廓与局部岩粒可分开启用，参数仍为原生像素，不随镜头缩放改变。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Terrain modifiers/花菜圆簇")]
    public sealed class RoundedClusterModifierAsset : CaveModifierAsset
    {
        [Tooltip("留空跟随地图种子；填写后独立控制形态。")] public string Seed = "";
        [Range(0, 12), InspectorName("下缘厚度（px）")] public int Depth = 8;
        [Range(8, 30), InspectorName("岩簇宽度（px）")] public int Size = 20;
        [Range(2, 6), InspectorName("小瓣尺寸（px）")] public int Petal = 3;
        [Range(0, 100), InspectorName("出现密度（%）")] public int Density = 90;
        [Range(0, 100), InspectorName("形态起伏（%）")] public int Variation = 65;
        [InspectorName("融合局部岩粒")] public bool Grain = true;
        public override ICaveMaskModifier Capture() => new RoundedClusterModifier(Depth, Size, Petal, Density, Variation, Grain, Seed);
    }
}
