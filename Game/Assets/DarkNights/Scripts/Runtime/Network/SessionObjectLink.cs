using System;
using System.Linq;
using FishNet.Object;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using Runtime.AppStartup;
using UnityEngine;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 会话 Prefab 的显式实例与同步器接线；装配事件和客户端角色同时就绪后登记只读观察。
    /// 服务端停止立即撤销权威，客户端停止只解除观察；旧连接代次的回调不能操作新房间。
    /// </summary>
    public sealed class SessionObjectLink : NetworkBehaviour
    {
        [SerializeField] private ObjectInstance instance;
        [SerializeField] private StateSynchronizer synchronizer;
        private SessionNetwork network;
        private CampSessionBehaviour session;
        private WorldSessionBehaviour projection, registered;
        private int attempt;
        private long connection;
        private bool clientActive;
        public ObjectInstance Instance => instance;
        public StateSynchronizer Synchronizer => synchronizer;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (instance == null || synchronizer == null || instance.gameObject != gameObject || synchronizer.gameObject != gameObject)
                throw new InvalidOperationException("SessionObjectLink requires explicit bindings on its own object.");
            network = AppStartup.Instance.Context.Resolve<SessionNetwork>();
            attempt = network.ConnectionAttempt;
            connection = network.Client.ConnectionGeneration;
            synchronizer.OnInitializeCompleted += Assembled;
            if (synchronizer.IsInitialized) Assembled();
        }

        private void Assembled()
        {
            try
            {
                session = instance.GetAllBehaviors().OfType<CampSessionBehaviour>().Single();
                projection = instance.GetAllBehaviors().OfType<WorldSessionBehaviour>().Single();
                Observe();
            }
            catch (Exception error)
            {
                if (network != null && attempt == network.ConnectionAttempt) network.Fail(error);
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            clientActive = true;
            Observe();
        }

        private void Observe()
        {
            if (!clientActive || projection == null || registered != null || network == null ||
                attempt != network.ConnectionAttempt || connection != network.Client.ConnectionGeneration) return;
            if (network.Hosting && !ReferenceEquals(network.ActiveSession, session)) return;
            registered = projection;
            network.Client.Observe(registered);
        }

        public override void OnStopClient()
        {
            clientActive = false;
            if (registered != null) network?.Client.Unobserve(registered);
            registered = null;
            base.OnStopClient();
        }

        public override void OnStopServer()
        {
            if (session != null && session.Owner == instance) session.StopServer();
            base.OnStopServer();
        }

        public override void OnStopNetwork()
        {
            Cleanup();
            base.OnStopNetwork();
        }

        private void Cleanup()
        {
            if (synchronizer != null) synchronizer.OnInitializeCompleted -= Assembled;
            if (registered != null) network?.Client.Unobserve(registered);
            if (session != null && session.Owner == instance) session.StopServer();
            registered = projection = null;
            session = null;
            network = null;
            clientActive = false;
        }

        private void OnDestroy() => Cleanup();
    }
}
