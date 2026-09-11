using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 会话元数据与完整世界的同切点展示帧；Publication 是单会话递增的发布序号，区别于规则 revision 和传输序号。
    /// 暂停、加载失败或连接变化可在相同 tick/revision 发布。此值不代表连接身份、握手或 Ready 授权。
    /// </summary>
    public sealed class SessionViewData
    {
        public const int MaximumEvents = 128;
        public const int MaximumRemnants = 256;
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
        public IReadOnlyList<PresentationEvent> Events { get; }
        public IReadOnlyList<PresentationEvent> Remnants { get; }

        public SessionViewData(long publication, int epoch, int revision, long serverTick,
            int policyRevision, bool hostOnly, int playerCount, int readyCount, bool loading,
            bool paused, int speed, double elapsed, WorldViewData world, IReadOnlyList<PresentationEvent> events = null,
            IReadOnlyList<PresentationEvent> remnants = null)
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
            if (events != null && events.Count > MaximumEvents) throw new ArgumentOutOfRangeException(nameof(events));
            var copied = new List<PresentationEvent>(events ?? Array.Empty<PresentationEvent>());
            long previous = 0;
            foreach (PresentationEvent item in copied)
            {
                if (item == null || item.Sequence <= previous || item.Tick < 0 || item.Tick > serverTick)
                    throw new ArgumentException("Invalid presentation event order.", nameof(events));
                previous = item.Sequence;
            }
            Events = copied.AsReadOnly();
            if (remnants != null && remnants.Count > MaximumRemnants) throw new ArgumentOutOfRangeException(nameof(remnants));
            var active = new List<PresentationEvent>(remnants ?? Array.Empty<PresentationEvent>());
            previous = 0;
            foreach (PresentationEvent item in active)
            {
                if (item == null || item.Sequence <= previous || item.Tick < 0 || item.Tick > serverTick || item.Type != "effect" ||
                    item.Cue == null || (item.Cue.Kind != "corpse" && item.Cue.Kind != "rubble"))
                    throw new ArgumentException("Invalid active remnant order.", nameof(remnants));
                previous = item.Sequence;
            }
            Remnants = active.AsReadOnly();
        }
    }
}
