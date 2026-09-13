namespace DarkNights.Runtime.Diagnostics
{
    /// <summary>
    /// 单次权威会话的可选性能证据，记录包含命令处理的实际 60 Hz 步时、线程分配及发布载荷。
    /// 仅显式验收参数启用；PayloadBytes 是应用有效载荷，UDP 分片、协议头及重传由外部代理独立测量。
    /// </summary>
    public sealed class SessionMeasurements
    {
        public MeasurementSeries StepMilliseconds { get; } = new MeasurementSeries();
        public MeasurementSeries StepAllocatedBytes { get; } = new MeasurementSeries();
        public MeasurementSeries ProjectionMilliseconds { get; } = new MeasurementSeries();
        public MeasurementSeries ProjectionAllocatedBytes { get; } = new MeasurementSeries();
        public long Publications { get; private set; }
        public long PayloadBytes { get; private set; }
        public double MaximumBacklogSeconds { get; private set; }
        public bool ThreadAllocationCounterValid { get; }

        public SessionMeasurements()
        {
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[65536];
            ThreadAllocationCounterValid = System.GC.GetAllocatedBytesForCurrentThread() - before >= probe.Length;
            System.GC.KeepAlive(probe);
        }

        public void Step(double milliseconds, long bytes)
        {
            StepMilliseconds.Add(milliseconds);
            if (ThreadAllocationCounterValid) StepAllocatedBytes.Add(bytes);
        }
        public void Projection(double milliseconds, long bytes, int payloadBytes, int remoteObservers)
        {
            ProjectionMilliseconds.Add(milliseconds);
            if (ThreadAllocationCounterValid) ProjectionAllocatedBytes.Add(bytes);
            Publications++;
            PayloadBytes += (long)payloadBytes * remoteObservers;
        }
        public void Backlog(double seconds) => MaximumBacklogSeconds = System.Math.Max(MaximumBacklogSeconds, seconds);

        public void Clear()
        {
            StepMilliseconds.Clear();
            StepAllocatedBytes.Clear();
            ProjectionMilliseconds.Clear();
            ProjectionAllocatedBytes.Clear();
            Publications = PayloadBytes = 0;
            MaximumBacklogSeconds = 0;
        }
    }
}
