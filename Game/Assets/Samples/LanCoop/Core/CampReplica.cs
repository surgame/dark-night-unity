namespace DarkNights.Samples.LanCoop.Core
{
    /// <summary>每次发布新建的只读展示副本；Host 和客户端均读取它，不保留 YYGC 池化状态引用。</summary>
    public sealed class CampReplica
    {
        public int Epoch { get; }
        public int Revision { get; }
        public int Coins { get; }
        public int Purchases { get; }
        public int Occupant { get; }
        public bool Paused { get; }
        public int SimulationTicks { get; }
        public CampReplica(int epoch, int revision, int coins, int purchases, int occupant, bool paused, int ticks)
        {
            Epoch = epoch;
            Revision = revision;
            Coins = coins;
            Purchases = purchases;
            Occupant = occupant;
            Paused = paused;
            SimulationTicks = ticks;
        }
    }
}
