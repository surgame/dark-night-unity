using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>白模上一个只读制作标记；格范围使用U／行坐标，类别控制显示，标签与形状共同表达语义，不拥有业务状态。</summary>
    internal sealed class TerrainDesignMarker
    {
        internal readonly Rect Cells;
        internal readonly Color Color;
        internal readonly string Label;
        internal readonly bool Scenery;
        internal TerrainDesignMarker(Rect cells, Color color, string label, bool scenery = false)
        { Cells = cells; Color = color; Label = label; Scenery = scenery; }
    }
}
