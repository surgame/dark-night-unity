using System;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Terrain;
using Newtonsoft.Json.Linq;

namespace DarkNights.Entry
{
    /// <summary>显式验收驱动的只读矿格射线报告；只读本客户端冻结帧与地图副本，不查询或修改服务端状态，报告不构成采集授权。</summary>
    internal static class MineralAutomationProbe
    {
        internal static JObject Read(SessionNetwork network)
        {
            var frame = network.Client.Replica.Current; var map = network.Terrain?.Replica;
            if (frame == null || map?.Descriptor == null) return null;
            var actor = frame.World.Actors.FirstOrDefault(value => value.ControllerSlot == network.Client.PlayerSlot);
            if (actor == null) return null;
            string item = actor.SelectedItem switch { 0 => actor.Slot0Definition, 1 => actor.Slot1Definition,
                2 => actor.Slot2Definition, 3 => actor.Slot3Definition, _ => "" };
            var tool = network.ObjectResources.Equipment.Mining(item); if (tool == null) return null;
            float radians = actor.AimAngle * (float)Math.PI / 180;
            float dx = (float)Math.Cos(radians), dh = (float)Math.Sin(radians), nearest = tool.Reach + .001f;
            if (TerrainMiningQuery.FirstSurface(map, actor.X, actor.Height + tool.HandHeight, dx, dh, tool.Reach,
                out _, out float wall)) nearest = wall;
            JObject result = null;
            foreach (var deposit in frame.World.MineralDeposits)
                foreach (var cell in deposit.Cells)
                {
                    var position = new CellCoord(cell.U, cell.V);
                    if (cell.Remaining <= 0 || !map.Read(position).TryGetCell(out var foreground) || !foreground.IsEmpty ||
                        !TerrainMiningGeometry.RayCell(actor.X, actor.Height + tool.HandHeight, dx, dh, tool.Reach,
                            cell.U, cell.V, TerrainCellShape.Full, out float distance) || distance >= nearest) continue;
                    nearest = distance;
                    result = new JObject { ["entity"] = deposit.Id, ["u"] = cell.U, ["v"] = cell.V,
                        ["version"] = cell.ContentVersion, ["foregroundVersion"] = map.ContentVersion(position),
                        ["remaining"] = cell.Remaining, ["capacity"] = cell.Capacity, ["durability"] = cell.Durability,
                        ["aim"] = actor.AimAngle, ["damage"] = tool.Damage, ["seconds"] = tool.Seconds };
                }
            return result;
        }
    }
}
