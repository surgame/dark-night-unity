using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>恢复候选的塌方阶段与最终地图一致性检查；不从初始资源回填源岩或瓦砾，任何矛盾在事务提交前拒绝。</summary>
    internal static class GeologyRestoreValidation
    {
        internal static void Validate(ObjectSession world, GeologyEventConfig config, TerrainGeologyProgress progress)
        {
            if (progress.SiteId != config.EventId || progress.Phase == 1 && progress.Clock > config.WarningSeconds + .25 ||
                progress.Phase == 2 && progress.Clock > config.FallSeconds + .25 ||
                progress.Phase == 3 && progress.Clock > config.OccupancyWaitSeconds + .25)
                throw new FormatException("塌方保存身份或阶段时钟不一致。");
            var map = world.Terrain.Map;
            if (progress.Phase == 6) return;
            uint material = map.Tiles.ByKey(config.Material);
            bool sourceEmpty = Enumerable.Range(0, config.RockCount).All(i => map.Read(config.Source(i)).TryGetCell(out var c) && c.IsEmpty);
            bool sourceFull = Enumerable.Range(0, config.RockCount).All(i => map.Read(config.Source(i)).TryGetCell(out var c) && c.TileId == material && c.Flags == 0);
            if (progress.Phase <= 1 && !sourceFull || progress.Phase >= 2 && progress.Phase <= 5 && !sourceEmpty ||
                progress.Phase == 6 && !sourceEmpty && !sourceFull) throw new FormatException("塌方保存源岩与阶段矛盾。");
            for (int i = 0; i < config.RubbleCount; i++)
            {
                if (!map.Read(config.Landing(i)).TryGetCell(out var cell)) throw new FormatException("塌方保存落点越界。");
                if (progress.Phase == 4 ? !cell.IsEmpty && (cell.TileId != material || cell.Flags != 0) : !cell.IsEmpty)
                    throw new FormatException("塌方保存瓦砾与阶段矛盾。");
            }
            if (progress.Phase <= 1)
            {
                string reason = GeologyLanding.Check(world, config, false);
                if (reason.Length != 0) throw new FormatException(reason);
            }
        }
    }
}
