using System;
using System.Collections.Generic;
using AnyRules.Next;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>一段同步 YYGC 事务中的稀疏地图草稿；同格只保留一次清除，候选状态准备失败时不安装地图。</summary>
    internal sealed class TerrainMiningBatch
    {
        private readonly ARDMap map;
        private readonly Dictionary<CellCoord, GridCell> originals = new Dictionary<CellCoord, GridCell>();
        private readonly Action completed;
        private readonly Action<GridChangeSet> publish;
        private readonly List<GridChangeSet> notifications = new List<GridChangeSet>();

        internal TerrainMiningBatch(ARDMap map, ObjectMutationBatch mutations, Action completed, Action<GridChangeSet> publish)
        {
            this.map = map; this.completed = completed; this.publish = publish;
            mutations.BeforeCommit(Commit);
            mutations.OnRollback(completed);
            mutations.AfterCommit(Publish);
        }

        internal bool Contains(CellCoord cell) => originals.ContainsKey(cell);
        internal bool Clear(CellCoord cell)
        {
            if (Contains(cell) || !map.Read(cell).TryGetCell(out var value) || value.IsEmpty) return false;
            originals.Add(cell, value);
            return true;
        }
        internal void Notify(GridChangeSet change) => notifications.Add(change);

        private void Commit()
        {
            foreach (var original in originals)
                if (!map.Read(original.Key).TryGetCell(out var current) || !current.Equals(original.Value))
                    throw new InvalidOperationException("采矿地图草稿已被另一写入改变。");
            using var edit = map.BeginEdit(map.CommitId);
            foreach (var original in originals) edit.ClearTile(original.Key);
            edit.Commit();
        }

        private void Publish()
        {
            completed();
            foreach (GridChangeSet change in notifications) publish(change);
        }
    }
}
