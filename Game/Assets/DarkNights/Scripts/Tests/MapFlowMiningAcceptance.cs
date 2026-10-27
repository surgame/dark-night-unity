using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnyRules.Next;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Entry;
using DarkNights.Runtime.Network;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>
    /// 本次矿区回归的一格真实采掘；读取现有只读探针并经正式客户端输入，不传送人物或直接改矿量。
    /// 只用于已Ready的嵌入矿快速局，采出一次即停止，临时停用本地人工输入后在finally恢复。
    /// </summary>
    internal static class MapFlowMiningAcceptance
    {
        internal static async UniTask Check(SessionNetwork network, string output)
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<HeroPlayerController>();
            bool enabled = controller != null && controller.enabled;
            var method = typeof(HeroInputPlayback).Assembly.GetType("DarkNights.Entry.MineralAutomationProbe")
                .GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic);
            var probe = (JObject)method.Invoke(null, new object[] { network });
            double readyDeadline = Time.realtimeSinceStartupAsDouble + 15;
            while (probe == null && Time.realtimeSinceStartupAsDouble < readyDeadline)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                await UniTask.Yield();
                probe = (JObject)method.Invoke(null, new object[] { network });
            }
            if (probe == null)
                File.WriteAllText(Path.Combine(output, "mineral-probe-missing.json"), JObject.FromObject(new
                {
                    network.Client.PlayerSlot, network.Terrain.Minerals.DataReady,
                    actors = network.Client.Replica.Current.World.Actors.Select(a => new
                    { a.Id, a.ControllerSlot, a.SelectedItem, a.Slot0Definition, a.X, a.Height, a.AimAngle })
                }).ToString());
            Assert.That(probe, Is.Not.Null, "快速局出生点必须有真实可瞄准矿格。");
            var cell = new CellCoord((int)probe["u"], (int)probe["v"]);
            int before = (int)probe["remaining"], after = before;
            if (controller != null) controller.enabled = false;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 12;
                bool first = true;
                while (after >= before)
                {
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline), "正式输入未采出目标矿格。");
                    var actor = network.Client.Replica.Current.World.Actors.Single(a => a.ControllerSlot == network.Client.PlayerSlot);
                    var map = network.Terrain.Replica;
                    Assert.That(map.Read(cell).TryGetCell(out var foreground), Is.True);
                    var target = new HeroMiningTarget(map.World.WorldId.ToString().Replace("-", ""), map.World.Epoch,
                        cell.U, cell.V, foreground.TileId, foreground.Flags, HeroMiningTargetKind.MineralDeposit,
                        contentVersion: map.ContentVersion(cell), mineralContentVersion: network.Terrain.Minerals.Replica.ContentVersion(cell));
                    await network.Client.SendInput(actor.Id, actor.ControlLease, 0, false, true, aimAngle: (float)probe["aim"],
                        selectionRevision: actor.SelectionRevision, usePressed: first, mining: target);
                    first = false;
                    await UniTask.Delay(100);
                    after = network.Terrain.Minerals.Replica.Query(cell).State.RemainingReserves;
                }
                var result = new JObject { ["u"] = cell.U, ["v"] = cell.V, ["before"] = before, ["after"] = after,
                    ["clientGateway"] = true, ["directMutation"] = false };
                File.WriteAllText(Path.Combine(output, "mineral-harvest.json"), result.ToString());
            }
            finally
            {
                var actor = network.Client.Replica.Current?.World.Actors.FirstOrDefault(a => a.ControllerSlot == network.Client.PlayerSlot);
                if (actor != null) await network.Client.SendInput(actor.Id, actor.ControlLease, 0, false, false,
                    selectionRevision: actor.SelectionRevision, useReleased: true);
                if (controller != null) controller.enabled = enabled;
            }
            Assert.That(after, Is.LessThan(before));
        }
    }
}
