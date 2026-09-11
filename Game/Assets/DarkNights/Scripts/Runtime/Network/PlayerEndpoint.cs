using DarkNights.Core.ViewData;
using FishNet.Connection;
using FishNet.Object;
using GameCore.NetworkCommands;
using Runtime.AppStartup;
using UnityEngine;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 与 YYGC owned Sender 同定义装配的定向回执入口，显式引用 Sender，不能靠组件查找兜底绑定。
    /// 生命周期向应用上下文中的会话适配登记；TargetRpc 只携带结果，不承载第二条业务执行路径。
    /// </summary>
    public sealed class PlayerEndpoint : NetworkBehaviour
    {
        [SerializeField] private NetworkCommandSender sender;
        public NetworkCommandSender Sender => sender;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner) AppStartup.Instance.Context.Resolve<SessionNetwork>()?.Client.Attach(this);
        }

        public override void OnOwnershipClient(NetworkConnection previousOwner)
        {
            base.OnOwnershipClient(previousOwner);
            if (IsOwner) AppStartup.Instance.Context.Resolve<SessionNetwork>()?.Client.Attach(this);
        }

        public override void OnStopClient()
        {
            AppStartup.Instance?.Context.Resolve<SessionNetwork>()?.Client.Detach(this);
            base.OnStopClient();
        }

        [TargetRpc]
        public void GrantRecovery(NetworkConnection connection, string token)
        {
            AppStartup.Instance?.Context.Resolve<SessionNetwork>()?.Client.GrantRecovery(this, token);
        }

        [TargetRpc]
        public void Reply(NetworkConnection connection, long sequence, int epoch, int revision,
            string code, int affected, int entityId, bool ready, int slot, int generation)
        {
            AppStartup.Instance?.Context.Resolve<SessionNetwork>()?.Client.Receive(this,
                new CommandFeedback(sequence, epoch, revision, code, affected, entityId, ready, slot, generation));
        }
    }
}
