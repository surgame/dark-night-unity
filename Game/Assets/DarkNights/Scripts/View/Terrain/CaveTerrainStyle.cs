using UnityEngine;
namespace DarkNights.View.Terrain
{
    /// <summary>洞穴岩层的人工可编辑表现配置；不保存玩法状态，材质按每个预览独立实例化并释放。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Cave terrain style")]
    public sealed class CaveTerrainStyle : ScriptableObject
    {
        public Shader Shader;
        public Texture2D Rock;
    }
}
