using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DarkNights.Core.ViewData;
using FishNet.Connection;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 菜单房间的唯一席位与临时准入票据所有者；不创建或推进游戏世界。
    /// 发现仅公开游戏就绪且有空位的房间，加入仍须通过原定义握手和票据挑战。
    /// </summary>
    internal sealed class RoomHost : IDisposable
    {
        private readonly RoomSocket socket;
        private readonly Dictionary<string, RoomPeer> peers = new Dictionary<string, RoomPeer>();
        private readonly Dictionary<string, (IPEndPoint endpoint, string nonce, double at)> pending =
            new Dictionary<string, (IPEndPoint, string, double)>();
        private readonly byte[] passwordKey;
        private readonly string salt = RoomProof.Random(16), content;
        private double now, broadcastAt, challengeWindow;
        private int challengeCount;
        internal string Id { get; } = Guid.NewGuid().ToString("N");
        internal string Name { get; }
        internal string HostPeer { get; } = Guid.NewGuid().ToString("N");
        internal byte[] HostKey { get; } = Convert.FromBase64String(RoomProof.Random());
        internal bool RequiresPassword { get; }
        internal bool LocalOnly { get; }
        internal bool LaunchRequested { get; private set; }
        internal bool Starting { get; private set; }
        internal int Revision { get; private set; } = 1;
        internal bool CanStart => !LaunchRequested && peers.Values.All(peer => peer.Ready);

        internal RoomHost(string name, string password, bool localOnly, string content)
        {
            Name = RoomProof.Name(name); this.content = content; LocalOnly = localOnly;
            RequiresPassword = !string.IsNullOrEmpty(password);
            passwordKey = RoomProof.Key(password ?? "", salt);
            if (!localOnly) socket = new RoomSocket(SessionRoomService.DiscoveryPort);
        }

        internal void Poll(double time, bool networkReady)
        {
            now = time;
            socket?.Poll(Receive);
            foreach (string id in pending.Where(pair => now - pair.Value.at > 8).Select(pair => pair.Key).ToArray())
                pending.Remove(id);
            foreach (var peer in peers.Values.ToArray())
                if (peer.Connection?.IsActive == true) peer.Seen = now;
                else if (peer.Connection != null || now - peer.Seen > (Starting ? 120 : 15))
                { peers.Remove(peer.Id); Revision++; }
            if (LaunchRequested && networkReady && !Starting) { Starting = true; Revision++; }
            if (now < broadcastAt) return;
            broadcastAt = now + .5;
            foreach (RoomPeer peer in peers.Values) Reply(peer);
        }

        private void Receive(RoomPacket packet, IPEndPoint endpoint)
        {
            if (packet.Type == "discover")
            {
                if (Starting && peers.Count < 3) socket.Send(new RoomPacket
                {
                    Type = "listing", Room = Id, Request = packet.Request, Name = Name, Content = content,
                    Port = 27777, Players = peers.Count + 1, Password = RequiresPassword
                }, endpoint);
                return;
            }
            if (packet.Room.Length != 0 && packet.Room != Id) return;
            if (packet.Type == "join")
            {
                var existing = peers.Values.FirstOrDefault(peer => peer.JoinRequest == packet.Request && peer.Endpoint.Equals(endpoint));
                if (existing != null) { Reply(existing, "accepted", packet.Request); return; }
                if (LaunchRequested && !Starting) { Reject(endpoint, packet.Request, "Preparing"); return; }
                if (peers.Count >= 3) { Reject(endpoint, packet.Request, "Full"); return; }
                if (packet.Content != content) { Reject(endpoint, packet.Request, "Version"); return; }
                if (packet.Request.Length < 16) return;
                if (now - challengeWindow >= 1) { challengeWindow = now; challengeCount = 0; }
                if (++challengeCount > 16 || pending.Count >= 16) return;
                string nonce = pending.TryGetValue(packet.Request, out var previous) && previous.endpoint.Equals(endpoint)
                    ? previous.nonce : RoomProof.Random();
                pending[packet.Request] = (endpoint, nonce, now);
                socket.Send(new RoomPacket { Type = "challenge", Room = Id, Request = packet.Request,
                    Nonce = nonce, Salt = salt, Password = RequiresPassword, Name = Name }, endpoint);
                return;
            }
            if (packet.Type == "prove")
            {
                var existing = peers.Values.FirstOrDefault(peer => peer.JoinRequest == packet.Request && peer.Endpoint.Equals(endpoint));
                if (existing != null) { Reply(existing, "accepted", packet.Request); return; }
                if (!pending.TryGetValue(packet.Request, out var challenge) || !challenge.endpoint.Equals(endpoint)) return;
                pending.Remove(packet.Request);
                if (LaunchRequested && !Starting) { Reject(endpoint, packet.Request, "Preparing"); return; }
                if (peers.Count >= 3) { Reject(endpoint, packet.Request, "Full"); return; }
                if (!RoomProof.Verify(passwordKey, "join|" + Id + "|" + packet.Request + "|" + challenge.nonce, packet.Proof))
                { Reject(endpoint, packet.Request, "Password"); return; }
                var peer = new RoomPeer { Id = Guid.NewGuid().ToString("N"), Name = "队员 " + (peers.Count + 2),
                    Endpoint = endpoint, Seen = now, JoinRequest = packet.Request };
                peer.Key = RoomProof.Ticket(passwordKey, Id, packet.Request, challenge.nonce, peer.Id);
                peers.Add(peer.Id, peer); Revision++;
                Reply(peer, "accepted", packet.Request);
                return;
            }
            if (!peers.TryGetValue(packet.Peer, out var sender) || !sender.Endpoint.Equals(endpoint) ||
                !RoomProof.Verify(sender.Key, packet.SigningText(), packet.Proof) || packet.Sequence < sender.Sequence) return;
            sender.Seen = now;
            if (packet.Sequence > sender.Sequence)
            {
                sender.Sequence = packet.Sequence;
                if (packet.Type == "leave")
                { sender.Connection?.Disconnect(true); peers.Remove(sender.Id); Revision++; return; }
                if ((packet.Type == "ready" || packet.Type == "status") && !LaunchRequested && sender.Ready != packet.Ready)
                { sender.Ready = packet.Ready; Revision++; }
            }
            Reply(sender);
        }

        private void Reply(RoomPeer recipient, string type = "status", string request = "")
        {
            RoomPeer[] members = peers.Values.ToArray();
            var packet = new RoomPacket { Type = type, Room = Id, Request = request, Peer = recipient.Id,
                Name = Name, Port = 27777, Players = members.Length + 1, State = Starting ? 1 : 0,
                Revision = Revision, Names = new[] { "房主" }.Concat(members.Select(peer => peer.Name)).ToArray(),
                Readiness = new[] { true }.Concat(members.Select(peer => peer.Ready)).ToArray() };
            packet.Proof = RoomProof.Sign(recipient.Key, packet.SigningText());
            socket?.Send(packet, recipient.Endpoint);
        }

        private void Reject(IPEndPoint endpoint, string request, string reason) =>
            socket.Send(new RoomPacket { Type = "rejected", Room = Id, Request = request, Error = reason }, endpoint);

        internal void Launch()
        {
            if (!CanStart) throw new InvalidOperationException("请等待队员完成准备。");
            LaunchRequested = true; Revision++;
        }

        internal RoomLobbyData Snapshot() => new RoomLobbyData(Name, true, CanStart,
            new[] { "你（房主）" }.Concat(peers.Values.Select(peer => peer.Name)).ToArray(),
            new[] { true }.Concat(peers.Values.Select(peer => peer.Ready)).ToArray(), true);

        internal byte[] TicketKey(string id, string address, NetworkConnection connection)
        {
            if (id == HostPeer && IPAddress.TryParse(address, out var ip) && IPAddress.IsLoopback(ip.MapToIPv4())) return HostKey;
            if (!Starting || !peers.TryGetValue(id, out var peer) ||
                !IPAddress.TryParse(address, out var remote) || !peer.Endpoint.Address.Equals(remote.MapToIPv4()) ||
                peer.Connection?.IsActive == true && peer.Connection != connection) return null;
            return peer.Key;
        }

        internal void Attached(string id, NetworkConnection connection)
        {
            if (peers.TryGetValue(id, out var peer)) { peer.Connection = connection; peer.Seen = now; }
        }

        public void Dispose()
        {
            foreach (RoomPeer peer in peers.Values) { ReplyClosed(peer); Array.Clear(peer.Key, 0, peer.Key.Length); }
            socket?.Dispose(); peers.Clear(); pending.Clear();
            Array.Clear(passwordKey, 0, passwordKey.Length); Array.Clear(HostKey, 0, HostKey.Length);
        }

        private void ReplyClosed(RoomPeer peer)
        {
            var packet = new RoomPacket { Type = "closed", Room = Id, Peer = peer.Id, Revision = Revision + 1 };
            packet.Proof = RoomProof.Sign(peer.Key, packet.SigningText());
            try { socket?.Send(packet, peer.Endpoint); } catch (System.Net.Sockets.SocketException) { }
        }
    }
}
