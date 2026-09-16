namespace DarkNights.Core.Config
{
    /// <summary>
    /// 场景中一块单向平台的冻结几何，正高度表示距地面向上；身份只在当前布局内有效。
    /// 只阻挡下落，水平移动和上升可穿过，不参与旧营地建筑占地或资源结算。
    /// </summary>
    public sealed class PlatformDefinition
    {
        public int Id { get; }
        public float MinX { get; }
        public float MaxX { get; }
        public float Height { get; }
        public PlatformDefinition(int id, float minX, float maxX, float height)
        {
            Id = id; MinX = minX; MaxX = maxX; Height = height;
        }
        public bool Contains(float x) => x >= MinX && x <= MaxX;
    }
}
