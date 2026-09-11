namespace DarkNights.View
{
    /// <summary>
    /// 当前客户端已装配实体外观的查询口；未加载或已释放时返回空，不提供可写世界状态或资源兜底。
    /// 实现由 Entry 持有，View 用于拾取、选择锚点和本地反馈。
    /// </summary>
    public interface IEntityVisuals
    {
        NativeVisual Visual(int id);
    }
}
