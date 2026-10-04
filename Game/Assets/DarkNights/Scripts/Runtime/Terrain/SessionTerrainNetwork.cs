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
using DarkNights.Runtime.Objects;
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
        private readonly FrozenTerrainRules rules;
        private readonly ARDMapDefinition definition;
        public FrozenTerrainRules Rules => rules;
        public SessionMineralNetwork Minerals { get; private set; }
        private readonly string visual;
        private readonly bool expedition;
        private Task<PlayableTerrain> generation;
        private PlayableTerrain selected;
        private string selectedPresetId = "";
        private int selectionVersion;
        public bool Selecting => generation != null;
        private NativeMapTransport<MapWireMessage> transport;
        private TerrainMapAuthority streaming;
        private bool disposed, initialReady;
        private readonly TerrainBackgroundBaseline background = new TerrainBackgroundBaseline();
        public BackgroundBakeDescriptor Background => background.Reference;
        public ChunkReplicaStateMachine Replica { get; private set; }
        public TileCatalog Tiles => gameplay.Tiles;
        public int Epoch { get; private set; }
        public string Seed { get; private set; } = "";
        public bool PresentationReady { get; set; }
        public GridBounds Region => transport?.Requested ?? SessionMapRegion.Around(320, 0);
        public bool LocalDataReady => transport?.DataReady == true;
        public bool DataReady => initialReady && background.Ready && Replica?.Descriptor != null && !Replica.Closed &&
            Replica.World.WorldId.ToString().Replace("-", "") == background.WorldId && Replica.World.Epoch == background.MapEpoch && Minerals?.InitialReady == true && Minerals.Replica.World.Equals(Replica.World);
        public string SelectionStatus { get; private set; } = "选择地图：灰松谷 · 点击地图按钮生成新地图";
        public long GenerationMilliseconds { get; private set; }
        private readonly Dictionary<ChunkCoord, byte[]> chunkDigests = new Dictionary<ChunkCoord, byte[]>();
        private string contentSha256 = "";
        private bool digestDirty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>旧诊断字段现在返回局部订阅摘要；不扫描 Unknown 区域，也不能与旧全图摘要比较。</summary>
        public string CompatibilitySha256 => ContentSha256;
