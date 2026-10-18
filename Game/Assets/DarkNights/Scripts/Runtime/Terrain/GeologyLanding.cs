using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>地质事件的有界地形预检和同步移格事务；只从声明的松岩移到空格，保护基岩、矿格、身体占位和完整地面支撑。事件状态和前景在同一对象事务安装。</summary>
    internal static class GeologyLanding
    {
        internal static string Check(ObjectSession world, GeologyEventConfig config, bool includeActors, bool detached = false)
        {
            var map = world.Terrain.Map;
            uint tile = map.Tiles.ByKey(config.Material);
            if (!map.Rules.CanDamage(tile) || map.Rules.Drop(tile).Amount != 0) return "瓦砾材质不支持安全清障";
            var sources = new HashSet<CellCoord>(Enumerable.Range(0, config.RockCount).Select(config.Source));
            var placed = new HashSet<CellCoord>();
            for (int index = 0; index < config.RockCount; index++)
            {
                var source = config.Source(index);
                if (!map.Descriptor.Bounds.Contains(source) || !map.Read(source).TryGetCell(out var rock) ||
                    (detached ? !rock.IsEmpty : rock.TileId != tile || rock.Flags != 0))
                    return "松岩或落点已改变";
                if (world.Terrain.Minerals != null && (!world.Terrain.Minerals.Read(source).TryGetCell(out var mineral) || !mineral.IsEmpty))
                    return "源岩覆盖矿格";
            }
            for (int index = 0; index < config.RubbleCount; index++)
            {
                var landing = config.Landing(index);
                if (!map.Descriptor.Bounds.Contains(landing) || !map.Read(landing).TryGetCell(out var empty) || !empty.IsEmpty || !placed.Add(landing))
                    return "松岩或落点已改变";
                var below = new CellCoord(landing.U, landing.V - 1);
                if (!placed.Contains(below) && (!map.Read(below).TryGetCell(out var floor) || floor.IsEmpty ||
                    TerrainShapeGeometry.Decode(floor.Flags) != TerrainCellShape.Full)) return "落点缺少完整支撑";
                if (world.Terrain.Minerals != null && (!world.Terrain.Minerals.Read(landing).TryGetCell(out var ore) || !ore.IsEmpty))
                    return "落点覆盖矿格";
                if (includeActors && world.Index.Actors.Any(actor => Occupies(actor, landing))) return "等待角色离开落点";
                if (includeActors && world.Index.Buildings.Any(building => CoversBuilding(building, landing))) return "落点覆盖建筑";
            }
            if (!PathClear(config, position => sources.Contains(position) || map.Read(position).TryGetCell(out var value) && value.IsEmpty))
                return "落石路径已有结构，事件不穿透岩壁";
            return "";
        }

        internal static bool PathClear(GeologyEventConfig config, Func<CellCoord, bool> passable)
        {
            var collapse = config.Collapse;
            for (int i = 0; i < collapse.Count; i++)
            {
                var piece = collapse.Fragment(i);
                for (int step = 0; step < collapse.Frames; step += 3)
                {
                    double clock = step * RubbleCollapseViewData.FrameSeconds;
                    if (clock < piece.Motion.Delay) continue;
                    collapse.Sample(i, clock, out float x, out float y, out float angle);
                    double radians = angle * Math.PI / 180;
                    float halfX = (float)(Math.Abs(Math.Cos(radians)) * piece.Width + Math.Abs(Math.Sin(radians)) * piece.Height) * .5f - .5f;
                    float halfY = (float)(Math.Abs(Math.Sin(radians)) * piece.Width + Math.Abs(Math.Cos(radians)) * piece.Height) * .5f - .5f;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        int u = (int)Math.Floor((x + (corner % 2 == 0 ? -halfX : halfX)) / PlayableTerrain.CellPixels + .5f);
                        int v = (int)Math.Floor((y - PlayableTerrain.OriginY + (corner < 2 ? -halfY : halfY)) / PlayableTerrain.CellPixels + .5f);
                        var cell = new CellCoord(u, v);
                        if (!passable(cell)) return false;
                    }
                }
            }
            return true;
        }

        internal static void Detach(ObjectSession world, GeologyEventConfig config)
        {
            world.Mutations.RequireWriting();
            for (int index = 0; index < config.RockCount; index++)
                if (!world.Terrain.Map.StageDamage(config.Source(index), int.MaxValue, out bool destroyed) || !destroyed)
                    throw new InvalidOperationException("地质事件源岩未完整脱离。");
        }

        internal static void Stage(ObjectSession world, GeologyEventConfig config)
        {
            world.Mutations.RequireWriting();
            string reason = Check(world, config, true, detached: true);
            if (reason.Length != 0) throw new InvalidOperationException(reason);
            var map = world.Terrain.Map;
            uint tile = map.Tiles.ByKey(config.Material);
            for (int index = 0; index < config.RubbleCount; index++)
            {
                if (!map.StagePlacement(config.Landing(index), tile, config.PileShape(index)))
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
