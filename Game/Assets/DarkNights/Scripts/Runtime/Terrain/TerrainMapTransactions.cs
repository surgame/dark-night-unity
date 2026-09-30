using System;
using AnyRules.Next;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>权威地图与所属 YYGC 短事务的生命周期接线；只保留当前批次的候选，通知在全部状态安装后发布。</summary>
    internal sealed class TerrainMapTransactions
    {
        private readonly ARDMap map;
        private readonly Action<GridChangeSet> publish;
        private TerrainMiningBatch pending;
        private ObjectMutationBatch mutations;
        internal bool CanStage => mutations?.IsOpen == true && !mutations.Publishing;

        internal TerrainMapTransactions(ARDMap map, Action<GridChangeSet> publish)
        {
            this.map = map; this.publish = publish;
        }

        internal void Bind(ObjectMutationBatch owner) => mutations = owner;
        internal GridSample Read(CellCoord position) => pending?.Contains(position) == true ? GridSample.Empty : map.Read(position);
        internal bool Clear(CellCoord target)
        {
            if (!CanStage) return false;
            if (pending == null) pending = new TerrainMiningBatch(map, mutations, () => pending = null, publish);
            return pending.Clear(target);
        }

        internal void Notify(GridChangeSet change)
        {
            if (pending != null) pending.Notify(change);
            else publish(change);
        }
    }
}
