using System;
using System.Collections.Generic;
using AnyRules.Next;

namespace DarkNights.View.Terrain
{
    /// <summary>冻结源输入的有界队列；基线按完整区块槽位计费，生命周期可在等待恢复时连续到达。</summary>
    internal sealed class TerrainInputBatchQueue
    {
        private const int MaximumBatches = 64;
        private const long MaximumInputBytes = 32L * 1024 * 1024;
        private readonly Queue<MapInputBatch> batches = new Queue<MapInputBatch>();
        private readonly object gate = new object();
        private WorldDescriptor descriptor;
        private int maximumDeltaCells = 320 * 192;
        private int maximumBaselineSlots = 128 * 32 * 32;
        private long queuedBytes;
        private bool overflowed, identityKnown;
        private ulong receivedGeneration, receivedCommit, sourceSession, streamGeneration;
        private WorldIdentity world;

        public ulong ReceivedGeneration { get { lock (gate) return receivedGeneration; } }
        public ulong ReceivedCommit { get { lock (gate) return receivedCommit; } }
        public bool HasPending { get { lock (gate) return overflowed || batches.Count != 0; } }

        /// <summary>绑定已验证地图描述；负坐标和边缘 padding 都计入完整快照预算。</summary>
        public void Configure(WorldDescriptor value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            lock (gate)
            {
                if (descriptor != null && !descriptor.World.Equals(value.World))
                    throw new InvalidOperationException("输入队列不能跨世界复用。");
                int size = value.ChunkSize;
                long columns = (long)GridMath.FloorDiv((int)(value.Bounds.MaxUExclusive - 1), size) -
                    GridMath.FloorDiv(value.Bounds.MinU, size) + 1;
                long rows = (long)GridMath.FloorDiv((int)(value.Bounds.MaxVExclusive - 1), size) -
                    GridMath.FloorDiv(value.Bounds.MinV, size) + 1;
                long slots = checked(columns * rows * size * size);
                if (slots > int.MaxValue || slots * 16 > MaximumInputBytes)
                    throw new ArgumentOutOfRangeException(nameof(value), "地图完整基线超过本地输入内存预算。");
                descriptor = value;
                maximumDeltaCells = checked(value.Bounds.Width * value.Bounds.Height);
                maximumBaselineSlots = (int)slots;
            }
        }

        public void Enqueue(MapInputBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            lock (gate)
            {
                if (descriptor != null && !descriptor.World.Equals(batch.World))
                    throw new InvalidOperationException("输入属于另一个世界，必须新建表现宿主。");
                bool lifecycle = IsLifecycle(batch.Kind);
                if (identityKnown && batch.InputGeneration < receivedGeneration) return;
                bool sameGeneration = identityKnown && batch.InputGeneration == receivedGeneration;
                if (sameGeneration && (!batch.World.Equals(world) || batch.SourceSession != sourceSession ||
                    batch.StreamGeneration != streamGeneration))
                    throw new InvalidOperationException("源身份变化必须增加输入代次。");
                if (!lifecycle && sameGeneration && batch.SourceCommit < receivedCommit) return;
                if (!lifecycle && sameGeneration && batch.SourceCommit == receivedCommit &&
                    batch.Kind != MapInputBatchKind.Baseline) return;
                if (lifecycle && sameGeneration) return;

                long bytes = InputBytes(batch);
                int slots = CountInputCells(batch);
                if (batch.Cells.Count > maximumDeltaCells || slots > maximumBaselineSlots || bytes > MaximumInputBytes)
                    throw new ArgumentOutOfRangeException(nameof(batch), "单个输入批次超过地图预算；不反复重发相同超限基线。");

                world = batch.World; sourceSession = batch.SourceSession; streamGeneration = batch.StreamGeneration;
                receivedGeneration = batch.InputGeneration; receivedCommit = lifecycle ? 0 : batch.SourceCommit;
                identityKnown = true;
                if (batch.Kind == MapInputBatchKind.Baseline || lifecycle)
                {
                    batches.Clear(); queuedBytes = 0; overflowed = false;
                    batches.Enqueue(batch); queuedBytes = bytes;
                    return;
                }
                if (overflowed) return;
                if (TryCoalesce(batch)) return;
                if (batches.Count >= MaximumBatches || queuedBytes + bytes > MaximumInputBytes)
                { Overflow(); return; }
                batches.Enqueue(batch); queuedBytes += bytes;
            }
        }

