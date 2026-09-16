using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.FishNet;
using AnyRules.Next.Networking;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using GameCore.NetworkCommands;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using UnityEngine;
using VitalRouter;

namespace DarkNights.Entry.Terrain
{
    /// <summary>仅用于独立地图测试场景的显式验收入口；实际 FishNet 与 YYGC 命令链验证，绝不挂入正式 Bootstrap。</summary>
    public sealed class TerrainNetworkProbe : MonoBehaviour
    {
        public NetworkManager Manager;
        public NetworkObject SenderPrefab;
        public TerrainMapAsset Map;
        private readonly TerrainProbeReport report = new TerrainProbeReport();
        private readonly List<MapInterestService> streams = new List<MapInterestService>();
        private FishNetMapTransport transport;
        private TerrainMapAuthority authority;
        private ObjectSessionContext session;
        private DIContainer container;
        private ObjectDefinitionDatabase definitions;
        private ChunkReplicaStateMachine replica;
        private Subscription route;
        private string reportPath;
        private CellCoord first, second;
        private double deadline, reconnectAt;
        private double nextReport;
        private bool sentFirst, sentSecond, reconnecting;
        private ulong epoch;
        private ulong initialRevision;
        private static string Arg(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        private async void Start()
        {
            report.Role = Arg("--dn-terrain-role", "");
            if (string.IsNullOrEmpty(report.Role))
            {
                enabled = false;
                UnityEngine.SceneManagement.SceneManager.LoadScene("TerrainTest");
                return;
            }
            reportPath = Arg("--dn-terrain-report", "");
            try
            {
                if (!Path.IsPathRooted(reportPath)) throw new ArgumentException("Explicit report path required.");
                Application.runInBackground = true; Application.targetFrameRate = 60;
                deadline = Time.realtimeSinceStartupAsDouble + 90;
                var initial = Map.ReadBlueprint(); var gameplay = Map.Definition.LoadGameplayCatalog();
                // Fixture knows the initialization load count. Product commands need authoritative revision in their response contract.
                initialRevision = (ulong)(((initial.Width - 1) / 32 + 1) * (1 - GridMath.FloorDiv(1 - initial.Height, 32)));
                var targets = new List<CellCoord>();
                for (int y = 115; y >= 100 && targets.Count < 2; y--) for (int x = 115; x < 140 && targets.Count < 2; x++)
                    if (initial.MaterialAt(x, y) != 0 && !initial.IsProtected(x, y)) targets.Add(new CellCoord(x, -y));
                first = targets[0]; second = targets[1];
                string visual;
                using (var hash = SHA256.Create()) visual = BitConverter.ToString(hash.ComputeHash(Map.Definition.VisualCatalog.bytes)).Replace("-", "").ToLowerInvariant();
                GenericTypeRegistry<INetworkCommand>.Reset();
                GenericTypeRegistry<INetworkCommand>.Register<TerrainEditCommand>(2291);
                GenericTypeRegistry<INetworkCommand>.MarkInitialized();
                GenericTypeRegistry<IStateData>.Reset();
                GenericTypeRegistry<IStateData>.MarkInitialized();
                definitions = ScriptableObject.CreateInstance<ObjectDefinitionDatabase>();
                await definitions.InitializeAsync();
                var authenticator = Manager.gameObject.AddComponent<DefinitionNetworkAuthenticator>();
                authenticator.Configure(definitions, "dark-nights-terrain-probe-v1");
                Manager.ServerManager.SetAuthenticator(authenticator);
                NetworkCommandGateway.Instance.ResetRuntimeState();
                NetworkCommandGateway.Instance.RoutingMode = NetworkCommandRoutingMode.ServerAuthoritative;
                NetworkCommandGateway.Instance.Initialize();
                Manager.TransportManager.Transport.SetPort(ushort.Parse(Arg("--dn-terrain-port", "28740")));
                Manager.TransportManager.Transport.SetClientAddress("127.0.0.1");
                Manager.SceneManager.OnClientLoadedStartScenes += OnLoaded;
                if (report.Role == "host")
                {
                    Manager.ServerManager.StartConnection();
                    while (!Manager.ServerManager.Started) { CheckDeadline(); await Task.Yield(); }
                    container = new DIContainer(); container.Initialize();
                    session = ObjectSessionContext.CreateAuthority(container, () => Manager != null && Manager.ServerManager.Started); session.Activate();
                    epoch = (ulong)DateTime.UtcNow.Ticks;
                    authority = new TerrainMapAuthority(session, initial, gameplay,
                        new WorldIdentity(StableGuid.Parse("bc196c111a164b0987d4223f426beff7"), epoch));
                    route = CommandRouters.LocalInput.SubscribeAwait<TerrainEditCommand>((command, publication) =>
                    {
                        if (!NetworkCommandContext.TryGet(command, out var context) || !context.IsServerExecution ||
                            context.SenderConnection == null || !context.SenderConnection.IsActive) return default;
                        try
                        {
                            if (!ulong.TryParse(command.RequestId, out ulong sequence)) throw new ArgumentException();
                            authority.DestroyTrusted(context.SenderConnection.ClientId, sequence, authority.World,
                                command.ExpectedRevision, new[] { new CellCoord(command.U, command.V) },
                                p => p.Equals(first) || p.Equals(second));
                            report.Accepted++;
                        }
                        catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { report.Rejected++; }
                        Save(); return default;
                    });
                }
                replica = TerrainMapNetworking.CreateReplica(gameplay, visual);
                MapHandshake hello = authority == null ? null : TerrainMapNetworking.Handshake(authority, gameplay, visual);
                transport = new FishNetMapTransport(Manager, authority == null ? null : (connection, token) =>
                {
                    var stream = TerrainMapNetworking.OpenStream(authority, hello, token, _ => true, () => 1);
                    streams.Add(stream); return stream;
                }, replica, new GridBounds(96, -128, 64, 64));
                Manager.ClientManager.StartConnection(); Save();
            }
            catch (Exception e) { Fail(e); }
        }
        private void OnLoaded(NetworkConnection connection, bool asServer)
        {
            if (asServer) Manager.ServerManager.Spawn(Instantiate(SenderPrefab), connection);
        }
        private async void Send(CellCoord p, ulong revision, string sequence)
        {
            try { await NetworkCommandGateway.Instance.ProcessLocalCommandAsync(new TerrainEditCommand { U = p.U, V = p.V, ExpectedRevision = revision, RequestId = sequence }); }
            catch (Exception e) { Fail(e); }
        }
        private void Update()
        {
            if (transport == null || report.Failure != null) return;
            try
            {
                transport.Pump();
                if (Time.realtimeSinceStartupAsDouble >= nextReport) { nextReport = Time.realtimeSinceStartupAsDouble + .5; Save(); }
                if (File.Exists(reportPath + ".stop")) { Application.Quit(); return; }
                if (!report.Success) CheckDeadline();
                if (replica.Descriptor != null && replica.Read(first).TryGetCell(out var a) && replica.Read(second).TryGetCell(out var b))
                {
                    report.FirstEmpty = a.IsEmpty; report.SecondEmpty = b.IsEmpty;
                    if (report.Role == "client" && NetworkCommandGateway.Instance.RegisteredSenderCount > 0)
                    {
                        if (!sentFirst)
                        {
                            sentFirst = true; Send(first, initialRevision, "1"); Send(first, initialRevision, "1"); Send(new CellCoord(0, -100), initialRevision, "3");
                        }
                        else if (a.IsEmpty && !reconnecting && !report.Reconnected)
                        {
                            reconnecting = true; Manager.ClientManager.StopConnection(); reconnectAt = Time.realtimeSinceStartupAsDouble + 1;
                        }
                        else if (report.Reconnected && !sentSecond) { sentSecond = true; Send(second, initialRevision + 1, "2"); }
                    }
                    if (a.IsEmpty && b.IsEmpty && (report.Role != "client" || report.Reconnected))
                    {
                        report.Success = report.Role == "client" || report.Accepted == 2 && report.Rejected >= 2;
                        report.Revision = authority?.CommitId ?? replica.CommitId; Save();
                    }
                }
                if (reconnecting && Time.realtimeSinceStartupAsDouble >= reconnectAt)
                {
                    reconnecting = false; report.Reconnected = true; Manager.ClientManager.StartConnection();
                }
            }
            catch (Exception e) { Fail(e); }
        }
        private void CheckDeadline()
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Terrain acceptance timeout.");
        }
        private void Save()
        {
            if (string.IsNullOrEmpty(reportPath)) return;
            report.Bytes = transport?.SentBytes ?? 0; report.Scans = 0;
            report.RejectedPackets = transport?.RejectedPackets ?? 0;
            report.Pending = replica?.PendingCount ?? 0; report.ReplicaClosed = replica?.Closed ?? false;
            foreach (var stream in streams) report.Scans += stream.PublicationScanCount;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }
        private void Fail(Exception e) { report.Failure = e.ToString(); Debug.LogException(e); Save(); }
        private void OnDestroy()
        {
            if (Manager != null) Manager.SceneManager.OnClientLoadedStartScenes -= OnLoaded;
            transport?.Dispose(); route.Dispose(); authority?.Dispose(); session?.Dispose(); container?.OnReturnToPool();
            if (definitions != null) Destroy(definitions);
        }
    }
}
