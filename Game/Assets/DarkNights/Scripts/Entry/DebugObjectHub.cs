#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using DarkNights.Runtime.Network;
using GameCore.Debugging;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>游戏侧物体调试页注册宿主；依附正式网络装配，禁用和卸载释放注册，不干预其他开发会话。</summary>
    public sealed class DebugObjectHub : MonoBehaviour
    {
        private SessionNetwork network;
        private IDisposable registration;
        public static void Install(SessionNetwork network)
        {
            if (network.GetComponent<DebugObjectHub>() != null) return;
            var host = network.gameObject.AddComponent<DebugObjectHub>(); host.network = network; host.Register();
        }
        private void Register()
        {
            if (network != null && registration == null) registration = RuntimeDebugHub.RegisterPanel(
                new RuntimeDebugPanelDescriptor("dark_nights.objects", "物体", 0), () => new DebugObjectPanel(network));
        }
        private void OnEnable() => Register();
        private void OnDisable() { registration?.Dispose(); registration = null; }
    }
}
#endif
