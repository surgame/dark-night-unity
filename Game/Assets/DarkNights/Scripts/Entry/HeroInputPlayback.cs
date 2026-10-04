using System;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DarkNights.Core.ViewData;
using AnyRules.Next;

namespace DarkNights.Entry
{
    /// <summary>
    /// 仅供显式 Player 验收驱动使用的输入回放，走正式客户端 Gateway，不直接修改角色。
    /// 连续输入每 100 ms 发送，冻结租约与 epoch；停止回放故意不补归零包，以验证服务端超时。
    /// </summary>
    public sealed class HeroInputPlayback : MonoBehaviour
    {
        private SessionClient client;
        private SessionNetwork network;
        private JObject held;
        private double nextSend;
        private bool sending;
        private int epoch;
        public int PacketsSent { get; private set; }
        public string Error { get; private set; }

        public void Initialize(SessionNetwork value) { network = value; client = value.Client; }

        public async ValueTask Execute(JObject command)
        {
            string operation = (string)command["operation"];
            if (operation == "input-stop") { held = null; return; }
            if (operation == "input-hold")
            {
                var frame = client.Replica.Current;
                if (!client.Ready || frame == null) throw new InvalidOperationException("Input playback requires Ready.");
                held = (JObject)command.DeepClone();
                int actor = (int)held["actor"];
                held["lease"] = (int?)held["lease"] ?? frame.World.Actors.Single(a => a.Id == actor).ControlLease;
                epoch = frame.Epoch; nextSend = 0;
                return;
            }
            await Send(command, operation == "input-raw");
        }

        private async void Update()
        {
            if (held == null || sending || Time.unscaledTimeAsDouble < nextSend) return;
            if (!client.Ready || client.Replica.Current?.Epoch != epoch) { held = null; return; }
            sending = true; nextSend = Time.unscaledTimeAsDouble + 0.1;
            try { await Send(held, false); }
            catch (Exception error) { Error = error.ToString(); held = null; }
            finally { sending = false; }
        }

        private async ValueTask Send(JObject command, bool raw)
        {
            var frame = client.Replica.Current;
            int actor = (int)command["actor"];
            int lease = (int?)command["lease"] ?? frame.World.Actors.Single(a => a.Id == actor).ControlLease;
            int horizontal = (int?)command["horizontal"] ?? 0;
            bool jump = (bool?)command["jumpHeld"] ?? false, use = (bool?)command["useHeld"] ?? false;
            bool pressed = (bool?)command["jumpPressed"] ?? false, drop = ((bool?)command["dropHeld"] ?? false) || ((bool?)command["dropPressed"] ?? false);
            float aim = (float?)command["aimAngle"] ?? 0;
            int selection = (int?)command["selectionRevision"] ?? frame.World.Actors.Single(a => a.Id == actor).SelectionRevision;
            bool usePressed = (bool?)command["usePressed"] ?? false, useReleased = (bool?)command["useReleased"] ?? false;
            bool cancel = (bool?)command["cancelUse"] ?? false;
            var mining = Mining(command);
            if (raw)
                await client.SendInputFrozen(new HeroInputRequest(
                    (int?)command["protocol"] ?? SessionAuthority.ProtocolVersion, (int?)command["epoch"] ?? frame.Epoch,
                    (int?)command["policy"] ?? frame.PolicyRevision, actor, lease, (long)command["sequence"],
                    (long?)command["observedTick"] ?? frame.ServerTick, horizontal, jump, use, pressed, drop, aim, selection, usePressed, useReleased, cancel, mining: mining));
            else await client.SendInput(actor, lease, horizontal, jump, use, pressed, drop, aim, selection, usePressed, useReleased, cancel, mining: mining);
            PacketsSent++;
            if (ReferenceEquals(command, held)) { command["jumpPressed"] = false; command["dropPressed"] = false; command["usePressed"] = false; command["useReleased"] = false; command["cancelUse"] = false; }
        }

        private void OnDisable() { held = null; }

        private HeroMiningTarget Mining(JObject command)
        {
            if (!(command["mining"] is JObject target)) return default;
            var map = network.Terrain?.Replica;
            if (map?.Descriptor == null) return default;
            var position = new CellCoord((int)target["u"], (int)target["v"]);
            if (!map.Read(position).TryGetCell(out var foreground)) return default;
            int id = (int?)target["entity"] ?? 0;
            var kind = id == 0 ? HeroMiningTargetKind.Foreground : HeroMiningTargetKind.MineralDeposit;
            ulong version = 0;
            if (id != 0)
            {
                var deposit = client.Replica.Current.World.MineralDeposits.SingleOrDefault(value => value.Id == id);
                version = deposit?.Cells.FirstOrDefault(value => value.U == position.U && value.V == position.V).ContentVersion ?? 0;
            }
            return new HeroMiningTarget((string)target["world"] ?? map.World.WorldId.ToString().Replace("-", ""),
                (ulong?)target["mapEpoch"] ?? map.World.Epoch, position.U, position.V, foreground.TileId, foreground.Flags,
                kind, id, (ulong?)target["foregroundVersion"] ?? map.ContentVersion(position), (ulong?)target["version"] ?? version);
        }
    }
}
