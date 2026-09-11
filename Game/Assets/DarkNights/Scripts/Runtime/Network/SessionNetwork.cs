using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Save;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using GameCore.NetworkCommands;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using UnityEngine;
using VitalRouter;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 应用启动上下文拥有的正式 LAN 生命周期，复用已有 NetworkManager、定义工厂和 YYGC 命令／状态链。
    /// 每次连接创建独立业务服务，所有异步创建检查尝试代次；退出停止权威、网络及订阅，单人也使用同一 Host 路径。
    /// </summary>
    public sealed class SessionNetwork : MonoBehaviour
    {
        private NetworkManager manager;
        private GameCatalog catalog;
        private LevelLayout layout;
        private Subscription commands, ready;
        private int attempt;
        private bool connecting, initialized;
        private string lastAddress;
        private ushort lastPort;
        public SessionClient Client { get; private set; }
        public SessionServer Server { get; private set; }
        public string Status { get; private set; } = "未连接";
        public bool Hosting => manager != null && manager.IsServerStarted;
        public string SaveDirectory { get; private set; }
        public event Action<Exception> Failed;

        public void Initialize(NetworkManager networkManager, GameCatalog content, LevelLayout level)
        {
            if (initialized) throw new InvalidOperationException("SessionNetwork already initialized.");
            manager = networkManager;
            if (manager == null) throw new InvalidOperationException("Existing NetworkManager is required.");
            catalog = content;
            layout = level;
            manager.ClientManager.SetRemoteServerTimeout(RemoteTimeoutType.Development, 15);
            manager.ServerManager.SetRemoteClientTimeout(RemoteTimeoutType.Development, 15);
            manager.TransportManager.Transport.SetTimeout(15, false);
            manager.TransportManager.Transport.SetTimeout(15, true);
            string[] args = System.Environment.GetCommandLineArgs();
            int saveArgument = Array.IndexOf(args, "--dn-save-dir");
            SaveDirectory = Path.GetFullPath(saveArgument >= 0 && saveArgument + 1 < args.Length
                ? args[saveArgument + 1] : Path.Combine(Application.persistentDataPath, "Saves"));
            var fingerprint = new SaveContentFingerprint(catalog, layout);
            var auth = manager.gameObject.AddComponent<DefinitionNetworkAuthenticator>();
            auth.Configure(ObjectDefinitionDatabase.Instance, "dark-nights-session-v" + Session.SessionAuthority.ProtocolVersion + ":" + fingerprint.RulesSha256 + ":" + fingerprint.LayoutSha256);
            manager.ServerManager.SetAuthenticator(auth);
            GenericTypeSerializer<GameCore.Objects.NetworkStates.IStateData>.MaximumPayloadBytes = ProjectionCodec.MaximumBytes + 1024;
            GenericTypeSerializer<INetworkCommand>.MaximumPayloadBytes = 8192;
            NetworkCommandGateway.Instance.RoutingMode = NetworkCommandRoutingMode.ServerAuthoritative;
            NetworkCommandGateway.Instance.Initialize();
            Client = new SessionClient(new ProjectionCodec(catalog, layout));
            Client.Failed += Fail;
            commands = CommandRouters.LocalInput.SubscribeAwait<SessionCommand>((command, context) => Server?.Handle(command, context) ?? default);
            ready = CommandRouters.LocalInput.SubscribeAwait<SetReadyCommand>((command, context) => Server?.Ready(command, context) ?? default);
            manager.SceneManager.OnClientLoadedStartScenes += Loaded;
            manager.ServerManager.OnRemoteConnectionState += Remote;
            manager.ClientManager.OnClientConnectionState += ClientState;
            initialized = true;
        }

        public async UniTask Connect(bool host, string address, ushort port)
        {
            if (!initialized || connecting || manager.IsClientStarted || manager.IsServerStarted) return;
            connecting = true;
            int current = ++attempt;
            try
            {
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
                    var view = await ObjectInstanceFactory.CreateByKeyAsync(FormalObjectCatalog.SessionKey, Vector3.zero, Quaternion.identity);
                    if (this == null || current != attempt) { if (view != null) Destroy(view.gameObject); return; }
                    if (view == null) throw new InvalidOperationException("正式会话对象创建失败。");
                    var behaviour = view.Owner.GetAllBehaviors().OfType<WorldSessionBehaviour>().Single();
                    Server = new SessionServer(catalog, layout, behaviour, new GameSaveStore(SaveDirectory, catalog, layout),
                        Array.IndexOf(System.Environment.GetCommandLineArgs(), "--dn-metrics") >= 0,
                        Array.IndexOf(System.Environment.GetCommandLineArgs(), "--dn-projection-pressure") >= 0);
                }
                Status = "正在连接";
                if (!manager.ClientManager.StartConnection()) throw new InvalidOperationException("无法启动客户端。");
            }
            catch (Exception error) { Fail(error); }
            finally { if (current == attempt) connecting = false; }
        }

        private async void Loaded(NetworkConnection connection, bool asServer)
        {
            if (!asServer || Server == null) return;
            int current = attempt;
            try
            {
                if (manager.ServerManager.Clients.Count > 4) { connection.Disconnect(true); return; }
                var view = await ObjectInstanceFactory.CreateByKeyAsync("connection.pinewatch", Vector3.zero, Quaternion.identity);
                if (this == null || current != attempt || !connection.IsActive)
                {
                    if (view != null) Destroy(view.gameObject);
                    return;
                }
                if (view == null) throw new InvalidOperationException("正式命令入口创建失败。");
                var endpoint = view.Get<PlayerEndpoint>("endpoint");
                Server.Add(connection, endpoint, connection.ClientId == manager.ClientManager.Connection.ClientId);
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

        private void ClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (!Client.HadReady) Client.ClearRecovery();
                Client.Dispose();
                Status = "连接已结束";
            }
        }

        private async void Update()
        {
            if (!initialized) return;
            try
            {
                Server?.Advance(Time.unscaledDeltaTime);
                if (manager.IsClientStarted) await Client.Advance(Time.realtimeSinceStartupAsDouble);
            }
            catch (Exception error) { Fail(error); }
        }

        public void Disconnect()
        {
            attempt++;
            connecting = false;
            Server?.Dispose();
            Server = null;
            Client?.Dispose();
            if (manager != null)
            {
                manager.ClientManager.StopConnection();
                manager.ServerManager.StopConnection(true);
            }
            Status = "未连接";
        }

        private void Fail(Exception error)
        {
            Disconnect();
            Status = error.Message;
            Debug.LogException(error);
            Failed?.Invoke(error);
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            Disconnect();
            commands.Dispose();
            ready.Dispose();
            manager.SceneManager.OnClientLoadedStartScenes -= Loaded;
            manager.ServerManager.OnRemoteConnectionState -= Remote;
            manager.ClientManager.OnClientConnectionState -= ClientState;
            Client.Failed -= Fail;
        }
    }
}
