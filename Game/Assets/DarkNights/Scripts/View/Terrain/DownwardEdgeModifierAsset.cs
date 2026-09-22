using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>下坠岩齿可编辑资产；默认保留用户确认的 7/100/5/0/89，长而尖的变体可另存为冰层形状。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Terrain modifiers/下坠岩齿")]
    public sealed class DownwardEdgeModifierAsset : CaveModifierAsset
    {
        [Tooltip("留空跟随地图种子；填写后独立控制形态。")] public string Seed = "";
        [Range(0, 24), InspectorName("最长下垂（px）")] public int Length = 7;
        [Range(0, 100), InspectorName("出现密度（%）")] public int Density = 100;
        [Range(3, 19), InspectorName("根部宽度（px）")] public int Width = 5;
        [Range(0, 100), InspectorName("尖锐度（%）")] public int Sharpness = 0;
        [Range(0, 100), InspectorName("形态起伏（%）")] public int Variation = 89;
        public override ICaveMaskModifier Capture() => new DownwardEdgeModifier(Length, Density, Width, Sharpness, Variation, Seed);
    }
}
