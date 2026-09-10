using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.NetworkStates;

namespace YYGC.IdentityValidation
{
    /// <summary>统计每个网络实例的装配及两个角色启动次数；状态修改只在权威端执行。</summary>
    public sealed partial class IdentityStateBehaviour : StatefulBehaviour<IdentityProbeState>, IStartServer, IStartClient
    {
        public int Initializations { get; private set; }
        public int ServerStarts { get; private set; }
        public int ClientStarts { get; private set; }
        protected override void Initialize() { Initializations++; }
        public void OnStartServer() { ServerStarts++; SetValue(4242); }
        public void OnStartClient() { ClientStarts++; }
        public void SetValue(int value) { using (var change = MutateState()) change.Value.Value = value; }
    }
}
