using System.Linq;
using FishNet.Object;
using GameCore.Objects.Runner;
using Runtime.AppStartup;
using UnityEngine;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 会话 Prefab 的显式 ObjectInstance 绑定，将已装配的状态行为交给应用级网络适配。
    /// 在网络初始化完成后登记，退出时解除当前行为订阅；不扫描场景或创建模拟。
    /// </summary>
    public sealed class SessionObjectLink : NetworkBehaviour
    {
        [SerializeField] private ObjectInstance instance;
        private WorldSessionBehaviour registered;

        private void Update()
        {
            if (!IsClientStarted || registered != null) return;
            var behaviour = instance.GetAllBehaviors().OfType<WorldSessionBehaviour>().SingleOrDefault();
            if (behaviour == null || behaviour.ReactiveState == null) return;
            registered = behaviour;
            AppStartup.Instance.Context.Resolve<SessionNetwork>().Client.Observe(behaviour);
        }

        public override void OnStopClient()
        {
            if (registered != null) AppStartup.Instance?.Context.Resolve<SessionNetwork>()?.Client.Unobserve(registered);
            registered = null;
            base.OnStopClient();
        }
    }
}
