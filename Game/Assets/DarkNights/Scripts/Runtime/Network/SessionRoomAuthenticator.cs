using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using GameCore.Objects.NetworkStates;
using UnityEngine;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 使用锁定YYGC现有的兼容通过扩展点增加游戏侧房间准入，不修改或绕过定义协议。
    /// 普通旧入口保持现有语义；由菜单创建的房间在服务端验证内存票据和实际连接地址后才完成认证。
    /// </summary>
    public sealed class SessionRoomAuthenticator : DefinitionNetworkAuthenticator
    {
        private SessionRoomService rooms;
        private readonly Dictionary<NetworkConnection, (string nonce, double at)> pending =
            new Dictionary<NetworkConnection, (string, double)>();
        public string RoomFailure { get; private set; } = "";
        internal void Attach(SessionRoomService value) { rooms = value; }

        public override void InitializeOnce(NetworkManager manager)
        {
            if (Initialized) return;
            base.InitializeOnce(manager);
            manager.ServerManager.RegisterBroadcast<RoomAuthMessage>(ReceiveProof, false);
            manager.ClientManager.RegisterBroadcast<RoomAuthMessage>(ReceiveChallenge);
            manager.ServerManager.OnRemoteConnectionState += RemoteState;
            manager.ClientManager.OnClientConnectionState += LocalState;
        }

        protected override void OnCompatibilityAccepted(NetworkConnection connection)
        {
            if (rooms?.RequiresTicket != true) { CompleteAuthentication(connection, true); return; }
            if (pending.Count >= 16) { CompleteAuthentication(connection, false); return; }
            string nonce = RoomProof.Random();
            pending[connection] = (nonce, Time.realtimeSinceStartupAsDouble);
            NetworkManager.ServerManager.Broadcast(connection,
                new RoomAuthMessage { Stage = 0, Nonce = nonce, Peer = "", Proof = "", Error = "" }, false);
        }

        private void ReceiveProof(NetworkConnection connection, RoomAuthMessage message, Channel channel)
        {
            if (message.Stage != 1 || !pending.TryGetValue(connection, out var challenge)) return;
            pending.Remove(connection);
            string address = NetworkManager.TransportManager.Transport.GetConnectionAddress(connection.ClientId);
            byte[] key = rooms?.TicketKey(message.Peer, address, connection);
            bool accepted = message.Nonce == challenge.nonce &&
                RoomProof.Verify(key, "fishnet|" + challenge.nonce + "|" + message.Peer, message.Proof);
            NetworkManager.ServerManager.Broadcast(connection,
                new RoomAuthMessage { Stage = 2, Nonce = challenge.nonce, Peer = "",
                    Proof = "", Error = accepted ? "" : "Admission" }, false);
            if (accepted) rooms.Attached(message.Peer, connection);
            CompleteAuthentication(connection, accepted);
        }

        private void ReceiveChallenge(RoomAuthMessage message, Channel channel)
        {
            if (message.Stage == 2)
            {
                if (!string.IsNullOrEmpty(message.Error)) RoomFailure = "房间准入已失效，请重新加入房间。";
                return;
            }
            if (message.Stage != 0 || message.Nonce == null || message.Nonce.Length > 64) return;
            byte[] key = rooms?.LocalTicket;
            if (key == null) { RoomFailure = "此房间需要先从房间列表加入。"; return; }
            NetworkManager.ClientManager.Broadcast(new RoomAuthMessage { Stage = 1, Nonce = message.Nonce,
                Peer = rooms.LocalPeer, Proof = RoomProof.Sign(key, "fishnet|" + message.Nonce + "|" + rooms.LocalPeer),
                Error = "" });
        }

        internal void Poll()
        {
            foreach (var connection in pending.Where(pair =>
                !pair.Key.IsActive || Time.realtimeSinceStartupAsDouble - pair.Value.at > 10)
                .Select(pair => pair.Key).ToArray())
            { pending.Remove(connection); CompleteAuthentication(connection, false); }
        }

        private void RemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped) pending.Remove(connection);
        }

        private void LocalState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Starting) RoomFailure = "";
        }

        internal void DisposeGate()
        {
            if (!Initialized || NetworkManager == null) return;
            NetworkManager.ServerManager.UnregisterBroadcast<RoomAuthMessage>(ReceiveProof);
            NetworkManager.ClientManager.UnregisterBroadcast<RoomAuthMessage>(ReceiveChallenge);
            NetworkManager.ServerManager.OnRemoteConnectionState -= RemoteState;
            NetworkManager.ClientManager.OnClientConnectionState -= LocalState;
            pending.Clear(); rooms = null;
        }
    }
}
