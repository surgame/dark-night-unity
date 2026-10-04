using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Network;
using FishNet.Connection;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>前景与矿层共用的可信局部区域；范围来自本地冻结角色或服务端真实连接，出生前退回船体，两层保持相同区块合同。</summary>
    public static class SessionMapRegion
    {
        public const int Side = 160;
        public static GridBounds Client(SessionNetwork network)
        {
            var frame = network.Client?.Replica?.Current;
            if (frame == null) return Around(320, 0);
            var actor = network.Client.PlayerSlot >= 0 ? frame.World.Actors.FirstOrDefault(value =>
                value.ControllerSlot == network.Client.PlayerSlot) : null;
            var ship = frame.World.Buildings.FirstOrDefault(value => value.Kind == "ship");
            var device = frame.World.Expedition?.Devices.FirstOrDefault(value => value.Id == ship?.Id);
            return Around(actor?.X ?? ship?.X ?? 320, actor?.Height ?? device?.Height ?? 0);
        }
        public static GridBounds Authorize(SessionNetwork network, NetworkConnection connection, GridBounds requested)
        {
            int slot = network.Server?.TerrainPlayerSlot(connection) ?? -1;
            var world = network.ObjectWorld;
            var actor = world.Index.Actors.FirstOrDefault(value => slot >= 0 && value.Read().ControllerSlot == slot);
            var ship = world.Index.Buildings.FirstOrDefault(value => value.RuleKey == "ship");
            var allowed = Around(actor?.X ?? ship?.X ?? 320, actor?.Read().Height ?? ship?.Read().Height ?? 0);
            var bounds = world.Terrain.Map.Descriptor.Bounds;
            if (!requested.IsValid || requested.Width > Side || requested.Height > Side ||
                Math.Abs(requested.MinU - allowed.MinU) > 32 || Math.Abs(requested.MinV - allowed.MinV) > 32 ||
                !bounds.Contains(new CellCoord(requested.MinU, requested.MinV)) ||
                !bounds.Contains(new CellCoord((int)requested.MaxUExclusive - 1, (int)requested.MaxVExclusive - 1)))
                throw new ArgumentException("地图订阅必须位于可信角色或出生船附近且有界。");
            return requested;
        }
        public static GridBounds Around(float x, float height)
        {
            int u = GridMath.FloorDiv(TerrainMiningGeometry.CellU(x), 32) * 32 - 64;
            int v = GridMath.FloorDiv(TerrainMiningGeometry.CellV(height), 32) * 32 - 64;
            u = Math.Max(0, Math.Min(TerrainGenerationSettings.Width - Side, u));
            v = Math.Max(-TerrainGenerationSettings.Height, Math.Min(-128, v));
            int bottom = Math.Max(-TerrainGenerationSettings.Height + 1, v), top = Math.Min(1, v + Side);
            return new GridBounds(u, bottom, Side, top - bottom);
        }
    }
}
