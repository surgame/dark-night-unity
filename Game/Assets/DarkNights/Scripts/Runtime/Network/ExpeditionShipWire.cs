using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>飞船网络冻结字段；只传输现有建筑状态的投影，不能在客户端驱动船体。</summary>
    [MemoryPackable]
    public partial class ExpeditionShipWire
    {
        public int Id { get; set; }
        public int Phase { get; set; }
        public int PilotId { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public double DoorClock { get; set; }
        public float DockX { get; set; }
        public float DockHeight { get; set; }
        public static ExpeditionShipWire From(ExpeditionShipData s) => s == null ? null : new ExpeditionShipWire
        { Id = s.Id, Phase = s.Phase, PilotId = s.PilotId, VelocityX = s.VelocityX, VelocityY = s.VelocityY,
            DoorClock = s.DoorClock, DockX = s.DockX, DockHeight = s.DockHeight };
        public ExpeditionShipData Freeze() => new ExpeditionShipData(Id, Phase, PilotId, VelocityX, VelocityY, DoorClock, DockX, DockHeight);
    }
}
