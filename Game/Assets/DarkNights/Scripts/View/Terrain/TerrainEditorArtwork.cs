using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>原生场景中可见的派生布局预览；只展示固定源的烘焙图，Play 时隐藏，由实际只读地形渲染替换。</summary>
    [ExecuteAlways]
    public sealed class TerrainEditorArtwork : MonoBehaviour
    {
        public SpriteRenderer Artwork;
        private void OnEnable() { if (Artwork != null) Artwork.enabled = !Application.isPlaying; }
    }
}
