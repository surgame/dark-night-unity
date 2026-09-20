using System;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>手持矿镐的服务端结算；按权威瞄准射线寻找首个岩格或矿床，状态与奖励仍归所属会话。</summary>
    internal static class HeroMining
    {
        internal static bool TryDeposit(ActorBehaviour actor, int targetId)
        {
            var deposit = actor.World.Index.Find<MineralDepositBehaviour>(targetId);
            if (deposit == null || actor.RuleKey != "worker") return false;
            var state = actor.Edit();
            double height = actor.World.Terrain == null ? 0 :
                PlayableTerrain.OriginY - (deposit.Y + .5f) * PlayableTerrain.CellPixels;
            float reach = actor.World.Catalog.Balance.HeroControl.WorkReach;
            if (Math.Abs(actor.X - deposit.X) > reach || Math.Abs(state.Height - height) > reach ||
                state.EquipmentCooldown > 0) return false;
            var map = actor.World.Terrain?.Map;
            if (!ExpeditionCargo.CanMine(actor) || map != null && !ExpeditionNavigation.Sight(map, actor.X, state.Height + 9, deposit.X, (float)height)) return false;
            if (!deposit.ExtractByHand()) return false;
            state.EquipmentCooldown = actor.World.Projectiles.Settings.PickaxeSeconds;
            ExpeditionCargo.Collect(actor, deposit.ResourceId);
            return true;
        }

        internal static void TryMine(ActorBehaviour actor)
        {
            var map = actor.World.Terrain?.Map;
            if (map == null) return;
            var state = actor.Edit();
            double radians = state.AimAngle * Math.PI / 180;
            float reach = actor.World.Catalog.Balance.HeroControl.WorkReach;
            for (float d = 0; d <= reach; d += 2)
            {
                float x = actor.X + (float)Math.Cos(radians) * d;
                float h = state.Height + 9 + (float)Math.Sin(radians) * d;
                var cell = new CellCoord((int)Math.Floor(x / PlayableTerrain.CellPixels + .5f),
                    (int)Math.Floor((h - PlayableTerrain.OriginY) / PlayableTerrain.CellPixels + .5f));
                if (!map.Descriptor.Bounds.Contains(cell)) return;
                if (Terrain.TerrainHeroMotion.Solid(map, x, h))
                {
                    try
                    {
                        var targets = map.BuildTargets(TerrainEditAction.HandMine, cell);
                        string resource = map.ResourceAt(cell);
                        map.DestroyTrusted(state.ControllerGeneration, "pick:" + actor.Id + ":" + map.CommitId,
                            TerrainEditAction.HandMine, map.World, cell, targets, _ => true, out bool applied);
                        if (applied && resource.Length != 0 && !actor.World.IsExpedition) actor.World.Economy.AddResource(resource, 1);
                    }
                    catch (InvalidOperationException) { }
                    return;
                }
                foreach (var deposit in actor.World.Index.MineralDeposits)
                    if (deposit is MineralDepositBehaviour mineral && Math.Abs(mineral.X - x) < 8 &&
                        Math.Abs(PlayableTerrain.OriginY - (mineral.Y + .5f) * PlayableTerrain.CellPixels - h) < 16 &&
                        TryDeposit(actor, mineral.Id)) return;
            }
        }
    }
}
