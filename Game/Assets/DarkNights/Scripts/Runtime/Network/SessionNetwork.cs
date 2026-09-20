using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Objects;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using GameCore.NetworkCommands;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using UnityEngine;
using VitalRouter;
using Runtime.Utils;
using YY.Features.Players.View;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 应用启动上下文拥有的正式 LAN 生命周期，复用已有 NetworkManager、定义工厂和 YYGC 命令／状态链。
    /// 网络入口只装配当前会话行为，权威服务由该行为拥有；异步创建检查尝试代次，单人也使用同一 Host 路径。
    /// </summary>
    public sealed class SessionNetwork : MonoBehaviour
    {
        private NetworkManager manager;
        private GameCatalog catalog;
        private LevelLayout layout;
        private Subscription commands, ready, heroInput;
        private DefinitionNetworkAuthenticator authenticator;
        private int attempt;
        private bool connecting, initialized, commandsBound, readyBound, heroInputBound;
        private string lastAddress;
        private ushort lastPort;
        private CampSessionBehaviour activeSession;
        public ObjectSessionResources ObjectResources { get; private set; }
        public IReadOnlyList<ObjectPlacement> ObjectPlacements { get; private set; }
        public ObjectSession ObjectWorld { get; private set; }
        public Terrain.SessionTerrainNetwork Terrain { get; set; }
        public ObjectReplica ReplicaObjects { get; private set; }
        private Transform objectParent;
        private float debugHeroSpeedMultiplier = 1;
        public SessionClient Client { get; private set; }
        public SessionServer Server => activeSession?.Server;
        public CampSessionBehaviour ActiveSession => activeSession;
        internal int ConnectionAttempt => attempt;
        public string Status { get; private set; } = "未连接";
        public bool Hosting => manager != null && manager.IsServerStarted;
        public string SaveDirectory { get; private set; }
        public event Action<Exception> Failed;

        public void Initialize(NetworkManager networkManager, GameCatalog content, LevelLayout level,
            ObjectSessionResources resources, IReadOnlyList<ObjectPlacement> placements, Transform objectParent = null,
            float heroSpeedMultiplier = 1)
        {
            if (initialized) throw new InvalidOperationException("SessionNetwork already initialized.");
            ObjectResources = resources ?? throw new ArgumentNullException(nameof(resources));
            ObjectPlacements = placements ?? throw new ArgumentNullException(nameof(placements));
            manager = networkManager;
            if (manager == null) throw new InvalidOperationException("Existing NetworkManager is required.");
            catalog = content;
            layout = level;
            this.objectParent = objectParent;
            debugHeroSpeedMultiplier = heroSpeedMultiplier;
            manager.ClientManager.SetRemoteServerTimeout(RemoteTimeoutType.Development, 15);
            manager.ServerManager.SetRemoteClientTimeout(RemoteTimeoutType.Development, 15);
            manager.TransportManager.Transport.SetTimeout(15, false);
            manager.TransportManager.Transport.SetTimeout(15, true);
            string[] args = System.Environment.GetCommandLineArgs();
            int saveArgument = Array.IndexOf(args, "--dn-save-dir");
            SaveDirectory = Path.GetFullPath(saveArgument >= 0 && saveArgument + 1 < args.Length
                ? args[saveArgument + 1] : Path.Combine(Application.persistentDataPath, "Saves"));
            SaveDirectory = Path.Combine(SaveDirectory, "v5");
            var fingerprint = new SaveContentFingerprint(catalog, layout);
            authenticator = manager.gameObject.AddComponent<DefinitionNetworkAuthenticator>();
            string identity = new ObjectWorldSaveJson(catalog, layout,
                resources.Definitions.ToDictionary(ObjectSessionResources.Rule, d => d.Guid.ToString()),
                placements.ToDictionary(p => p.PlacementKey, p => ObjectSessionResources.Rule(p.Definition))).IdentitySha256;
            var equipment = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch").SharedConfigs.OfType<HandheldConfig>().Single();
            authenticator.Configure(ObjectDefinitionDatabase.Instance, "dark-nights-session-v" + Session.SessionAuthority.ProtocolVersion +
                ":" + fingerprint.RulesSha256 + ":" + fingerprint.LayoutSha256 + ":" + identity + ":" + equipment.Fingerprint());
            manager.ServerManager.SetAuthenticator(authenticator);
            GenericTypeSerializer<GameCore.Objects.NetworkStates.IStateData>.MaximumPayloadBytes = ProjectionCodec.MaximumBytes + 1024;
            GenericTypeSerializer<INetworkCommand>.MaximumPayloadBytes = 8192;
            NetworkCommandGateway.Instance.RoutingMode = NetworkCommandRoutingMode.ServerAuthoritative;
            NetworkCommandGateway.Instance.Initialize();
            Client = new SessionClient(new ProjectionCodec(catalog, layout));
            ReplicaObjects = new ObjectReplica(ObjectResources, ObjectPlacements, objectParent);
            Client.PrepareProjection = frame =>
            {
                if (!Hosting) ReplicaObjects.Apply(frame.World, frame.Epoch);
            };
            Client.Failed += Fail;
            commands = CommandRouters.LocalInput.SubscribeAwait<SessionCommand>((command, context) => Server?.Handle(command, context) ?? default);
            commandsBound = true;
            heroInput = CommandRouters.LocalInput.SubscribeAwait<HeroInputCommand>((command, context) => Server?.Input(command, context) ?? default);
            heroInputBound = true;
            ready = CommandRouters.LocalInput.SubscribeAwait<SetReadyCommand>((command, context) => Server?.Ready(command, context) ?? default);
            readyBound = true;
            manager.SceneManager.OnClientLoadedStartScenes += Loaded;
            manager.ServerManager.OnRemoteConnectionState += Remote;
            manager.ClientManager.OnClientConnectionState += ClientState;
            manager.ServerManager.OnServerConnectionState += ServerState;
            initialized = true;
        }

        public async UniTask Connect(bool host, string address, ushort port)
        {
            if (!initialized || connecting || manager.IsClientStarted || manager.IsServerStarted) return;
            connecting = true;
            int current = ++attempt;
            ObjectSession preparing = null;
            try
            {
                if (host && Terrain != null) await Terrain.EnsureSelected();
                if (this == null || current != attempt) return;
                if (host || address != lastAddress || port != lastPort) Client.ClearRecovery();
                lastAddress = address;
                lastPort = port;
                Client.Begin();
                manager.TransportManager.Transport.SetPort(port);
                manager.TransportManager.Transport.SetClientAddress(address);
                if (host)
                {
                    Status = "正在创建房间";
                    if (!manager.ServerManager.StartConnection()) throw new InvalidOperationException("无法启动房间。");
                    double deadline = Time.realtimeSinceStartupAsDouble + 10;
                    while (!manager.IsServerStarted && Time.realtimeSinceStartupAsDouble < deadline)
                        await UniTask.Yield();
                    if (this == null || current != attempt) return;
                    if (!manager.IsServerStarted) throw new TimeoutException("房间启动超时。");
                    preparing = new ObjectSession(catalog, layout, ObjectResources,
                        () => this != null && current == attempt && manager.IsServerStarted, objectParent,
                        debugHeroSpeedMultiplier);
                    var view = await CreateCurrent(FormalObjectCatalog.SessionKey, current, preparing.Context);
                    if (this == null || current != attempt) { if (view != null) Destroy(view.gameObject); return; }
                    if (view == null) throw new InvalidOperationException("正式会话对象创建失败。");
                    if (Terrain != null) preparing.Terrain = Terrain.CreateAuthority(preparing.Context);
                    preparing.Prepare(view.Owner, ObjectPlacements);
                    var behaviour = view.Owner.GetAllBehaviors().OfType<WorldSessionBehaviour>().Single();
                    var session = view.Owner.GetAllBehaviors().OfType<CampSessionBehaviour>().Single();
                    if (activeSession != null) throw new InvalidOperationException("A session object is already active.");
                    activeSession = session;
                    ObjectWorld = preparing;
                    session.Configure(catalog, layout, behaviour, SaveDirectory, preparing,
                        source => { if (current == attempt && ReferenceEquals(activeSession, source)) { activeSession = null; ObjectWorld = null; } },
                        error => { if (current == attempt) Fail(error); },
                        Array.IndexOf(System.Environment.GetCommandLineArgs(), "--dn-metrics") >= 0,
                        Array.IndexOf(System.Environment.GetCommandLineArgs(), "--dn-projection-pressure") >= 0);
                    preparing = null;
                }
                Terrain?.BeginConnection();
                Status = "正在连接";
                if (!manager.ClientManager.StartConnection()) throw new InvalidOperationException("无法启动客户端。");
            }
            catch (Exception error) { if (current == attempt) Fail(error); }
            finally { preparing?.Dispose(); if (current == attempt) connecting = false; }
        }

        private async void Loaded(NetworkConnection connection, bool asServer)
        {
            if (!asServer || Server == null) return;
            int current = attempt;
            var owner = activeSession;
            var server = owner.Server;
            try
            {
                if (manager.ServerManager.Clients.Count > 4) { connection.Disconnect(true); return; }
                var view = await CreateCurrent("connection.pinewatch", current);
                if (this == null || current != attempt || !ReferenceEquals(activeSession, owner) ||
                    !ReferenceEquals(owner.Server, server) || !connection.IsActive)
                {
                    if (view != null) Destroy(view.gameObject);
                    return;
                }
                if (view == null) throw new InvalidOperationException("正式命令入口创建失败。");
                var endpoint = view.Get<PlayerEndpoint>("endpoint");
                server.Add(connection, endpoint, connection.ClientId == manager.ClientManager.Connection.ClientId);
                endpoint.NetworkObject.GiveOwnership(connection);
            }
            catch (Exception error) { Debug.LogException(error); connection.Disconnect(true); }
        }

        private void Remote(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;
            Server?.Remove(connection);
            if (Server?.Authority.Closed == true) Disconnect();
        }

        private async UniTask<ObjectView> CreateCurrent(string key, int current, ObjectSessionContext objectSession = null)
        {
            var definition = ObjectDefinitionDatabase.Instance.GetDefinitionByKey(key);
            // 锁定的 YYGC 工厂唯一 await 是该缓存加载。先完成它，再检查代次；命中缓存后的
            // 实例化、定义装配和 Spawn 在同一主线程片段完成，旧尝试不会先广播对象再被销毁。
            var prefab = await FastInstantiator.GetOrLoadComponentAsync<ObjectView>(definition.PrefabRef);
            if (this == null || current != attempt || !manager.IsServerStarted) return null;
            if (prefab == null) throw new InvalidOperationException("正式对象资源加载失败：" + key);
            return await ObjectInstanceFactory.CreateObjectInstanceAsync(definition, Vector3.zero, Quaternion.identity,
                session: objectSession, activate: objectSession == null);
        }

        private void ClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (!Client.HadReady) Client.ClearRecovery();
                Client.Dispose();
                if (!Hosting) ReplicaObjects.Clear();
                Status = !Hosting && !string.IsNullOrEmpty(authenticator.LastFailure)
                    ? authenticator.LastFailure : "连接已结束";
            }
        }

        private void ServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;
            activeSession?.StopServer();
        }

        private async void Update()
        {
            if (!initialized) return;
            int current = attempt;
            long generation = Client.ConnectionGeneration;
            try
            {
                if (manager.IsClientStarted) await Client.Advance(Time.realtimeSinceStartupAsDouble);
            }
            catch (Exception error)
            {
                if (this != null && current == attempt && generation == Client.ConnectionGeneration) Fail(error);
            }
        }

        public void Disconnect()
        {
            attempt++;
            Terrain?.Disconnect();
            connecting = false;
            var previous = activeSession;
            activeSession = null;
            previous?.StopServer();
            ObjectWorld = null;
            ReplicaObjects?.Clear();
            Client?.Dispose();
            if (manager != null)
            {
                manager.ClientManager.StopConnection();
                manager.ServerManager.StopConnection(true);
            }
            Status = "未连接";
        }

        public void Fail(Exception error)
        {
            Disconnect();
            Status = error.Message;
            Debug.LogException(error);
            Failed?.Invoke(error);
        }

        private void OnDestroy()
        {
            Disconnect();
            if (commandsBound) commands.Dispose();
            if (readyBound) ready.Dispose();
            if (heroInputBound) heroInput.Dispose();
            if (manager != null)
            {
                manager.SceneManager.OnClientLoadedStartScenes -= Loaded;
                manager.ServerManager.OnRemoteConnectionState -= Remote;
                manager.ClientManager.OnClientConnectionState -= ClientState;
                manager.ServerManager.OnServerConnectionState -= ServerState;
                if (manager.ServerManager.GetAuthenticator() == authenticator) manager.ServerManager.SetAuthenticator(null);
            }
            if (authenticator != null) Destroy(authenticator);
            if (Client != null) Client.Failed -= Fail;
            ReplicaObjects?.Dispose();
            ObjectResources?.Dispose();
        }
    }
}
