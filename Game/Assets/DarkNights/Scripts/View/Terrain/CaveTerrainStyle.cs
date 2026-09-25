using UnityEngine;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.View.Terrain
{
    /// <summary>洞穴表现配置；即时模式只改变当前前景求解合同，不改写原 Modifier 资产或冻结背景。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Cave terrain style")]
    public sealed class CaveTerrainStyle : ScriptableObject
    {
        public Shader Shader;
        public Texture2D Rock;
        public CaveBackgroundStyle Background;
        public bool ProceduralRock;
        [Tooltip("本修复候选使用有界 LocalV2 前景；关闭可对照原 LegacyV1。视觉签收前不要合并正式美术绑定。")]
        public bool ImmediateForeground = true;
        [Range(0, 8), Tooltip("热资源小范围修改的同步求解预算；超时自动转后台，不阻塞整帧等待。")]
        public float InteractiveBakeBudgetMs = 4;
        [Range(2, 12)] public int StoneSize = 4;
        public CaveOutlineMode OutlineMode = CaveOutlineMode.HybridB;
        public string OutlineSeed = "OUTLINE-0921";
        [Range(0, 8)] public float OutlineAmplitude = 3;
        [Range(8, 96)] public int OutlineWavelength = 20;
        [Range(1, 4)] public int OutlineQuantization = 2;
        [Tooltip("即时模式支持空栈或单个圆簇；其他栈明确报错，不悄悄回退全图烘焙。")]
        public CaveModifierAsset[] Modifiers = System.Array.Empty<CaveModifierAsset>();
        public string VisualIdentity => string.Join("|", ProceduralRock, StoneSize, OutlineMode, OutlineSeed,
            OutlineAmplitude.ToString(System.Globalization.CultureInfo.InvariantCulture), OutlineWavelength, OutlineQuantization,
            CaptureModifiers().Identity, Background == null ? "none" : Background.VisualIdentity);
        public CaveModifierStack CaptureModifiers()
        {
            var original = CaveModifierAsset.CaptureStack(Modifiers);
            return ImmediateForeground && ProceduralRock ? original.AsLocalForeground() : original;
        }
        public CaveOutlineSettings CaptureOutline() => new CaveOutlineSettings(OutlineMode, OutlineSeed,
            OutlineAmplitude, OutlineWavelength, OutlineQuantization);
    }
}
