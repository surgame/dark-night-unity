using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>落镐时验证本轮冻结目标及最近表面，区分岩壁与矿床；仅在完成前预检容量，耐久与奖励通过同一对象事务提交。</summary>
    internal static class HeroMining
    {
        internal static HeroMiningTarget InputTarget(ActorState state) => new HeroMiningTarget(state.MiningWorldId,
            state.MiningMapEpoch, state.MiningU, state.MiningV, state.MiningTileId, state.MiningFlags,
            state.MiningTargetKind, state.MiningEntityId, state.MiningContentVersion);

        internal static bool TryMine(ActorBehaviour actor, HeroMiningTarget intent, float aim)
        {
            var map = actor.World.Terrain?.Map;
            var state = actor.Read(); var tool = actor.World.Projectiles.Settings;
            if (map == null || actor.RuleKey != "worker" || state.Hp <= 0 || !intent.Present ||
                intent.WorldId != map.World.WorldId.ToString().Replace("-", "") || intent.MapEpoch != map.World.Epoch ||
                !ExpeditionCargo.CanMine(actor)) return false;
            var target = new CellCoord(intent.U, intent.V);
            if (!map.Descriptor.Bounds.Contains(target) || !map.Read(target).TryGetCell(out var cell) ||
                cell.TileId != intent.TileId || cell.Flags != intent.Flags ||
                map.ContentVersion(target) != intent.ContentVersion) return false;
            float radians = aim * (float)Math.PI / 180;
            float dx = (float)Math.Cos(radians), dh = (float)Math.Sin(radians);
            float hand = state.Height + tool.PickaxeHandHeight;
            bool wall = TerrainMiningQuery.FirstSurface(map, state.X, hand, dx, dh, tool.PickaxeReach,
                out var first, out float wallDistance);
            if (intent.Kind == HeroMiningTargetKind.Foreground)
            {
                if (!wall || !first.Equals(target) || cell.IsEmpty || intent.EntityId != 0 || !map.Rules.CanDamage(cell.TileId)) return false;
                int damage = map.Rules.PickaxeDamage(cell.TileId, tool.PickaxeDamage);
                var drop = map.Rules.Drop(cell.TileId);
                if (map.Query(target).State.Durability <= damage && !ExpeditionCargo.CanCollect(actor, drop.Amount)) return false;
                if (!map.StageDamage(target, damage, out bool destroyed)) return false;
                if (destroyed && drop.Amount > 0) ExpeditionCargo.Collect(actor, drop.Resource, drop.Amount);
                return true;
            }
            if (intent.Kind != HeroMiningTargetKind.MineralDeposit || !cell.IsEmpty ||
                !TerrainMiningGeometry.RayCell(state.X, hand, dx, dh, tool.PickaxeReach,
                    target.U, target.V, TerrainCellShape.Full, out float depositDistance) ||
                wall && wallDistance <= depositDistance) return false;
            var deposit = actor.World.Index.MineralDeposits.OfType<MineralDepositBehaviour>()
                .SingleOrDefault(value => value.Id == intent.EntityId);
            if (deposit == null || deposit.Remaining <= 0 || (int)Math.Floor(deposit.X / PlayableTerrain.CellPixels) != target.U ||
                -deposit.Y != target.V || deposit.Durability <= tool.PickaxeDamage && !ExpeditionCargo.CanCollect(actor, deposit.HarvestAmount))
                return false;
            if (!deposit.HitByHand(tool.PickaxeDamage, out int harvested)) return false;
            if (harvested > 0) ExpeditionCargo.Collect(actor, deposit.ResourceId, harvested);
            return true;
        }
    }
}
