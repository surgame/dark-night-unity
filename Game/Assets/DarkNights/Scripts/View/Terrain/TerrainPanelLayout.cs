using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>工作台面板的逻辑像素布局；在低分辨率限制缩放与宽度，高 DPI 可独立放大，不改变游戏镜头。</summary>
    public readonly struct TerrainPanelLayout
    {
        public readonly float Scale;
        public readonly Rect Panel;
        public readonly int TabColumns;
        public TerrainPanelLayout(int width, int height, float requestedScale, float requestedWidth)
        {
            float automatic = Mathf.Clamp(height / 900f, 1, 2.4f);
            float fit = Mathf.Min(width / 400f, height / 480f);
            Scale = Mathf.Max(.5f, Mathf.Min(automatic * Mathf.Clamp(requestedScale, .75f, 1.75f), fit));
            float logicalWidth = width / Scale, logicalHeight = height / Scale;
            float panelWidth = Mathf.Clamp(requestedWidth, 280, 520);
            panelWidth = Mathf.Min(panelWidth, Mathf.Max(200, logicalWidth - 24));
            Panel = new Rect(12, 12, panelWidth, Mathf.Max(120, logicalHeight - 24));
            TabColumns = panelWidth < 360 ? 3 : 5;
        }
        public bool ContainsScreenPoint(Vector2 point, float screenHeight)
            => Panel.Contains(new Vector2(point.x, screenHeight - point.y) / Scale);
    }
}
