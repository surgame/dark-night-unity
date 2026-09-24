using System;
using System.Collections.Generic;
using AnyRules.Next;

namespace DarkNights.View.Terrain
{
    /// <summary>冻结 Editor／网络输入的有界并发队列；只合并同源稀疏 Delta，基线与生命周期保持独立边界。</summary>
    internal sealed class TerrainInputBatchQueue
    {
        private const int MaximumBatches = 64;
        private const int MaximumCoalescedCells = 320 * 192;
        private const int MaximumQueuedCells = MaximumCoalescedCells * 2;
        private readonly Queue<MapInputBatch> batches = new Queue<MapInputBatch>();
        private readonly object gate = new object();
        private int queuedCells;
        private bool overflowed;
        private ulong receivedGeneration, receivedCommit;

        public ulong ReceivedGeneration { get { lock (gate) return receivedGeneration; } }
        public ulong ReceivedCommit { get { lock (gate) return receivedCommit; } }
        public bool HasPending { get { lock (gate) return overflowed || batches.Count != 0; } }

        public void Enqueue(MapInputBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            lock (gate)
            {
                if (batch.InputGeneration < receivedGeneration || batch.InputGeneration == receivedGeneration &&
                    batch.SourceCommit < receivedCommit) return;
                if (batch.InputGeneration == receivedGeneration && batch.SourceCommit == receivedCommit &&
                    (batch.Kind == MapInputBatchKind.Delta || batch.Kind == MapInputBatchKind.SnapshotUpdate)) return;
                if (batch.InputGeneration > receivedGeneration || batch.InputGeneration == receivedGeneration && batch.SourceCommit > receivedCommit)
                { receivedGeneration = batch.InputGeneration; receivedCommit = batch.SourceCommit; }
                if (batch.Kind == MapInputBatchKind.Baseline || IsLifecycle(batch.Kind))
                {
                    batches.Clear(); queuedCells = 0; overflowed = false;
                    int units = CountInputCells(batch);
                    if (units > MaximumCoalescedCells) { overflowed = true; return; }
                    batches.Enqueue(batch); queuedCells = units; return;
                }
                if (TryCoalesce(batch)) return;
                int added = CountInputCells(batch);
                if (batches.Count >= MaximumBatches || queuedCells + added > MaximumQueuedCells)
                { batches.Clear(); queuedCells = 0; overflowed = true; return; }
                batches.Enqueue(batch); queuedCells += added;
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
                batch = batches.Dequeue(); queuedCells = Math.Max(0, queuedCells - CountInputCells(batch)); return true;
            }
        }

        public void Clear()
        {
            lock (gate) { batches.Clear(); queuedCells = 0; overflowed = false; }
        }

        private bool TryCoalesce(MapInputBatch incoming)
        {
            if (batches.Count == 0 || incoming.Kind != MapInputBatchKind.Delta) return false;
            var pending = new List<MapInputBatch>(batches);
            int suffixStart = pending.Count;
            while (suffixStart > 0 && pending[suffixStart - 1].Kind == MapInputBatchKind.Delta) suffixStart--;
            if (suffixStart == pending.Count) return false;
            var latest = new Dictionary<CellCoord, MapInputCell>();
            int prefixCells = 0;
            for (int i = 0; i < suffixStart; i++) prefixCells = checked(prefixCells + CountInputCells(pending[i]));
            for (int i = suffixStart; i < pending.Count; i++)
            {
                var queued = pending[i];
                if (queued.Kind != MapInputBatchKind.Delta || !SameSource(queued, incoming)) return false;
                foreach (var cell in queued.Cells) latest[cell.Position] = cell;
            }
            foreach (var cell in incoming.Cells) latest[cell.Position] = cell;
            if (latest.Count > MaximumCoalescedCells) { batches.Clear(); queuedCells = 0; overflowed = true; return true; }
            var cells = new List<MapInputCell>(latest.Values);
            cells.Sort((left, right) => left.Position.CompareTo(right.Position));
            batches.Clear();
            var merged = new MapInputBatch(incoming.World, incoming.InputGeneration, incoming.SourceSession,
                incoming.StreamGeneration, incoming.SourceCommit, MapInputBatchKind.Delta, cells);
            for (int i = 0; i < suffixStart; i++) batches.Enqueue(pending[i]);
            batches.Enqueue(merged); queuedCells = checked(prefixCells + merged.Cells.Count); return true;
        }

        private static bool SameSource(MapInputBatch left, MapInputBatch right) => left.World.Equals(right.World) &&
            left.InputGeneration == right.InputGeneration && left.SourceSession == right.SourceSession &&
            left.StreamGeneration == right.StreamGeneration && right.SourceCommit > left.SourceCommit;

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
