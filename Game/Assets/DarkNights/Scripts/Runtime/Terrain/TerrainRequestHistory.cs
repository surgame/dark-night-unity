using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>显式地形命令的有界连接级回执；不拥有地图状态，正式主角输入另由既有租约及序号去重。</summary>
    internal sealed class TerrainRequestHistory
    {
        private readonly Dictionary<int, Dictionary<string, (TerrainEditAction Action, CellCoord Center, string Fingerprint, GridCommitReceipt Receipt)>> records =
            new Dictionary<int, Dictionary<string, (TerrainEditAction, CellCoord, string, GridCommitReceipt)>>();
        private readonly Dictionary<int, Queue<string>> order = new Dictionary<int, Queue<string>>();
        private readonly Dictionary<int, ulong> sequences = new Dictionary<int, ulong>();
        internal void RequireSequence(int connection, ulong sequence)
        {
            if (sequence == 0 || sequences.TryGetValue(connection, out var previous) && sequence <= previous)
                throw new InvalidOperationException("地图输入序号已过期。");
        }
        internal void Advance(int connection, ulong sequence) => sequences[connection] = sequence;
        internal bool Find(int connection, string request, CellCoord center, out TerrainEditAction action, out GridCommitReceipt receipt)
        {
            action = default; receipt = null;
            if (!records.TryGetValue(connection, out var values) || !values.TryGetValue(request, out var value)) return false;
            if (value.Center != center) throw new InvalidOperationException("重复请求的目标已改变。");
            action = value.Action; receipt = value.Receipt; return true;
        }
        internal bool Find(int connection, string request, TerrainEditAction action, CellCoord center, string fingerprint, out GridCommitReceipt receipt)
        {
            if (!Find(connection, request, center, out var previous, out receipt)) return false;
            if (previous != action || records[connection][request].Fingerprint != fingerprint)
                throw new InvalidOperationException("重复请求的参数已改变。");
            return true;
        }
        internal void Remember(int connection, string request, TerrainEditAction action, CellCoord center, string fingerprint, GridCommitReceipt receipt)
        {
            if (!records.TryGetValue(connection, out var values))
            {
                records.Add(connection, values = new Dictionary<string, (TerrainEditAction, CellCoord, string, GridCommitReceipt)>());
                order.Add(connection, new Queue<string>());
            }
            values.Add(request, (action, center, fingerprint, receipt)); order[connection].Enqueue(request);
            while (order[connection].Count > 64) values.Remove(order[connection].Dequeue());
        }
        internal void Forget(int connection) { records.Remove(connection); order.Remove(connection); sequences.Remove(connection); }
        internal void Clear() { records.Clear(); order.Clear(); sequences.Clear(); }
    }
}
