using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>v16.1 轮廓跟随点缀的可替换配置；密度为零时关闭该层生成，显示开关及下缘 modifier 另归背景样式。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Background generators/轮廓跟随点缀")]
    public sealed class ContourBackgroundGeneratorAsset : CaveBackgroundGeneratorAsset
    {
        public string Seed = "DECOR-0922";
        [Header("近层：密度 / 范围 / 贴边")]
        [Range(0, 100)] public int NearAmount = 58;
        [Range(1, 120)] public int NearWidth = 24;
        [Range(0, 100)] public int NearBlend = 72;
        [Header("中层：密度 / 范围 / 贴边")]
        [Range(0, 100)] public int MiddleAmount = 72;
        [Range(1, 120)] public int MiddleWidth = 40;
        [Range(0, 100)] public int MiddleBlend = 52;
        [Header("深层：密度 / 范围 / 贴边")]
        [Range(0, 100)] public int DeepAmount = 48;
        [Range(1, 120)] public int DeepWidth = 10;
        [Range(0, 100)] public int DeepBlend = 68;
        public override ICaveBackgroundGenerator Capture() => new ContourBackgroundGenerator(new BackgroundContourSettings(Seed,
            NearAmount, NearWidth, NearBlend, MiddleAmount, MiddleWidth, MiddleBlend, DeepAmount, DeepWidth, DeepBlend));
    }
}
