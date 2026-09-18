using System.Collections.Generic;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 由服务端会话签发的游戏参与者能力，不是 FishNet 连接；不可由请求中的槽位或 PlayerId 重新构造。
    /// 每槽只有一个有效引用；重连替换引用并增加代次。Ready 与结果窗口归会话线程唯一管理。
    /// </summary>
    public sealed class SessionConnection
    {
        public int PlayerSlot { get; }
        public int Generation { get; }
        public bool IsHost => PlayerSlot == 0;
        public bool Ready { get; internal set; }
        internal bool DefaultHeroRequested { get; set; }
        internal int DefaultHeroId { get; set; }
        internal bool DefaultHeroRecoveryPending { get; set; }
        internal int BaselineRevision { get; set; }
        internal long HighestSequence { get; set; }
        internal int PendingCount { get; set; }
        internal int ExplosiveCharges { get; set; } = 3;
        internal long LastTerrainActionTick { get; set; } = -1000;
        internal Dictionary<long, SessionCommandEntry> History { get; } = new Dictionary<long, SessionCommandEntry>();
        internal Queue<long> Completed { get; } = new Queue<long>();

        internal SessionConnection(int slot, int generation, int revision)
        {
            PlayerSlot = slot;
            Generation = generation;
            BaselineRevision = revision;
        }

        internal void ResetWorld(bool preserveDefaultHero = false)
        {
            Ready = false;
            DefaultHeroRequested = false;
            DefaultHeroRecoveryPending = preserveDefaultHero && DefaultHeroId > 0;
            if (!preserveDefaultHero) DefaultHeroId = 0;
            BaselineRevision = 0;
            HighestSequence = 0;
            PendingCount = 0;
            ExplosiveCharges = 3;
            LastTerrainActionTick = -1000;
            History.Clear();
            Completed.Clear();
        }

        internal void RememberCompleted(long sequence)
        {
            Completed.Enqueue(sequence);
            while (Completed.Count > SessionAuthority.ResultWindow) History.Remove(Completed.Dequeue());
        }
    }
}
