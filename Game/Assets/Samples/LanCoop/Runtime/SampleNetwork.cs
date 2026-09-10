using System;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Samples.LanCoop.Core;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using GameCore.NetworkCommands;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using R3;
using UnityEngine;
using VitalRouter;

namespace DarkNights.Samples.LanCoop.Runtime
{
    /// <summary>
    /// 独立场景的网络生命周期：启动、owned 入口、Ready 和只读副本。
    /// 业务通过 YYGC Gateway/Sender/Processor，状态通过 ObjectInstance/StateSynchronizer。
    /// </summary>
    public sealed class SampleNetwork : MonoBehaviour, ISampleClient
    {
        public NetworkManager Manager;
        public NetworkObject SessionPrefab;
        public NetworkObject SenderPrefab;
        public ObjectDefinition SessionDefinition;
        public CampReplica Replica { get; private set; }
        public string Status { get; private set; } = "Offline";
        public string LastResult { get; private set; } = "-";
        public bool Ready { get; private set; }
        public event Action<long, string, int, int> Result;
        public int JoinCount { get; private set; }
        private SampleAuthority authority;
        private CampBehaviour observed;
        private SampleEndpoint endpoint;
        private IDisposable stateSubscription;
        private Subscription commandSubscription;
        private ObjectDefinitionDatabase database;
        private long nextSequence;
        private int readyEpoch;
        private CampCommand lastCommand;
        private bool initialized, connecting;
        private int connectionAttempt;
        private NetworkObject session;

        private async void Start()
        {
            try
            {
                Application.runInBackground = true;
                Application.targetFrameRate = 60;
                SampleRegistry.Register();
                database = ScriptableObject.CreateInstance<ObjectDefinitionDatabase>();
                database.Definitions.Add(SessionDefinition);
                await database.InitializeAsync();
                var authenticator = Manager.gameObject.AddComponent<DefinitionNetworkAuthenticator>();
                authenticator.Configure(database, "lan-coop-sample-v1");
                Manager.ServerManager.SetAuthenticator(authenticator);
                NetworkCommandGateway.Instance.RoutingMode = NetworkCommandRoutingMode.ServerAuthoritative;
                NetworkCommandGateway.Instance.Initialize();
                commandSubscription = CommandRouters.LocalInput.SubscribeAwait<CampCommand>(Handle);
                Manager.SceneManager.OnClientLoadedStartScenes += OnLoaded;
                Manager.ServerManager.OnRemoteConnectionState += OnRemote;
                Manager.ClientManager.OnClientConnectionState += OnClient;
                initialized = true;
            }
            catch (Exception error) { Fail(error); }
        }

        public async void Connect(bool host, string address, ushort port)
        {
            if (connecting || Manager.IsClientStarted || Manager.IsServerStarted) return;
            connecting = true;
            int attempt = ++connectionAttempt;
            try
            {
                float deadline = Time.realtimeSinceStartup + 10;
                while (!initialized && Time.realtimeSinceStartup < deadline) await Task.Delay(20);
                if (this == null || attempt != connectionAttempt) return;
                if (!initialized) throw new TimeoutException("Sample initialization timed out");
                ClearReplica();
                Status = host ? "Hosting" : "Connecting";
                Manager.TransportManager.Transport.SetPort(port);
                Manager.TransportManager.Transport.SetClientAddress(address);
                if (host)
                {
                    if (!Manager.ServerManager.StartConnection()) throw new InvalidOperationException("Cannot start server");
                    while (!Manager.IsServerStarted && Time.realtimeSinceStartup < deadline) await Task.Delay(20);
                    if (this == null || attempt != connectionAttempt) return;
                    if (!Manager.IsServerStarted) throw new TimeoutException("Server start timed out");
                    session = Instantiate(SessionPrefab);
                    session.GetComponent<StateSynchronizer>().InitializeDefinition(SessionDefinition);
                    Manager.ServerManager.Spawn(session);
                    var behaviour = session.GetComponent<ObjectInstance>().GetAllBehaviors().OfType<CampBehaviour>().Single();
                    authority = new SampleAuthority(behaviour);
                }
                Manager.ClientManager.StartConnection();
            }
            catch (Exception error) { Fail(error); Disconnect(); }
            finally { connecting = false; }
        }

        private ValueTask Handle(CampCommand command, PublishContext publication)
            => authority == null ? default : authority.Handle(command, publication);

