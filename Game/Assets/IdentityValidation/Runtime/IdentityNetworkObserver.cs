using GameCore.Objects.NetworkStates;
using UnityEngine;

namespace YYGC.IdentityValidation
{
    /// <summary>在网络启动前订阅框架初始化通知；受控夹具只观察，不参与装配和网络协议。</summary>
    public sealed class IdentityNetworkObserver : MonoBehaviour
    {
        public int Completed;
        private void Awake() { GetComponent<StateSynchronizer>().OnInitializeCompleted += () => Completed++; }
    }
}
