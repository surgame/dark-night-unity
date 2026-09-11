using System;
using System.Collections.Generic;
using System.Threading;

namespace DarkNights.Runtime.Session
{
    /// <summary>
    /// 将未缩放的真实秒数累积成权威 60 Hz 单步，每次调用限制追帧次数并保留余量，不静默丢弃模拟时间。
    /// 暂停和加载期间仍执行会话 tick；倍速仅由 Core 应用。生命周期属于同线程的会话装配，不访问 Unity Time。
    /// </summary>
    public sealed class SessionClock
    {
        public const double MaximumBacklogSeconds = 86400;
        private readonly SessionAuthority session;
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private double pendingTicks;
        public int MaximumSteps { get; }
        public int LastSteps { get; private set; }
        public double PendingSeconds => pendingTicks / 60;
        public Action<double, long> MeasureStep { get; set; }

        public SessionClock(SessionAuthority session, int maximumSteps = 8)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            session.CheckThread();
            if (maximumSteps < 1 || maximumSteps > 600) throw new ArgumentOutOfRangeException(nameof(maximumSteps));
            MaximumSteps = maximumSteps;
        }

        public IReadOnlyList<SessionReceipt> Advance(double unscaledSeconds)
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread)
                throw new InvalidOperationException("Clock must run on its owning thread.");
            if (unscaledSeconds < 0 || double.IsNaN(unscaledSeconds) || double.IsInfinity(unscaledSeconds) ||
                unscaledSeconds > MaximumBacklogSeconds - PendingSeconds)
                throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            LastSteps = 0;
            if (session.Closed)
            {
                pendingTicks = 0;
                return Array.Empty<SessionReceipt>();
            }
            pendingTicks += unscaledSeconds * 60;
            var results = new List<SessionReceipt>();
            // 只容忍 binary64 累加误差，不用帧 delta 改变规则步长。
            while (pendingTicks + 1e-9 >= 1 && LastSteps < MaximumSteps)
            {
                long started = MeasureStep == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
                long allocated = MeasureStep == null ? 0 : GC.GetAllocatedBytesForCurrentThread();
                var receipts = session.Tick();
                if (MeasureStep != null) MeasureStep((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 /
                    System.Diagnostics.Stopwatch.Frequency, GC.GetAllocatedBytesForCurrentThread() - allocated);
                results.AddRange(receipts);
                pendingTicks = Math.Max(0, pendingTicks - 1);
                LastSteps++;
            }
            return results.AsReadOnly();
        }
    }
}