        private void OnLoaded(NetworkConnection connection, bool asServer)
        {
            if (!asServer || authority == null) return;
            if (Manager.ServerManager.Clients.Count > 4) { connection.Disconnect(true); return; }
            var sender = Instantiate(SenderPrefab);
            authority.Add(connection, sender.GetComponent<SampleEndpoint>());
            Manager.ServerManager.Spawn(sender, connection);
        }
        private void OnRemote(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped) authority?.Remove(connection);
        }
        private void OnClient(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started) { JoinCount++; Status = "Waiting for snapshot"; }
            if (args.ConnectionState == LocalConnectionState.Stopped) { ClearReplica(); Status = "Disconnected"; }
        }

        private void Update()
        {
            authority?.Advance(Time.unscaledDeltaTime);
            if (!Manager.IsClientStarted) return;
            if (endpoint == null)
            {
                endpoint = FindObjectsByType<SampleEndpoint>(FindObjectsSortMode.None).FirstOrDefault(value => value.IsOwner);
                if (endpoint != null) endpoint.ResultReceived += OnResult;
            }
            if (observed == null)
            {
                var objects = FindObjectsByType<ObjectInstance>(FindObjectsSortMode.None);
                observed = objects.SelectMany(value => value.GetAllBehaviors()).OfType<CampBehaviour>().FirstOrDefault();
                if (observed != null)
                    stateSubscription = observed.ReactiveState.Where(value => value != null).Subscribe(Apply);
            }
            if (endpoint != null && Replica != null && readyEpoch != Replica.Epoch)
            {
                readyEpoch = Replica.Epoch;
                Ready = false;
                Send(SampleOperation.Ready);
            }
        }

        private void Apply(CampState state)
        {
            if (Replica != null && (state.Epoch < Replica.Epoch ||
                (state.Epoch == Replica.Epoch && state.Revision <= Replica.Revision))) return;
            Replica = new CampReplica(state.Epoch, state.Revision, state.Coins, state.Purchases,
                state.Occupant, state.Paused, state.SimulationTicks);
        }
        private void OnResult(long sequence, string code, int epoch, int revision)
        {
            LastResult = sequence + ": " + code;
            if (code == "Ready" && Replica?.Epoch == epoch) { Ready = true; Status = "Ready"; }
            Result?.Invoke(sequence, code, epoch, revision);
        }

        public async void Send(SampleOperation operation, int entity = 1, int fault = 0, bool repeat = false)
        {
            if (endpoint == null || Replica == null) return;
            var command = repeat && lastCommand != null ? lastCommand with { } : new CampCommand
            {
                SenderObjectId = endpoint.ObjectId,
                Protocol = fault == 1 ? 99 : 1,
                Epoch = Replica.Epoch - (fault == 2 ? 1 : 0),
                RequestSequence = ++nextSequence,
                Operation = operation,
                EntityId = entity
            };
            lastCommand = command with { };
            try
            {
                if (fault == 3 && !Manager.IsServerStarted)
                {
                    command.SenderObjectId = int.MaxValue;
                    endpoint.GetComponent<NetworkCommandSender>().SendCommandToServer(command, Channel.Reliable);
                }
                else await NetworkCommandGateway.Instance.ProcessLocalCommandAsync(command);
            }
            catch (Exception error) { Fail(error); }
        }

        public void Disconnect()
        {
            // 停止权威发布后再停止网络，避免停机回调继续发送到已销毁对象。
            authority = null;
            connectionAttempt++;
            Manager.ClientManager.StopConnection();
            Manager.ServerManager.StopConnection(true);
            ClearReplica();
            Status = "Offline";
        }
        private void ClearReplica()
        {
            stateSubscription?.Dispose();
            stateSubscription = null;
            observed = null;
            if (endpoint != null) endpoint.ResultReceived -= OnResult;
            endpoint = null;
            Replica = null;
            Ready = false;
            readyEpoch = 0;
            lastCommand = null;
        }
        private void Fail(Exception error) { Status = error.Message; Debug.LogException(error); }
        private void OnDestroy()
        {
            authority = null;
            connectionAttempt++;
            ClearReplica();
            commandSubscription.Dispose();
            if (Manager != null)
            {
                Disconnect();
                Manager.SceneManager.OnClientLoadedStartScenes -= OnLoaded;
                Manager.ServerManager.OnRemoteConnectionState -= OnRemote;
                Manager.ClientManager.OnClientConnectionState -= OnClient;
            }
            NetworkCommandGateway.Instance.ResetRuntimeState();
            if (database != null) Destroy(database);
        }
    }
}
