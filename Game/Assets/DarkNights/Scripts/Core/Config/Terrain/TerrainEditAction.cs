namespace DarkNights.Core.Config.Terrain
{
    /// <summary>服务端从可信装备状态推导出的地形动作；客户端请求只携带中心格，不能提交动作参数。</summary>
    public enum TerrainEditAction
    {
        HandMine = 1,
        Explosive = 2
    }
}
