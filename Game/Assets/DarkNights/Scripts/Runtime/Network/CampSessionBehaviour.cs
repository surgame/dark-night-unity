using System;
using DarkNights.Core.Config;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Behaviours.Interfaces;
using UnityEngine;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 正式会话对象唯一拥有的权威服务生命周期；配置与服务端角色同时就绪后才创建服务。
    /// 通过框架统一更新推进未缩放时钟，停止先撤销外部引用再释放；客户端观察不拥有模拟。
    /// </summary>
    public sealed partial class CampSessionBehaviour : PooledBehaviour, IStartServer, IUpdate
    {
        private GameCatalog catalog;
        private LevelLayout layout;
        private WorldSessionBehaviour projection;
        private string saveDirectory;
        private Action<CampSessionBehaviour> detached;
        private Action<Exception> failed;
        private bool measure, pressure, stopped;
        private SessionWorld simulation;
        public SessionServer Server { get; private set; }
        public bool Configured { get; private set; }
        public bool ServerRoleReady { get; private set; }
        public int StartCount { get; private set; }

        public void Configure(GameCatalog content, LevelLayout level, WorldSessionBehaviour state,
            string saves, Action<CampSessionBehaviour> onDetached, Action<Exception> onFailed,
            bool metrics = false, bool projectionPressure = false, SessionWorld objectSimulation = null)
        {
            if (Configured || stopped) throw new InvalidOperationException("Session instance cannot be configured twice or after stop.");
            catalog = content ?? throw new ArgumentNullException(nameof(content));
            layout = level ?? throw new ArgumentNullException(nameof(level));
            projection = state ?? throw new ArgumentNullException(nameof(state));
            saveDirectory = saves ?? throw new ArgumentNullException(nameof(saves));
            detached = onDetached;
            failed = onFailed;
            measure = metrics;
            pressure = projectionPressure;
            simulation = objectSimulation;
            Configured = true;
            TryStart();
        }

        public void OnStartServer()
        {
            if (stopped) return;
            ServerRoleReady = true;
            TryStart();
        }

        private void TryStart()
        {
            if (!Configured || !ServerRoleReady || stopped || Server != null) return;
            try
            {
                Server = new SessionServer(catalog, layout, projection,
                    new GameSaveStore(saveDirectory, catalog, layout,
                        simulation is ObjectSession objects ? objects.SaveCodec.Serialize : null), measure, pressure, simulation);
                StartCount++;
            }
            catch
            {
                StopServer();
                throw;
            }
        }

        public void Update(float deltaTime)
        {
            if (Server == null || stopped) return;
            try { Server.Advance(Time.unscaledDeltaTime); }
            catch (Exception error)
            {
                var report = failed;
                StopServer();
                report?.Invoke(error);
            }
        }

        public void StopServer()
        {
            if (stopped) return;
            stopped = true;
            ServerRoleReady = false;
            SessionServer previous = Server;
            Server = null;
            var notify = detached;
            detached = null;
            try { notify?.Invoke(this); }
            finally { previous?.Dispose(); }
        }

        protected override void OnSpawn()
        {
            StopServer();
            stopped = Configured = ServerRoleReady = false;
            StartCount = 0;
        }

        public override void OnDespawn()
        {
            StopServer();
            Configured = false;
            catalog = null;
            layout = null;
            projection = null;
            saveDirectory = null;
            failed = null;
            simulation = null;
            base.OnDespawn();
        }
    }
}
