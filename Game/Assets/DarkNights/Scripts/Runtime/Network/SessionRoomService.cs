using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using DarkNights.Core.ViewData;
using FishNet.Connection;
using FishNet.Managing;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using UnityEngine;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// SessionNetwork拥有的开局前房间服务；组合有限发现、房主席位和单个加入尝试，不拥有游戏业务世界。
    /// 所有状态在同一Local主线程推进，退出清理套接字与票据；好友服务尚未接入时不生成假房间。
    /// </summary>
    public sealed class SessionRoomService : MonoBehaviour
    {
        internal const int DiscoveryPort = 27778;
        private readonly Dictionary<string, (RoomListingData data, double seen)> listings =
            new Dictionary<string, (RoomListingData, double)>();
        private readonly Dictionary<string, double> queries = new Dictionary<string, double>();
        private RoomSocket socket;
        private RoomHost host;
        private RoomClient client;
        private SessionNetwork network;
        private SessionRoomAuthenticator authenticator;
        private string content;
        private bool browsing;
        private double nextQuery;
        private int hostRevision = -1;
        public IReadOnlyList<RoomListingData> Listings => listings.Values.Select(value => value.data)
            .OrderBy(value => value.Ping ?? int.MaxValue).ThenBy(value => value.Name).ToArray();
        public bool IsHost => host != null;
        public bool Active => host != null || client != null;
        public bool RequiresTicket => host != null;
        public string Stage => client?.Stage ?? (host == null ? "" : host.LaunchRequested ? "Starting" : "Lobby");
        public int JoiningPhase => client?.ChallengeReceived == true ? 1 : 0;
        public bool CompatibilityAccepted => authenticator != null && authenticator.ClientCompatible;
        public string TargetAddress => client?.Address ?? "127.0.0.1";
        internal string AuthenticationFailure => string.IsNullOrEmpty(authenticator.RoomFailure)
            ? authenticator.LastFailure : authenticator.RoomFailure;
        public RoomLobbyData Lobby => host?.Snapshot() ?? client?.Snapshot();
        internal byte[] LocalTicket => host?.HostKey ?? client?.Key;
        internal string LocalPeer => host?.HostPeer ?? client?.PeerId;
        public event Action Changed, PasswordRequired, StartClientRequested;
        public event Action<string> Rejected;

        internal DefinitionNetworkAuthenticator Install(SessionNetwork value, NetworkManager manager,
            ObjectDefinitionDatabase database, string fingerprint)
        {
            var gate = manager.gameObject.AddComponent<SessionRoomAuthenticator>();
            gate.Configure(database, fingerprint);
            manager.ServerManager.SetAuthenticator(gate);
            network = value; authenticator = gate; content = fingerprint;
            gate.Attach(this);
            return gate;
        }

        public void Browse(bool active)
        {
            browsing = active;
            if (!active) return;
            EnsureSocket(); listings.Clear(); queries.Clear(); nextQuery = 0; Changed?.Invoke();
        }

        public void Host(string name, string password, bool localOnly)
        {
            if (Active || !network.CanStartSession) throw new InvalidOperationException("已有房间或连接正在进行。");
            host = new RoomHost(name, password, localOnly, content);
            browsing = false; hostRevision = host.Revision; Changed?.Invoke();
        }

        public void Join(RoomListingData listing, string password)
        {
            if (Active || !network.CanStartSession) throw new InvalidOperationException("已有房间或连接正在进行。");
            if (!listing.Compatible) throw new InvalidOperationException("房间版本与当前游戏内容不一致。");
            EnsureSocket(); browsing = false;
            client = new RoomClient(socket, listing, content, password, Time.realtimeSinceStartupAsDouble);
            client.Changed += OnChanged; client.PasswordRequired += OnPassword;
            client.StartRequested += OnStarting; client.Failed += OnRejected;
            Changed?.Invoke();
        }

        public void JoinAddress(string address, ushort port)
        {
            if (port != 27777) throw new ArgumentException("当前正式房间端口为27777。");
            Join(new RoomListingData("", "正在连接房间", address, port, 0, null, false, true), "");
        }

        public void SubmitPassword(string value)
        {
            if (client?.Stage != "Password") throw new InvalidOperationException("当前没有等待密码的房间。");
            client.SubmitPassword(value, Time.realtimeSinceStartupAsDouble); Changed?.Invoke();
        }

        public void SetReady(bool value) => client?.SetReady(value);

        public void BeginStart()
        {
            if (host == null) throw new InvalidOperationException("只有房主能开始远征。");
            host.Launch(); Changed?.Invoke();
        }

        private void EnsureSocket() { if (socket == null) socket = new RoomSocket(); }

        private void Update()
        {
            if (network == null) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                authenticator.Poll();
                host?.Poll(now, network.Hosting && network.Server != null);
                if (host != null && hostRevision != host.Revision)
                { hostRevision = host.Revision; Changed?.Invoke(); }
                socket?.Poll((packet, endpoint) => Receive(packet, endpoint, now));
                client?.Poll(now);
                bool changed = false;
                foreach (string key in listings.Where(pair => now - pair.Value.seen > 7).Select(pair => pair.Key).ToArray())
                { listings.Remove(key); changed = true; }
                foreach (string key in queries.Where(pair => now - pair.Value > 3).Select(pair => pair.Key).ToArray()) queries.Remove(key);
                if (changed) Changed?.Invoke();
                if (!browsing || now < nextQuery) return;
                nextQuery = now + 2;
                string request = RoomProof.Random(16); queries[request] = now;
                socket.Send(new RoomPacket { Type = "discover", Request = request },
                    new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            }
            catch (System.Net.Sockets.SocketException)
            { browsing = false; OnRejected("Network"); }
            catch (FormatException) { OnRejected("Admission"); }
        }

        private void Receive(RoomPacket packet, IPEndPoint endpoint, double now)
        {
            if (packet.Type != "listing") { client?.Receive(packet, endpoint, now); return; }
            if (!browsing || packet.Room.Length != 32 || packet.Port != 27777 ||
                packet.Players < 1 || packet.Players >= 4 || !queries.TryGetValue(packet.Request, out double at)) return;
            string key = packet.Room + "|" + endpoint.Address;
            if (!listings.ContainsKey(key) && listings.Count >= 256) return;
            var listing = new RoomListingData(packet.Room, RoomProof.Name(packet.Name), endpoint.Address.ToString(),
                (ushort)packet.Port, packet.Players, Math.Max(0, (int)Math.Round((now - at) * 1000)),
                packet.Password, packet.Content == content);
            listings[key] = (listing, now); Changed?.Invoke();
        }

        internal byte[] TicketKey(string peer, string address, NetworkConnection connection) =>
            peer == null || peer.Length > 64 ? null : host?.TicketKey(peer, address, connection);
        internal void Attached(string peer, NetworkConnection connection) => host?.Attached(peer, connection);
        private void OnChanged() => Changed?.Invoke();
        private void OnPassword() => PasswordRequired?.Invoke();
        private void OnStarting() => StartClientRequested?.Invoke();
        private void OnRejected(string reason) => Rejected?.Invoke(reason);

        public void Stop()
        {
            browsing = false;
            if (client != null)
            {
                client.Changed -= OnChanged; client.PasswordRequired -= OnPassword;
                client.StartRequested -= OnStarting; client.Failed -= OnRejected;
                client.Dispose(); client = null;
            }
            host?.Dispose(); host = null;
            socket?.Dispose(); socket = null; listings.Clear(); queries.Clear();
        }

        internal void DisposeGate() { if (authenticator != null) authenticator.DisposeGate(); }
        private void OnDestroy() { Stop(); DisposeGate(); }
    }
}
