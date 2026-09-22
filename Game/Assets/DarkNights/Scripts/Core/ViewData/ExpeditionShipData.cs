namespace DarkNights.Core.ViewData
{
    /// <summary>飞船冻结副本；位置仍取建筑合同，驾驶占用仅用于显示，读档时撤销控制租约并停止推力。</summary>
    public sealed class ExpeditionShipData
    {
        public int Id { get; }
        public int Phase { get; }
        public int PilotId { get; }
        public float VelocityX { get; }
        public float VelocityY { get; }
        public double DoorClock { get; }
        public float DockX { get; }
        public float DockHeight { get; }

        public ExpeditionShipData(int id, int phase, int pilotId, float velocityX, float velocityY, double doorClock, float dockX, float dockHeight)
        {
            Id = id; Phase = phase; PilotId = pilotId; VelocityX = velocityX; VelocityY = velocityY;
            DoorClock = doorClock; DockX = dockX; DockHeight = dockHeight;
        }
    }
}
