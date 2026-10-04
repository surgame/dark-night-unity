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
        private static WorldIdentity? observedWorld;
        private static CellCoord observedCell;
        private static int capacity;
        internal static JObject Read(SessionNetwork network)
        {
            var frame = network.Client.Replica.Current; var map = network.Terrain?.Replica;
            if (frame == null || map?.Descriptor == null || network.Client.PlayerSlot < 0) return null;
            var actor = frame.World.Actors.FirstOrDefault(value => value.ControllerSlot == network.Client.PlayerSlot);
            if (actor == null) return null;
            string item = actor.SelectedItem switch { 0 => actor.Slot0Definition, 1 => actor.Slot1Definition,
                2 => actor.Slot2Definition, 3 => actor.Slot3Definition, _ => "" };
            var tool = network.ObjectResources.Equipment.Mining(item); if (tool == null) return null;
            var minerals = network.Terrain?.Minerals;
            if (minerals?.DataReady != true) return null;
            var identity = minerals.Replica.World;
            if (!observedWorld.HasValue || !observedWorld.Value.Equals(identity))
            {
                float radians = actor.AimAngle * (float)Math.PI / 180;
                if (!MineralMiningQuery.First(minerals.Replica, map, actor.X, actor.Height + tool.HandHeight,
                    (float)Math.Cos(radians), (float)Math.Sin(radians), tool.Reach, out observedCell, out _)) return null;
                observedWorld = identity; capacity = minerals.Replica.Query(observedCell).State.RemainingReserves;
            }
            var position = observedCell;
            var sample = minerals.Replica.Query(position);
            if (sample.Sample.State == GridSampleState.Unknown) return null;
            var state = sample.Sample.State == GridSampleState.Present ? sample.State : default;
            return new JObject { ["entity"] = -1, ["kind"] = "mineral", ["u"] = position.U, ["v"] = position.V,
                ["version"] = minerals.Replica.ContentVersion(position), ["foregroundVersion"] = map.ContentVersion(position),
                ["capacity"] = capacity, ["remaining"] = state.RemainingReserves, ["durability"] = state.Durability,
                ["aim"] = actor.AimAngle, ["damage"] = tool.Damage, ["seconds"] = tool.Seconds };
        }
    }
}
