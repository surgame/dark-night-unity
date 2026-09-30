using System;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>矿镐唯一权威执行入口；只采冻结意图中的格子，地图和所属 YYGC 货物状态在同一同步事务提交。</summary>
    internal static class HeroMining
    {
        internal static bool TryMine(ActorBehaviour actor)
        {
            var map = actor.World.Terrain?.Map;
            ActorState state = actor.Read();
            if (map == null || actor.RuleKey != "worker" || string.IsNullOrEmpty(state.MiningWorldId) ||
                state.MiningWorldId != map.World.WorldId.ToString().Replace("-", "") || state.MiningMapEpoch != map.World.Epoch ||
                state.EquipmentCooldown > 0 || !ExpeditionCargo.CanMine(actor)) return false;
            var target = new CellCoord(state.MiningU, state.MiningV);
            if (!map.Read(target).TryGetCell(out var cell) || cell.TileId != state.MiningTileId || cell.Flags != state.MiningFlags ||
                !TerrainMiningQuery.Reachable(map, state.X, state.Height + actor.World.Projectiles.Settings.HandHeight,
                    target, actor.World.Catalog.Balance.HeroControl.WorkReach)) return false;
            if (!cell.IsEmpty)
            {
                string resource = map.ResourceAt(target);
                if (!map.StageMine(target)) return false;
                if (resource.Length != 0) ExpeditionCargo.Collect(actor, resource);
                return true;
            }
            foreach (var value in actor.World.Index.MineralDeposits)
                if (value is MineralDepositBehaviour deposit && deposit.Remaining > 0 &&
                    (int)Math.Floor(deposit.X / PlayableTerrain.CellPixels) == target.U && -(int)deposit.Y == target.V)
                {
                    if (!deposit.ExtractByHand()) return false;
                    ExpeditionCargo.Collect(actor, deposit.ResourceId);
                    return true;
                }
            return false;
        }
    }
}
