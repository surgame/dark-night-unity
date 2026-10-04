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
        internal bool SetResult(CellCoord target, GridCell cell, GridBusinessState state)
        {
            if (!CanStage) return false;
            if (pending == null) pending = new TerrainMiningBatch(map, mutations, () => pending = null, publish);
            return pending.SetResult(target, cell, state);
        }
        internal GridSample Read(CellCoord position) => pending?.Read(position) ?? map.Read(position);
        internal GridBusinessSample Query(CellCoord position) => pending?.Query(position) ?? map.Business.Query(position);
        internal bool Damage(CellCoord target, int amount, out bool destroyed)
        {
            destroyed = false;
            if (!CanStage) return false;
            if (pending == null) pending = new TerrainMiningBatch(map, mutations, () => pending = null, publish);
            return pending.Damage(target, amount, out destroyed);
        }

        internal void Notify(GridChangeSet change)
        {
            if (pending != null) pending.Notify(change);
            else publish(change);
        }
    }
}
