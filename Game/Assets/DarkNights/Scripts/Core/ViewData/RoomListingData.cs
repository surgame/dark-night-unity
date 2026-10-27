namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 房间浏览器的一次冻结发现结果，仅描述待加入房间及本次探测的可选往返时间。
    /// 地址不作为玩家身份或权限凭据；密码只暴露是否需要，不包含任何验证材料。
    /// </summary>
    public sealed class RoomListingData
    {
        public string Id { get; }
        public string Name { get; }
        public string Address { get; }
        public ushort Port { get; }
        public int Players { get; }
        public int? Ping { get; }
        public bool RequiresPassword { get; }
        public bool Compatible { get; }

        public RoomListingData(string id, string name, string address, ushort port,
            int players, int? ping, bool requiresPassword, bool compatible)
        {
            Id = id; Name = name; Address = address; Port = port;
            Players = players; Ping = ping; RequiresPassword = requiresPassword; Compatible = compatible;
        }
    }
}
