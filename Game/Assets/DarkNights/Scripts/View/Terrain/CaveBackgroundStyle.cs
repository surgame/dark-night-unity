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
    }
}
