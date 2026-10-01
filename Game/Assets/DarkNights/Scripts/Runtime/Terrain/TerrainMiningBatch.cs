using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>YYGC 短事务内的稀疏耐久候选；同格命中累计，全部对象校验后安装地图，通知延迟到对象安装完成。</summary>
    internal sealed class TerrainMiningBatch
    {
        private readonly ARDMap map;
        private readonly Dictionary<CellCoord, (GridCell Cell, GridBusinessState Before, GridBusinessState After)> candidates =
            new Dictionary<CellCoord, (GridCell, GridBusinessState, GridBusinessState)>();
        private readonly Action completed;
        private readonly Action<GridChangeSet> publish;
        private readonly List<GridChangeSet> notifications = new List<GridChangeSet>(1);

        internal TerrainMiningBatch(ARDMap map, ObjectMutationBatch mutations, Action completed, Action<GridChangeSet> publish)
        {
            this.map = map; this.completed = completed; this.publish = publish;
            mutations.BeforeCommit(Commit); mutations.OnRollback(completed); mutations.AfterCommit(Publish);
        }
        internal GridSample Read(CellCoord cell)
        {
            if (!candidates.TryGetValue(cell, out var value)) return map.Read(cell);
            return value.After.Durability == 0 ? GridSample.Empty : GridSample.FromCell(value.Cell);
        }
        internal GridBusinessSample Query(CellCoord cell)
        {
            var source = map.Business.Query(cell);
            if (!candidates.TryGetValue(cell, out var value)) return source;
            return new GridBusinessSample(Read(cell), source.Terrain, source.Definition, value.After, true);
        }
        internal bool Damage(CellCoord cell, int damage, out bool destroyed)
        {
            destroyed = false;
            if (damage < 1 || !Read(cell).TryGetCell(out var current) || current.IsEmpty) return false;
            if (!candidates.TryGetValue(cell, out var value))
            {
                var state = map.Business.Query(cell).State;
                value = (current, state, state);
            }
            var next = value.After.WithDurability(Math.Max(0, value.After.Durability - damage));
            candidates[cell] = (value.Cell, value.Before, next);
            destroyed = next.Durability == 0; return true;
        }
        internal void Notify(GridChangeSet change) => notifications.Add(change);
        private void Commit()
        {
            foreach (var pair in candidates)
                if (!map.Read(pair.Key).TryGetCell(out var cell) || cell != pair.Value.Cell ||
                    map.Business.Query(pair.Key).State != pair.Value.Before)
                    throw new InvalidOperationException("格子或耐久候选已被其他写入改变。");
            using var edit = map.BeginEdit(map.CommitId);
            foreach (var pair in candidates)
                if (pair.Value.After.Durability == 0) edit.ClearTile(pair.Key);
                else edit.SetBusinessState(pair.Key, pair.Value.After);
            edit.Commit();
        }
        private void Publish() { completed(); foreach (var change in notifications) publish(change); }
    }
}
