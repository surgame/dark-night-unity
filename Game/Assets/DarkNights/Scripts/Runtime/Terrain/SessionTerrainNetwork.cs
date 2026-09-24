using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using AnyRules.Next.FishNet;
using AnyRules.Next.Networking;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Network;
using FishNet.Managing;
using FishNet.Transporting;
using Channel = FishNet.Transporting.Channel;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>正式会话的地图选择缓存和 AMP1 网络接线；后台只执行纯生成，主线程拥有传输与副本，地图及实体共同就绪才 Ready。</summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class SessionTerrainNetwork : IMapStateDebugContributor, IDisposable
#else
    public sealed class SessionTerrainNetwork : IDisposable
#endif
    {
        private readonly NetworkManager manager;
        private readonly SessionNetwork network;
        private readonly ServerGameplayCatalog gameplay;
        private readonly string visual;
        private readonly bool expedition;
        private Task<PlayableTerrain> generation;
        private PlayableTerrain selected;
        private FishNetMapTransport transport;
        private TerrainMapAuthority streaming;
        private bool disposed;
        private readonly TerrainBackgroundBaseline background = new TerrainBackgroundBaseline();
        public BackgroundBakeDescriptor Background => background.Reference;
        public ChunkReplicaStateMachine Replica { get; private set; }
        public int Epoch { get; private set; }
        public string Seed { get; private set; } = "";
        public bool PresentationReady { get; set; }
        public bool DataReady => background.Ready && Replica?.Descriptor != null && Replica.CommitId > 0 && !Replica.Closed && !Replica.NeedsResync &&
            Replica.World.WorldId.ToString().Replace("-", "") == background.WorldId && Replica.World.Epoch == background.MapEpoch;
        public string SelectionStatus { get; private set; } = "选择地图：灰松谷 · 点击地图按钮生成新地图";
        public long GenerationMilliseconds { get; private set; }
        private readonly Dictionary<ChunkCoord, byte[]> chunkDigests = new Dictionary<ChunkCoord, byte[]>();
        private string contentSha256 = "";
        private bool digestDirty;
        public string ContentSha256
        {
            get
            {
                if (!DataReady) return "";
                if (digestDirty) RebuildDigest();
                return contentSha256;
            }
        }
        public long SentBytes => transport?.SentBytes ?? 0;
        public SessionTerrainNetwork(NetworkManager manager, SessionNetwork network, ARDMapDefinition definition, bool expedition = false, string styleIdentity = "")
        {
            this.expedition = expedition;
            if (expedition) SelectionStatus = "选择地图：洞穴远征 · 点击地图生成新种子";
            this.manager = manager; this.network = network;
            gameplay = definition.LoadGameplayCatalog(); using (var hash = System.Security.Cryptography.SHA256.Create())
                visual = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                    Convert.ToBase64String(definition.VisualCatalog.bytes) + BackgroundBakeDescriptor.StyleContentHash + "|" + styleIdentity))).Replace("-", "").ToLowerInvariant();
            manager.ClientManager.RegisterBroadcast<TerrainEpochSignal>(ReceiveEpoch);
            manager.ClientManager.RegisterBroadcast<TerrainBackgroundChunk>(ReceiveBackground);
            network.Client.MapReady = epoch => Epoch == epoch && DataReady && PresentationReady;
            network.Client.MapIdentity = () => Replica == null ? "" : Replica.World.WorldId + ":" + Replica.World.Epoch;
        }
        public async UniTask SelectNew()
        {
            if (disposed || network.Hosting || network.Client.Replica.Current != null || generation != null) return;
            SelectionStatus = (expedition ? "洞穴远征" : "灰松谷") + " · 正在生成地图…";
            string seed = Guid.NewGuid().ToString("N");
            string[] args = System.Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--dn-map-seed");
            if (index >= 0 && index + 1 < args.Length) seed = args[index + 1];
            string id = Guid.NewGuid().ToString("N");
            var watch = Stopwatch.StartNew();
            generation = Task.Run(() => expedition ? ExpeditionTerrainGenerator.Generate(seed, id) : PlayableTerrainGenerator.Generate(seed, id));
            try
            {
                var result = await generation;
                if (disposed) return;
                selected = result; GenerationMilliseconds = watch.ElapsedMilliseconds;
                SelectionStatus = "已选择" + (expedition ? "洞穴远征" : "灰松谷") + " · 种子 " + seed.Substring(0, Math.Min(12, seed.Length)) + " · 点击地图可重新生成";
                UnityEngine.Debug.Log("DARK_NIGHTS_MAP_GENERATED seed=" + seed + " milliseconds=" + GenerationMilliseconds);
            }
            finally { generation = null; }
        }
        public async UniTask EnsureSelected()
        {
            if (generation != null) await generation;
            if (selected == null) await SelectNew();
            if (selected == null) throw new InvalidOperationException("地图尚未生成。");
        }
        public SessionTerrain CreateAuthority(ObjectSessionContext context)
        {
            if (selected == null) throw new InvalidOperationException("请先选择地图。");
            var result = new SessionTerrain(context, gameplay, selected); selected = null; return result;
        }
        public void BeginConnection()
        {
            Disconnect();
            Replica = TerrainMapNetworking.CreateReplica(gameplay, visual);
            Replica.Applied += OnReplicaApplied;
            CreateTransport();
        }
        private void CreateTransport()
        {
            transport = new FishNetMapTransport(manager, (connection, token) =>
            {
                var map = network.ObjectWorld.Terrain.Map;
                var terrain = network.ObjectWorld.Terrain;
                byte[] bytes = BackgroundReferenceCodec.Encode(terrain.Background);
                string id = map.World.WorldId.ToString().Replace("-", "");
                int epoch = network.Server.Authority.Epoch;
                manager.ServerManager.Broadcast(connection, new TerrainEpochSignal { Epoch = epoch, Seed = terrain.Seed,
                    WorldId = id, MapEpoch = map.World.Epoch, BackgroundBytes = bytes.Length }, true, Channel.Reliable);
                for (int offset = 0, index = 0; offset < bytes.Length; offset += TerrainBackgroundBaseline.ChunkBytes, index++)
                {
                    var part = new byte[Math.Min(TerrainBackgroundBaseline.ChunkBytes, bytes.Length - offset)];
                    Buffer.BlockCopy(bytes, offset, part, 0, part.Length);
                    manager.ServerManager.Broadcast(connection, new TerrainBackgroundChunk { Epoch = epoch, MapEpoch = map.World.Epoch,
                        WorldId = id, Index = index, Bytes = part }, true, Channel.Reliable);
                }
                return TerrainMapNetworking.OpenStream(map, TerrainMapNetworking.Handshake(map, gameplay, visual), token, _ => true, () => 1);
            }, Replica, new GridBounds(0, -TerrainGenerationSettings.Height + 1, TerrainGenerationSettings.Width, TerrainGenerationSettings.Height));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (transport.Diagnostics != null)
            {
                transport.Diagnostics.Contributor = this;
                transport.Diagnostics.Authority = () => network.ObjectWorld?.Terrain?.Map;
            }
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public IReadOnlyDictionary<string, string> Capture()
        {
            return new Dictionary<string, string>
            {
                ["游戏 Epoch"] = Epoch.ToString(),
                ["地图世界"] = Replica?.World.ToString() ?? "未连接",
                ["流 Commit"] = Replica?.CommitId.ToString() ?? "0",
                ["数据就绪"] = DataReady.ToString(),
                ["背景参考"] = Background?.ReferenceHash ?? "未安装"
            };
        }
#endif
        private void ReceiveEpoch(TerrainEpochSignal signal, Channel channel)
        {
            if (channel != Channel.Reliable || signal.Epoch < Epoch) return;
            try
            {
                if (expedition && signal.BackgroundBytes == 0) throw new FormatException("远征基线缺少初始背景参考。");
                background.Begin(signal);
            }
            catch (Exception error) { network.Fail(error); return; }
            Replica.ResetConnection(); Epoch = signal.Epoch; Seed = signal.Seed; PresentationReady = false;
        }
        private void ReceiveBackground(TerrainBackgroundChunk chunk, Channel channel)
        {
            if (channel != Channel.Reliable) return;
            try { background.Accept(chunk); }
            catch (Exception error) { network.Fail(error); }
        }
        public void Pump()
        {
            if (transport == null) return;
            if (network.Hosting)
            {
                var map = network.ObjectWorld?.Terrain?.Map;
                if (network.Server == null || map == null) return;
                if (streaming != map)
                {
                    transport.Dispose(); Replica.ResetConnection(); PresentationReady = false;
                    streaming = map; CreateTransport();
                }
            }
            transport.Pump();
        }
        private void OnReplicaApplied(MapReplicaChange change)
        {
            if (change.Kind == MapReplicaChangeKind.WorldReset || change.Kind == MapReplicaChangeKind.VisibilityRevoked ||
                change.Kind == MapReplicaChangeKind.Disconnected)
            { chunkDigests.Clear(); contentSha256 = ""; digestDirty = true; return; }
            int size = Replica.Descriptor.ChunkSize;
            using var hash = System.Security.Cryptography.SHA256.Create();
            foreach (var chunk in change.Chunks)
            {
                var bytes = new byte[size * size * 9]; int at = 0;
                for (int i = 0; i < size * size; i++)
                {
                    var cell = Replica.Read(new CellCoord(chunk.U * size + i % size, chunk.V * size + i / size));
                    bool known = cell.TryGetCell(out var value);
                    bytes[at++] = known ? (byte)1 : (byte)0;
                    uint tile = known ? value.TileId : 0;
                    for (int shift = 0; shift < 32; shift += 8) bytes[at++] = (byte)(tile >> shift);
                    short height = known ? value.Height : (short)0;
                    bytes[at++] = (byte)height; bytes[at++] = (byte)(height >> 8);
                    ushort flags = known ? value.Flags : (ushort)0;
                    bytes[at++] = (byte)flags; bytes[at++] = (byte)(flags >> 8);
                }
                chunkDigests[chunk] = hash.ComputeHash(bytes);
            }
            digestDirty = true;
        }
        private void RebuildDigest()
        {
            var chunks = new List<ChunkCoord>(chunkDigests.Keys);
            chunks.Sort((a, b) => a.V != b.V ? a.V.CompareTo(b.V) : a.U.CompareTo(b.U));
            var bytes = new byte[chunks.Count * 40]; int at = 0;
            foreach (var chunk in chunks)
            {
                foreach (int coordinate in new[] { chunk.U, chunk.V })
                    for (int shift = 0; shift < 32; shift += 8) bytes[at++] = (byte)(coordinate >> shift);
                Buffer.BlockCopy(chunkDigests[chunk], 0, bytes, at, 32); at += 32;
            }
            using var hash = System.Security.Cryptography.SHA256.Create();
            contentSha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            digestDirty = false;
        }
        public void Disconnect()
        {
            if (Replica != null) Replica.Applied -= OnReplicaApplied;
            transport?.Dispose(); transport = null; Replica = null; streaming = null;
            Epoch = 0; contentSha256 = ""; chunkDigests.Clear(); digestDirty = false; PresentationReady = false;
            background.Reset();
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            Disconnect(); manager.ClientManager.UnregisterBroadcast<TerrainEpochSignal>(ReceiveEpoch);
            manager.ClientManager.UnregisterBroadcast<TerrainBackgroundChunk>(ReceiveBackground);
        }
    }
}
