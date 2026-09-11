using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 会话元数据与完整世界的同切点展示帧；Publication 是单会话递增的发布序号，区别于规则 revision 和传输序号。
    /// 暂停、加载失败或连接变化可在相同 tick/revision 发布。此值不代表连接身份、握手或 Ready 授权。
    /// </summary>
    public sealed class SessionViewData
    {
        public long Publication { get; }
        public int Epoch { get; }
        public int Revision { get; }
        public long ServerTick { get; }
        public int PolicyRevision { get; }
        public bool HostOnly { get; }
        public int PlayerCount { get; }
        public int ReadyCount { get; }
        public bool Loading { get; }
        public bool Paused { get; }
        public int Speed { get; }
        public double Elapsed { get; }
        public WorldViewData World { get; }

        public SessionViewData(long publication, int epoch, int revision, long serverTick,
            int policyRevision, bool hostOnly, int playerCount, int readyCount, bool loading,
            bool paused, int speed, double elapsed, WorldViewData world)
        {
            if (publication <= 0 || epoch <= 0 || revision < 0 || serverTick < 0 || policyRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(publication));
            if (playerCount < 0 || playerCount > 4 || readyCount < 0 || readyCount > playerCount)
                throw new ArgumentOutOfRangeException(nameof(playerCount));
            if ((speed != 1 && speed != 2) || elapsed < 0 || double.IsNaN(elapsed) || double.IsInfinity(elapsed))
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            Publication = publication;
            Epoch = epoch;
            Revision = revision;
            ServerTick = serverTick;
            PolicyRevision = policyRevision;
            HostOnly = hostOnly;
            PlayerCount = playerCount;
            ReadyCount = readyCount;
            Loading = loading;
            Paused = paused;
            Speed = speed;
            Elapsed = elapsed;
            World = world ?? throw new ArgumentNullException(nameof(world));
        }
    }
}
