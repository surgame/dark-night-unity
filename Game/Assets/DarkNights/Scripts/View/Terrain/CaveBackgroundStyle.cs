using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>静态三层候选风格的显示选项；参数不拥有玩法状态，原风格不配置此资源时继续使用既有后壁。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Cave background style")]
    public sealed class CaveBackgroundStyle : ScriptableObject
    {
        public bool ContourStatic = true;
        [Range(0, 4)] public int MiddleSoftness = 2;
        public bool Near = true;
        public bool Middle = true;
        public bool Deep = true;
        public Texture2D SourceMaskAtlas;
        public string ContentHash = Core.Config.Terrain.BackgroundBakeDescriptor.StyleContentHash;
        [Tooltip("独立点缀布局算法；空引用保留旧 v16.1。关闭整层请取消 Contour Static。")]
        public CaveBackgroundGeneratorAsset Generator;
        public CaveModifierAsset[] NearModifiers = System.Array.Empty<CaveModifierAsset>();
        public CaveModifierAsset[] MiddleModifiers = System.Array.Empty<CaveModifierAsset>();
        public CaveModifierAsset[] DeepModifiers = System.Array.Empty<CaveModifierAsset>();
        public Core.Logic.Terrain.ICaveBackgroundGenerator CaptureGenerator()
            => Generator != null ? Generator.Capture() : new Core.Logic.Terrain.ContourBackgroundGenerator();
        public Core.Logic.Terrain.CaveModifierStack[] CaptureModifiers() => new[] {
            CaveModifierAsset.CaptureStack(NearModifiers), CaveModifierAsset.CaptureStack(MiddleModifiers), CaveModifierAsset.CaptureStack(DeepModifiers) };
        public string VisualIdentity => string.Join("|", ContourStatic, ContentHash, MiddleSoftness, Near, Middle, Deep,
            CaptureGenerator().Identity, CaveModifierAsset.CaptureStack(NearModifiers).Identity,
            CaveModifierAsset.CaptureStack(MiddleModifiers).Identity, CaveModifierAsset.CaptureStack(DeepModifiers).Identity);
    }
}
