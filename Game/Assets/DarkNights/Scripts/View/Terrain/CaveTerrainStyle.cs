using UnityEngine;
using DarkNights.Core.Config.Terrain;
namespace DarkNights.View.Terrain
{
    /// <summary>洞穴岩层的人工可编辑表现配置；不保存玩法状态，材质按每个预览独立实例化并释放。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Cave terrain style")]
    public sealed class CaveTerrainStyle : ScriptableObject
    {
        public Shader Shader;
        public Texture2D Rock;
        public CaveBackgroundStyle Background;
        public bool ProceduralRock;
        [Range(2, 12)] public int StoneSize = 4;
        public CaveOutlineMode OutlineMode = CaveOutlineMode.HybridB;
        public string OutlineSeed = "OUTLINE-0921";
        [Range(0, 8)] public float OutlineAmplitude = 3;
        [Range(8, 96)] public int OutlineWavelength = 20;
        [Range(1, 4)] public int OutlineQuantization = 2;
        public string VisualIdentity => string.Join("|", ProceduralRock, StoneSize, OutlineMode, OutlineSeed,
            OutlineAmplitude.ToString(System.Globalization.CultureInfo.InvariantCulture), OutlineWavelength, OutlineQuantization,
            Background == null ? "none" : string.Join(",", Background.ContentHash, Background.MiddleSoftness, Background.Near, Background.Middle, Background.Deep));
        public CaveOutlineSettings CaptureOutline() => new CaveOutlineSettings(OutlineMode, OutlineSeed,
            OutlineAmplitude, OutlineWavelength, OutlineQuantization);
    }
}