#endif
        public string ContentSha256
        {
            get
            {
                if (!LocalDataReady) return "";
                if (digestDirty) RebuildDigest();
                return contentSha256;
            }
        }
        public long SentBytes => transport?.SentBytes ?? 0;
        public SessionTerrainNetwork(NetworkManager manager, SessionNetwork network, ARDMapDefinition definition, bool expedition = false, string styleIdentity = "")
        {
            this.expedition = expedition;
            if (expedition) SelectionStatus = "太空远征 · 进入船舱后在驾驶台选择星球";
            this.manager = manager; this.network = network;
            this.definition = definition; rules = TerrainProfileConfig.Resolve().Freeze(definition);
            gameplay = rules.Business.Gameplay; using (var hash = System.Security.Cryptography.SHA256.Create())
                visual = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                    Convert.ToBase64String(definition.VisualCatalog.bytes) + BackgroundBakeDescriptor.StyleContentHash + "|" + styleIdentity))).Replace("-", "").ToLowerInvariant();
            manager.ClientManager.RegisterBroadcast<TerrainEpochSignal>(ReceiveEpoch);
            manager.ClientManager.RegisterBroadcast<TerrainBackgroundChunk>(ReceiveBackground);
            network.Client.MapReady = epoch => Epoch == epoch && DataReady && PresentationReady;
            network.Client.MapIdentity = () => Replica == null ? "" : Replica.World.WorldId + ":" + Replica.World.Epoch;
        }
        public async UniTask SelectNew(QuickTestPreset quickTest = null)
        {
            if (disposed || network.Hosting || network.Client.Replica.Current != null || generation != null) return;
            int version = selectionVersion;
            selected = null;
            SelectionStatus = (expedition ? "洞穴远征" : "灰松谷") + " · 正在生成地图…";
            string seed = Guid.NewGuid().ToString("N");
            string[] args = System.Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--dn-map-seed");
            if (index >= 0 && index + 1 < args.Length) seed = args[index + 1];
            if (quickTest != null) seed = quickTest.Seed;
            string id = Guid.NewGuid().ToString("N");
            var watch = Stopwatch.StartNew();
            var flow = GameCore.Objects.Definition.ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch")
                .SharedConfigs.Find(c => c is DarkNights.Runtime.Objects.ExpeditionFlowConfig) as DarkNights.Runtime.Objects.ExpeditionFlowConfig;
            bool orbit = expedition && flow?.Enabled == true && quickTest == null;
            var caveMap = expedition ? flow?.FreezeCaveMap() ?? throw new InvalidOperationException("远征缺少共用洞穴地图配置。") : null;
            var groundPlanet = expedition && !orbit ? quickTest?.Planet ?? flow.PreviewPlanet() : null;
            var terrainModifiers = expedition && !orbit ? flow.FreezeModifiers() : null;
            generation = Task.Run(() => orbit ? PlanetTerrainGenerator.Space(id) :
                expedition ? PlanetTerrainGenerator.GenerateCandidate(groundPlanet, seed, id, caveMap,
                    pipeline: terrainModifiers) : PlayableTerrainGenerator.Generate(seed, id));
            try
            {
                var result = await generation;
                if (disposed || version != selectionVersion) throw new OperationCanceledException("地图生成已取消。");
                selected = result; selectedPresetId = quickTest?.Id ?? ""; GenerationMilliseconds = watch.ElapsedMilliseconds;
                SelectionStatus = orbit ? "太空船舱已准备 · 开房后靠近驾驶台选择星球" :
                    "已选择" + (expedition ? "洞穴远征" : "灰松谷") + " · 种子 " + seed.Substring(0, Math.Min(12, seed.Length)) + " · 点击地图可重新生成";
                UnityEngine.Debug.Log("DARK_NIGHTS_MAP_GENERATED seed=" + seed + " milliseconds=" + GenerationMilliseconds);
            }
            finally { generation = null; }
        }
        public async UniTask EnsureSelected(QuickTestPreset quickTest = null)
        {
            int version = selectionVersion;
            if (generation != null) await generation;
            if (disposed || version != selectionVersion) throw new OperationCanceledException("地图选择已取消。");
            if (quickTest != null || selected == null || selectedPresetId != "") await SelectNew(quickTest);
            if (selected == null) throw new InvalidOperationException("地图尚未生成。");
        }
        public SessionTerrain CreateAuthority(ObjectSessionContext context)
        {
            if (selected == null) throw new InvalidOperationException("请先选择地图。");
            var result = new SessionTerrain(context, gameplay, selected, rules, definition); selected = null; return result;
        }
        public void BeginConnection()
        {
            Disconnect();
            Replica = TerrainMapNetworking.CreateReplica(gameplay, visual, rules.Business);
            Replica.Applied += OnReplicaApplied;
            Minerals = new SessionMineralNetwork(manager, network); Minerals.BeginConnection();
            CreateTransport();
        }
        private void CreateTransport()
        {
            transport = new NativeMapTransport<MapWireMessage>(manager, Replica, (connection, token) =>
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
            }, SessionMapRegion.Client(network), (connection, region) => SessionMapRegion.Authorize(network, connection, region),
                message => message.Bytes, bytes => new MapWireMessage { Bytes = bytes }, diagnostics: true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (transport.Diagnostics != null)
            {
                transport.Diagnostics.Contributor = this;
                transport.Diagnostics.Authority = () => network.ObjectWorld?.Terrain?.Map;
            }
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public IReadOnlyDictionary<string, string> Capture() => TerrainNetworkDiagnostics.Capture(this);
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
            var world = new WorldIdentity(StableGuid.Parse(signal.WorldId), signal.MapEpoch);
            transport.BeginWorld(world); Epoch = signal.Epoch; Seed = signal.Seed; PresentationReady = initialReady = false;
            Minerals?.BeginWorld(world);
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
            transport.RequestRegion(SessionMapRegion.Client(network));
            transport.Pump();
            initialReady |= transport.DataReady;
            Minerals?.Pump();
        }
        private void OnReplicaApplied(MapReplicaChange change)
        {
            if (change.Kind == MapReplicaChangeKind.WorldReset || change.Kind == MapReplicaChangeKind.VisibilityRevoked ||
                change.Kind == MapReplicaChangeKind.Disconnected)
            {
                chunkDigests.Clear(); contentSha256 = ""; digestDirty = true;
                return;
            }
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
            selectionVersion++; selected = null; selectedPresetId = "";
            SelectionStatus = expedition ? "太空远征 · 进入船舱后在驾驶台选择星球" : "选择地图：灰松谷 · 点击地图按钮生成新地图";
            if (Replica != null) Replica.Applied -= OnReplicaApplied;
            transport?.Dispose(); transport = null; Replica = null; streaming = null;
            Minerals?.Dispose(); Minerals = null;
            Epoch = 0; contentSha256 = ""; chunkDigests.Clear(); digestDirty = false; PresentationReady = initialReady = false;
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
