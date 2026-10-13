using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>地质事件的有界地形预检和同步移格事务；只从声明的松岩移到空格，保护基岩、矿格、身体占位和完整地面支撑。事件状态和前景在同一对象事务安装。</summary>
    internal static class GeologyLanding
    {
        internal static string Check(ObjectSession world, GeologyEventConfig config, bool includeActors)
        {
            var map = world.Terrain.Map;
            uint tile = map.Tiles.ByKey(config.Material);
            if (!map.Rules.CanDamage(tile) || map.Rules.Drop(tile).Amount != 0) return "瓦砾材质不支持安全清障";
            for (int index = 0; index < config.Width; index++)
            {
                var source = config.Source(index); var landing = config.Landing(index);
                if (!map.Descriptor.Bounds.Contains(source) || !map.Descriptor.Bounds.Contains(landing) ||
                    !map.Read(source).TryGetCell(out var rock) || rock.TileId != tile || rock.Flags != 0 ||
                    !map.Read(landing).TryGetCell(out var empty) || !empty.IsEmpty)
                    return "松岩或落点已改变";
                if (!map.Read(new CellCoord(landing.U, landing.V - 1)).TryGetCell(out var floor) || floor.IsEmpty ||
                    TerrainShapeGeometry.Decode(floor.Flags) != TerrainCellShape.Full) return "落点缺少完整支撑";
                for (int v = landing.V + 1; v < source.V; v++)
                    if (!map.Read(new CellCoord(landing.U, v)).TryGetCell(out var corridor) || !corridor.IsEmpty)
                        return "落石路径已有结构，事件不穿透岩壁";
                if (world.Terrain.Minerals != null && (!world.Terrain.Minerals.Read(landing).TryGetCell(out var ore) || !ore.IsEmpty))
                    return "落点覆盖矿格";
                if (includeActors && world.Index.Actors.Any(actor => Occupies(actor, landing))) return "等待角色离开落点";
                if (includeActors && world.Index.Buildings.Any(building => CoversBuilding(building, landing))) return "落点覆盖建筑";
            }
            return "";
        }

        internal static void Stage(ObjectSession world, GeologyEventConfig config)
        {
            world.Mutations.RequireWriting();
            string reason = Check(world, config, true);
            if (reason.Length != 0) throw new InvalidOperationException(reason);
            var map = world.Terrain.Map;
            uint tile = map.Tiles.ByKey(config.Material);
            for (int index = 0; index < config.Width; index++)
            {
                if (!map.StageDamage(config.Source(index), int.MaxValue, out bool destroyed) || !destroyed ||
                    !map.StagePlacement(config.Landing(index), tile))
                    throw new InvalidOperationException("地质事件地形候选未完整暂存。");
            }
        }

        internal static bool Occupies(ActorBehaviour actor, CellCoord cell)
        {
            var state = actor.Read();
            if (state.Hp <= 0) return false;
            float x = TerrainMiningGeometry.CenterX(cell.U), y = TerrainMiningGeometry.CenterHeight(cell.V);
            return state.X + HeroControlDefinition.BodyHalfWidth > x - 8 && state.X - HeroControlDefinition.BodyHalfWidth < x + 8 &&
                state.Height + HeroControlDefinition.BodyHeight > y - 8 && state.Height < y + 8;
        }

        private static bool CoversBuilding(BuildingBehaviour building, CellCoord cell)
        {
            var state = building.Read();
            float x = TerrainMiningGeometry.CenterX(cell.U), y = TerrainMiningGeometry.CenterHeight(cell.V);
            return Math.Abs(state.X - x) < building.Definition.Width * .5f + PlayableTerrain.CellPixels * .5f &&
                y + 8 > state.Height && y - 8 < state.Height + 128;
        }
    }
}
