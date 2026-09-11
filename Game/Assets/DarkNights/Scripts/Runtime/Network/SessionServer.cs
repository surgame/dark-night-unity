using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Session;
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
        private double uptime, budgetAt;
        private long lastPublishTick;
        public SessionAuthority Authority { get; }
        public int LastPayloadBytes { get; private set; }

        public SessionServer(GameCatalog catalog, LevelLayout layout, WorldSessionBehaviour behaviour)
        {
            this.behaviour = behaviour;
            codec = new ProjectionCodec(catalog, layout);
            Authority = new SessionAuthority(catalog, layout);
            clock = new SessionClock(Authority);
            Publish();
        }

        public void Add(NetworkConnection connection, PlayerEndpoint endpoint, bool isLocalHost)
        {
            if (peers.ContainsKey(connection)) throw new InvalidOperationException("Connection already has an endpoint.");
            int slot = isLocalHost ? 0 : Enumerable.Range(1, 3).FirstOrDefault(i => peers.Values.All(p => p.Authority.PlayerSlot != i));
            if ((!isLocalHost && slot == 0) || peers.Values.Any(p => p.Authority.PlayerSlot == slot))
                throw new InvalidOperationException("Room is full.");
            peers.Add(connection, new SessionPeer(connection, Authority.Connect(slot), endpoint, uptime));
            Publish();
        }

        public void Remove(NetworkConnection connection)
        {
            if (!peers.TryGetValue(connection, out var peer)) return;
            Authority.Disconnect(peer.Authority);
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
            if (peer == null) return default;
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
                if (!peer.Authority.Ready && uptime - peer.JoinedAt > 30) peer.Network.Disconnect(true);
            var receipts = clock.Advance(seconds);
            foreach (var receipt in receipts)
            {
                var peer = peers.Values.FirstOrDefault(p => p.Authority.PlayerSlot == receipt.PlayerSlot &&
                    p.Authority.Generation == receipt.ConnectionGeneration);
                if (peer != null) Reply(peer, receipt);
            }
            if (receipts.Count != 0 || Authority.ServerTick - lastPublishTick >= 6) Publish();
        }

        private static void Reply(SessionPeer peer, SessionReceipt receipt) => peer.Endpoint.Reply(peer.Network,
            receipt.Sequence, receipt.Epoch, receipt.Revision, receipt.Code.ToString(), receipt.AffectedCount,
            receipt.EntityId, false, receipt.PlayerSlot, receipt.ConnectionGeneration);

        private void Publish()
        {
            var frame = Authority.CaptureProjection();
            byte[] payload = codec.Encode(frame);
            LastPayloadBytes = payload.Length;
            publications.Enqueue((frame.Publication, frame.Epoch, frame.Revision));
            while (publications.Count > 128) publications.Dequeue();
            lastPublishTick = Authority.ServerTick;
            behaviour.Publish(frame, payload);
        }

        public void Dispose()
        {
            Authority.Dispose();
            peers.Clear();
            publications.Clear();
        }
    }
}
