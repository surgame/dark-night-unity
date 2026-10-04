using System;
using System.Collections.Generic;
using AnyRules.Next;
using AnyRules.Next.Networking;
using AnyRules.Next.FishNet;
using FishNet.Connection;
using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Transporting;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>两张原生地图共用的游戏侧 FishNet 接线；各层通过独立消息类型隔离，复用兴趣服务与副本，区域可更新，所有状态在主线程处理。</summary>
    public sealed class NativeMapTransport<TWire> : IDisposable where TWire : struct, IBroadcast
    {
        /// <summary>单个可信连接的流租约；每次换世界退休，确认与限速只属于这一连接。</summary>
        private sealed class Peer
        {
            internal NetworkConnection Connection;
            internal MapInterestService Stream;
            internal bool Ready;
            internal int Controls;
            internal byte[] Hello;
            internal ulong HelloGeneration;
        }
        private readonly NetworkManager manager;
        private readonly Func<NetworkConnection, ulong, MapInterestService> open;
        private readonly Func<NetworkConnection, GridBounds, GridBounds> regionPolicy;
        private readonly Func<TWire, byte[]> readBytes;
        private readonly Func<byte[], TWire> wrapBytes;
        private readonly Dictionary<int, Peer> peers = new Dictionary<int, Peer>();
        private readonly List<int> removed = new List<int>();
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private GridBounds requested;
        private ulong nextSession;
        private long nextRetry;
        private bool disposed, regionReady;
        private WorldIdentity? expectedWorld;
        public ChunkReplicaStateMachine Replica { get; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public MapStateDiagnostics Diagnostics { get; }
#endif
        public long SentBytes { get; private set; }
        public int RejectedPackets { get; private set; }
        public string LastRejection { get; private set; } = "";
        public GridBounds Requested => requested;
        public void BeginWorld(WorldIdentity world)
        {
            expectedWorld = world; regionReady = false; Replica.ResetConnection();
        }
        public bool DataReady => regionReady && Replica.CommitId > 0 && !Replica.Closed && !Replica.NeedsResync;

        public NativeMapTransport(NetworkManager manager, ChunkReplicaStateMachine replica,
            Func<NetworkConnection, ulong, MapInterestService> open, GridBounds initial,
            Func<NetworkConnection, GridBounds, GridBounds> regionPolicy,
            Func<TWire, byte[]> readBytes, Func<byte[], TWire> wrapBytes, bool diagnostics = false)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            Replica = replica ?? throw new ArgumentNullException(nameof(replica)); this.open = open;
            requested = initial; this.regionPolicy = regionPolicy;
            this.readBytes = readBytes; this.wrapBytes = wrapBytes;
            nextSession = (ulong)DateTime.UtcNow.Ticks;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (diagnostics)
            {
                Diagnostics = new MapStateDiagnostics(Replica, Dispose);
                Diagnostics.PeerProjections = CapturePeerProjections;
                Replica.Applied += OnReplicaApplied;
            }
#endif
            manager.ServerManager.RegisterBroadcast<TWire>(ReceiveServer, true);
            manager.ClientManager.RegisterBroadcast<TWire>(ReceiveClient);
            manager.ClientManager.OnClientConnectionState += ConnectionChanged;
        }

        public void RequestRegion(GridBounds region)
        {
            if (disposed || !region.IsValid || region.Equals(requested)) return;
            requested = region; regionReady = false;
            if (manager.ClientManager.Started && Replica.Descriptor != null) SendControl(MapMessage.Subscribe);
        }

        public void Pump()
        {
            if (disposed) return;
            if (manager.ServerManager.Started && open != null)
            {
                removed.Clear();
                foreach (var pair in peers) if (!pair.Value.Connection.IsActive || !pair.Value.Connection.IsAuthenticated) removed.Add(pair.Key);
                foreach (int id in removed) { peers[id].Stream.Dispose(); peers.Remove(id); }
                foreach (var connection in manager.ServerManager.Clients.Values)
                    if (connection.IsActive && connection.IsAuthenticated && !peers.ContainsKey(connection.ClientId))
                    {
                        if (peers.Count >= 4) break;
                        peers.Add(connection.ClientId, new Peer { Connection = connection, Stream = open(connection, checked(++nextSession)) });
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Diagnostics?.SetPeerCount(peers.Count);
#endif
                foreach (var peer in peers.Values)
                {
                    peer.Controls = 0;
                    if (peer.Ready) peer.Stream.Publish();
                    int budget = 64 * 1024;
                    for (int i = 0; i < 8 && budget > 0; i++)
                    {
                        byte[] bytes = peer.Stream.Dequeue(); if (bytes == null) break;
                        if (peer.HelloGeneration != peer.Stream.Generation && MapProtocol.Decode(bytes).Kind == MapMessage.Hello)
                        { peer.Hello = bytes; peer.HelloGeneration = peer.Stream.Generation; }
                        manager.ServerManager.Broadcast(peer.Connection, wrapBytes(bytes), true, Channel.Reliable);
                        budget -= bytes.Length; SentBytes += bytes.Length;
                    }
                }
            }
            if (manager.ClientManager.Started && expectedWorld.HasValue && clock.ElapsedMilliseconds >= nextRetry)
            {
                nextRetry = clock.ElapsedMilliseconds + (Replica.Descriptor == null ? 500 : 100);
                if (Replica.Descriptor == null)
                    manager.ClientManager.Broadcast(wrapBytes(Array.Empty<byte>()), Channel.Reliable);
                else if (Replica.Tick()) SendControl(MapMessage.Resync);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Diagnostics != null) MapStateDebugBridge.Capture();
#endif
        }

        private void ReceiveServer(NetworkConnection connection, TWire message, Channel channel)
        {
            if (disposed || !peers.TryGetValue(connection.ClientId, out var peer) || !ReferenceEquals(peer.Connection, connection)) return;
            try
            {
                if (++peer.Controls > 16 || channel != Channel.Reliable) throw new FormatException("矿层控制消息限速或通道无效。");
                // 空信封仅请求重发已冻结的 Hello；不创建新流、不改变原生 AMP1 schema 或授予区域。
                byte[] bytes = readBytes(message);
                if (bytes?.Length == 0)
                {
                    if (peer.Hello != null) manager.ServerManager.Broadcast(connection,
                        wrapBytes(peer.Hello), true, Channel.Reliable);
                    return;
                }
                var packet = MapProtocol.Decode(bytes); var stream = peer.Stream;
                if (!packet.World.Equals(stream.World) || packet.Session != stream.Session || packet.Generation != stream.Generation) return;
                if (packet.Kind == MapMessage.Ready) peer.Ready = true;
                else if (peer.Ready && packet.Kind == MapMessage.Subscribe)
                    stream.Subscribe(regionPolicy == null ? packet.Region : regionPolicy(connection, packet.Region));
                else if (peer.Ready && packet.Kind == MapMessage.Ack) stream.Acknowledge(packet.Commit);
                else if (peer.Ready && packet.Kind == MapMessage.Resync && stream.Region.IsValid) stream.Subscribe(stream.Region);
                else throw new FormatException("矿层消息类型无效。");
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is OverflowException)
            { RejectedPackets++; LastRejection = error.ToString(); }
        }

        private void ReceiveClient(TWire message, Channel channel)
        {
            if (disposed) return;
            try
            {
                if (channel != Channel.Reliable) throw new FormatException("矿层必须使用可靠通道。");
                byte[] bytes = readBytes(message);
                if (!expectedWorld.HasValue || !MapProtocol.Decode(bytes).World.Equals(expectedWorld.Value)) return;
                ulong previousSession = Replica.Session;
                bool first = Replica.Descriptor == null;
                var result = Replica.ReceivePacket(bytes);
                if (!result.Applied) return;
                if (result.Kind == MapMessage.Hello || result.Kind == MapMessage.Revoke) regionReady = false;
                else if (!regionReady) regionReady = MineralRegionReadiness.Complete(Replica, Replica.Descriptor, requested);
                if (result.Kind == MapMessage.Hello)
                { SendControl(MapMessage.Ready); if (first || previousSession != Replica.Session) SendControl(MapMessage.Subscribe); }
                else if (result.Kind == MapMessage.Manifest || result.Kind == MapMessage.Delta || result.Kind == MapMessage.Snapshot)
                {
                    SendControl(MapMessage.Ack);
                    // 区域请求可能在服务端换代前发出并被旧 generation 拒绝；完整基线后用当前代次对齐最新需求。
                    if (!regionReady) SendControl(MapMessage.Subscribe);
                }
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is KeyNotFoundException || error is OverflowException)
            { RejectedPackets++; LastRejection = error.ToString(); Replica.Disconnect(); }
        }
        private void SendControl(MapMessage kind)
        {
            var packet = new MapPacket { Kind = kind, World = Replica.World, Session = Replica.Session,
                Generation = Replica.Generation, Commit = Replica.CommitId,
                Region = kind == MapMessage.Subscribe ? MineralRegionReadiness.Subscription(requested, Replica.Descriptor.Bounds) : requested };
            manager.ClientManager.Broadcast(wrapBytes(MapProtocol.Encode(packet)), Channel.Reliable);
        }
        private void ConnectionChanged(ClientConnectionStateArgs state)
        { if (state.ConnectionState == LocalConnectionState.Stopped) Replica.ResetConnection(); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IReadOnlyList<MapPeerProjection> CapturePeerProjections()
        {
            var result = new List<MapPeerProjection>(peers.Count);
            foreach (var pair in peers)
            {
                var peer = pair.Value; var stream = peer.Stream;
                bool stable = peer.Ready && stream.Region.IsValid && stream.ProjectionCurrent && !stream.Backpressured &&
                    stream.QueuedPackets == 0 && stream.RetainedPackets == 0;
                result.Add(new MapPeerProjection(pair.Key, stream.World, stream.Session, stream.Generation, stream.Commit,
                    stable ? "Ready" : "InFlight", stable ? stream.ProjectedCanonicalDigest() : ""));
            }
            return result;
        }
        private void OnReplicaApplied(MapReplicaChange change) => Diagnostics?.Record(change.Kind.ToString(), -1,
            Replica.World, Replica.Session, Replica.Generation, change.Commit, records: change.Cells.Count);
#endif
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            manager.ServerManager.UnregisterBroadcast<TWire>(ReceiveServer);
            manager.ClientManager.UnregisterBroadcast<TWire>(ReceiveClient);
            manager.ClientManager.OnClientConnectionState -= ConnectionChanged;
            foreach (var peer in peers.Values) peer.Stream.Dispose(); peers.Clear(); Replica.Disconnect();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Replica.Applied -= OnReplicaApplied; Diagnostics?.Dispose(); MapStateDebugBridge.StopWhenIdle();
#endif
        }
    }
}
