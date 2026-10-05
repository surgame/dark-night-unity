namespace DarkNights.View.Terrain
{
    /// <summary>工作台本地网格叠图模式；逻辑边界围绕整数格中心，渲染边界位于整数线上，两者相差半格且不改变地图状态。</summary>
    public enum TerrainGridMode
    {
        Logical,
        Render
    }
}
