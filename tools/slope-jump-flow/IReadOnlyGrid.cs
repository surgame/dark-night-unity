namespace AnyRules.Next
{
    /// <summary>源码探针只读取正式格编码；隔离适配不替代完整 AnyRules 地图接口或联网实现。</summary>
    public interface IReadOnlyGrid
    {
        GridSample Read(CellCoord position);
    }
}
