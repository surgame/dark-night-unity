using System;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Networking;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Network;
using FishNet.Connection;
using FishNet.Managing;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>会话独立矿层的局部流生命周期；每个连接订阅玩家附近五个区块宽，包含视口和规则缓冲，初始就绪与移动订阅分开。</summary>
    public sealed class SessionMineralNetwork : IDisposable
    {
        public const int RegionSide = SessionMapRegion.Side;
        private readonly NetworkManager manager;
        private readonly SessionNetwork network;
        private MineralMapAuthority streaming;
        private NativeMapTransport<MineralMapWireMessage> transport;
        private bool disposed;
        private WorldIdentity? expectedWorld;
        public FrozenMineralRules Rules { get; }
        public ChunkReplicaStateMachine Replica => transport?.Replica;
        public GridBounds Region => transport?.Requested ?? default;
        public bool InitialReady { get; private set; }
        public bool DataReady => transport?.DataReady == true;
        public long SentBytes => transport?.SentBytes ?? 0;
        public string Diagnostics => Replica == null ? "未连接" : Replica.World.WorldId + ":" + Replica.World.Epoch +
            " session=" + Replica.Session + " generation=" + Replica.Generation + " commit=" + Replica.CommitId +
            " closed=" + Replica.Closed + " resync=" + Replica.NeedsResync + " rejected=" + transport.RejectedPackets + " " + transport.LastRejection;
        public SessionMineralNetwork(NetworkManager manager, SessionNetwork network)
        { this.manager = manager; this.network = network; Rules = FrozenMineralRules.Resolve(); }
        public void BeginConnection()
        {
            Disconnect();
            var replica = TerrainMapNetworking.CreateReplica(Rules.Business.Gameplay, Rules.Definition.AuthoringSourceDigest, Rules.Business);
            transport = new NativeMapTransport<MineralMapWireMessage>(manager, replica, Open, Around(320, 0),
                (connection, region) => SessionMapRegion.Authorize(network, connection, region),
                message => message.Bytes, bytes => new MineralMapWireMessage { Bytes = bytes });
            if (expectedWorld.HasValue) transport.BeginWorld(expectedWorld.Value);
        }
        public void BeginWorld(WorldIdentity world)
        { expectedWorld = world; InitialReady = false; transport?.BeginWorld(world); }
        private MapInterestService Open(NetworkConnection connection, ulong session)
        {
            var authority = network.ObjectWorld.Terrain.Minerals;
            var handshake = new MapHandshake(authority.Descriptor, Rules.Business.ContentDigest, Rules.Definition.AuthoringSourceDigest,
                Rules.Business.Gameplay.Definitions.Select(value => value.Identity.Guid).ToArray());
            return new MapInterestService(authority, authority.Tiles, handshake, session, _ => true, () => authority.CommitId, () => 1);
        }
        public void Pump()
        {
            if (disposed || transport == null) return;
            if (network.Hosting)
            {
                var current = network.ObjectWorld?.Terrain?.Minerals;
                if (network.Server == null || current == null) return;
                if (streaming != current) { BeginConnection(); streaming = current; }
            }
            transport.RequestRegion(SessionMapRegion.Client(network));
            transport.Pump();
            if (transport.DataReady && network.Terrain?.Replica?.World.Equals(Replica.World) == true) InitialReady = true;
        }
        public static GridBounds Around(float x, float height)
            => SessionMapRegion.Around(x, height);
        public void Disconnect() { transport?.Dispose(); transport = null; streaming = null; InitialReady = false; }
        public void Dispose() { if (disposed) return; disposed = true; Disconnect(); }
    }
}
