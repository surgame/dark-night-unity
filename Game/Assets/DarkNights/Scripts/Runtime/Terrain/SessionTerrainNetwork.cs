using System;
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
    public sealed class SessionTerrainNetwork : IDisposable
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
        public string ContentSha256 { get; private set; } = "";
        private ulong hashedCommit;
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
        }
        private void ReceiveEpoch(TerrainEpochSignal signal, Channel channel)
        {
            if (channel != Channel.Reliable || signal.Epoch < Epoch) return;
            try
            {
                if (expedition && signal.BackgroundBytes == 0) throw new FormatException("远征基线缺少初始背景参考。");
                background.Begin(signal);
            }
            catch (Exception error) { network.Fail(error); return; }
            Replica.ResetConnection(); hashedCommit = 0; ContentSha256 = ""; Epoch = signal.Epoch; Seed = signal.Seed; PresentationReady = false;
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
            if (DataReady && hashedCommit != Replica.CommitId)
            {
                var bytes = new byte[TerrainGenerationSettings.Width * TerrainGenerationSettings.Height * 6];
                int at = 0;
                for (int y = 0; y < 192; y++) for (int x = 0; x < 320; x++)
                {
                    var cell = Replica.Read(new CellCoord(x, -y)).Cell;
                    for (int shift = 0; shift < 32; shift += 8) bytes[at++] = (byte)(cell.TileId >> shift);
                    bytes[at++] = (byte)cell.Flags; bytes[at++] = (byte)(cell.Flags >> 8);
                }
                using var hash = System.Security.Cryptography.SHA256.Create();
                ContentSha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                hashedCommit = Replica.CommitId;
            }
        }
        public void Disconnect()
        {
            transport?.Dispose(); transport = null; Replica = null; streaming = null;
            Epoch = 0; hashedCommit = 0; ContentSha256 = ""; PresentationReady = false;
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
