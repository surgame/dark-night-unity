using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Diagnostics;
using FishNet.Connection;
using GameCore.NetworkCommands;
using VitalRouter;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 可信网络上下文到唯一权威世界的适配，统一 Host/远端业务、速率预算、真实发布版本 Ready 和定向回执。
    /// 同线程拥有整个会话；完整状态以 10 Hz 起点发布，连接或业务变化可立即发布，不跨 await 持有状态池引用。
    /// </summary>
    public sealed class SessionServer : IDisposable
    {
        private readonly Dictionary<NetworkConnection, SessionPeer> peers = new Dictionary<NetworkConnection, SessionPeer>();
        private readonly Queue<(long publication, int epoch, int revision)> publications = new Queue<(long, int, int)>();
        private readonly WorldSessionBehaviour behaviour;
        private readonly ProjectionCodec codec;
        private readonly SessionClock clock;
        private readonly SessionRecoverySlots recovery = new SessionRecoverySlots();
        private readonly bool pressure;
        private double uptime, budgetAt;
        private bool disposed;
        private long lastPublishTick;
        public SessionAuthority Authority { get; }
        public int LastPayloadBytes { get; private set; }
        public SessionStorage Storage { get; }
        public SessionMeasurements Measurements { get; }

        public SessionServer(GameCatalog catalog, LevelLayout layout, WorldSessionBehaviour behaviour, GameSaveStore saves,
            bool measure = false, bool pressure = false)
        {
            this.pressure = pressure;
            this.behaviour = behaviour;
            codec = new ProjectionCodec(catalog, layout);
            Authority = new SessionAuthority(catalog, layout);
            clock = new SessionClock(Authority);
            Storage = new SessionStorage(Authority, saves);
            if (measure) { Measurements = new SessionMeasurements(); clock.MeasureStep = Measurements.Step; }
            try { Publish(); }
            catch { Dispose(); throw; }
        }

        public void Add(NetworkConnection connection, PlayerEndpoint endpoint, bool isLocalHost)
        {
            if (peers.ContainsKey(connection)) throw new InvalidOperationException("Connection already has an endpoint.");
            if (peers.Count >= 4) throw new InvalidOperationException("Room is full.");
            peers.Add(connection, new SessionPeer(connection, isLocalHost, endpoint, uptime));
            Publish();
        }

        public void Remove(NetworkConnection connection)
        {
            if (!peers.TryGetValue(connection, out var peer)) return;
            if (peer.Authority != null)
            {
                recovery.Release(peer.Authority.PlayerSlot, uptime);
                Authority.Disconnect(peer.Authority, closeHostedSession: !peer.IsHost);
            }
            peers.Remove(connection);
            if (!Authority.Closed) Publish();
        }

        private SessionPeer Sender(PublishContext publication)
        {
            if (!NetworkCommandContext.TryGet(publication, out var context) || !context.IsServerExecution ||
                !peers.TryGetValue(context.SenderConnection, out var peer) || !peer.Network.IsActive ||
                peer.Endpoint == null || peer.Endpoint.Owner != peer.Network || peer.Endpoint.Sender.ObjectId != context.SenderObjectId)
                return null;
            if (++peer.Requests > 60) { peer.Network.Disconnect(true); return null; }
            return peer;
        }

        public ValueTask Handle(SessionCommand command, PublishContext publication)
        {
            var peer = Sender(publication);
            if (peer == null || peer.Authority == null) return default;
            try
            {
                SessionReceipt receipt = Authority.Submit(peer.Authority, command.Freeze());
                if (receipt.Code != SessionResultCode.Pending) Reply(peer, receipt);
            }
            catch (ArgumentException)
            {
                peer.Endpoint.Reply(peer.Network, command.RequestSequence, Authority.Epoch, Authority.Revision,
                    "InvalidRequest", 0, 0, false, peer.Authority.PlayerSlot, peer.Authority.Generation);
            }
            return default;
        }

        public ValueTask Ready(SetReadyCommand command, PublishContext publication)
        {
            var peer = Sender(publication);
            if (peer == null) return default;
            bool sent = publications.Any(p => p.publication == command.AppliedPublication && p.epoch == command.Epoch && p.revision == command.AppliedRevision);
            if (peer.Authority == null && command.Protocol == SessionAuthority.ProtocolVersion && command.Ready && sent && !Authority.Loading)
            {
                int slot = recovery.Claim(peer.IsHost, command.RecoveryToken, uptime);
                if (slot < 0) { peer.Network.Disconnect(true); return default; }
                peer.Authority = Authority.Connect(slot);
                peer.Endpoint.GrantRecovery(peer.Network, recovery.Token(slot));
            }
            if (peer.Authority == null) return default;
            bool accepted = command.Protocol == SessionAuthority.ProtocolVersion && command.Ready && sent &&
                Authority.AcknowledgeReady(peer.Authority, command.Epoch, command.AppliedRevision);
            peer.Endpoint.Reply(peer.Network, command.RequestSequence, Authority.Epoch, Authority.Revision,
                accepted ? "Ready" : "NotReady", 0, 0, true, peer.Authority.PlayerSlot, peer.Authority.Generation);
            Publish();
            return default;
        }

        public void Advance(double seconds)
        {
            if (Authority.Closed) return;
            uptime += seconds;
            if (uptime - budgetAt >= 1) { budgetAt = uptime; foreach (var p in peers.Values) p.Requests = 0; }
            foreach (var peer in peers.Values.ToArray())
            {
                if (peer.ReadyEpoch != Authority.Epoch)
                {
                    peer.ReadyEpoch = Authority.Epoch;
                    peer.ReadyDeadline = uptime + 30;
                }
                if (peer.Authority?.Ready != true && uptime > peer.ReadyDeadline) peer.Network.Disconnect(true);
            }
            var receipts = clock.Advance(seconds);
            Measurements?.Backlog(clock.PendingSeconds);
            foreach (var receipt in receipts)
            {
                var peer = peers.Values.FirstOrDefault(p => p.Authority != null && p.Authority.PlayerSlot == receipt.PlayerSlot &&
                    p.Authority.Generation == receipt.ConnectionGeneration);
                if (peer != null) Reply(peer, receipt);
            }
            int previousEpoch = Authority.Epoch;
            Storage.Advance();
            if (receipts.Count != 0 || previousEpoch != Authority.Epoch || Authority.ServerTick - lastPublishTick >= 6) Publish();
        }

        private static void Reply(SessionPeer peer, SessionReceipt receipt) => peer.Endpoint.Reply(peer.Network,
            receipt.Sequence, receipt.Epoch, receipt.Revision, receipt.Code.ToString(), receipt.AffectedCount,
            receipt.EntityId, false, receipt.PlayerSlot, receipt.ConnectionGeneration);

        private void Publish()
        {
            long started = Measurements == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
            long allocated = Measurements == null ? 0 : GC.GetAllocatedBytesForCurrentThread();
            var frame = Authority.CaptureProjection();
            if (pressure) frame = ProjectionPressure.Expand(frame);
            byte[] payload = codec.Encode(frame);
            LastPayloadBytes = payload.Length;
            publications.Enqueue((frame.Publication, frame.Epoch, frame.Revision));
            while (publications.Count > 128) publications.Dequeue();
            lastPublishTick = Authority.ServerTick;
            behaviour.Publish(frame, payload);
            Measurements?.Projection((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 /
                System.Diagnostics.Stopwatch.Frequency, GC.GetAllocatedBytesForCurrentThread() - allocated,
                payload.Length, peers.Values.Count(p => !p.IsHost));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Storage.Dispose();
            Authority.Dispose();
            peers.Clear();
            publications.Clear();
        }
    }
}