        public bool TakeOverflow()
        {
            lock (gate) { bool result = overflowed; overflowed = false; return result; }
        }

        public bool TryDequeue(out MapInputBatch batch)
        {
            lock (gate)
            {
                if (batches.Count == 0) { batch = null; return false; }
                batch = batches.Dequeue(); queuedBytes -= InputBytes(batch); return true;
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                batches.Clear(); queuedBytes = 0; overflowed = identityKnown = false;
                receivedGeneration = receivedCommit = sourceSession = streamGeneration = 0;
                descriptor = null; world = default;
            }
        }

        private bool TryCoalesce(MapInputBatch incoming)
        {
            if (batches.Count == 0 || incoming.Kind != MapInputBatchKind.Delta) return false;
            var pending = new List<MapInputBatch>(batches);
            int suffixStart = pending.Count;
            while (suffixStart > 0 && pending[suffixStart - 1].Kind == MapInputBatchKind.Delta &&
                SameSource(pending[suffixStart - 1], incoming)) suffixStart--;
            if (suffixStart == pending.Count) return false;
            var latest = new Dictionary<CellCoord, MapInputCell>();
            long prefixBytes = 0;
            for (int i = 0; i < suffixStart; i++) prefixBytes += InputBytes(pending[i]);
            for (int i = suffixStart; i < pending.Count; i++)
                foreach (var cell in pending[i].Cells) latest[cell.Position] = cell;
            foreach (var cell in incoming.Cells) latest[cell.Position] = cell;
            if (latest.Count > maximumDeltaCells || prefixBytes + 128 + (long)latest.Count * 24 > MaximumInputBytes)
            { Overflow(); return true; }
            var cells = new List<MapInputCell>(latest.Values);
            cells.Sort((left, right) => left.Position.CompareTo(right.Position));
            var merged = new MapInputBatch(incoming.World, incoming.InputGeneration, incoming.SourceSession,
                incoming.StreamGeneration, incoming.SourceCommit, MapInputBatchKind.Delta, cells);
            batches.Clear();
            for (int i = 0; i < suffixStart; i++) batches.Enqueue(pending[i]);
            batches.Enqueue(merged); queuedBytes = prefixBytes + InputBytes(merged);
            return true;
        }

        private void Overflow() { batches.Clear(); queuedBytes = 0; overflowed = true; }
        private static bool SameSource(MapInputBatch left, MapInputBatch right) => left.World.Equals(right.World) &&
            left.InputGeneration == right.InputGeneration && left.SourceSession == right.SourceSession &&
            left.StreamGeneration == right.StreamGeneration && right.SourceCommit > left.SourceCommit;
        private static long InputBytes(MapInputBatch batch) => 128L + (long)batch.Cells.Count * 24 +
            ((long)CountInputCells(batch) - batch.Cells.Count) * 16 + (long)batch.SnapshotChunks.Count * 64;
        private static int CountInputCells(MapInputBatch batch)
        {
            int count = batch.Cells.Count;
            foreach (var snapshot in batch.SnapshotChunks) count = checked(count + snapshot.Cells.Count);
            return count;
        }
        private static bool IsLifecycle(MapInputBatchKind kind) => kind == MapInputBatchKind.Reset ||
            kind == MapInputBatchKind.VisibilityRevoked || kind == MapInputBatchKind.Disconnected;
    }
}
