using DarkNights.Samples.LanCoop.Core;
using UnityEngine;

namespace DarkNights.Samples.LanCoop.Presentation
{
    /// <summary>仅用于样板调试的 IMGUI 面板；地址、选择与按钮状态属于本客户端，正式 HUD 仍使用 YYGC UGUI。</summary>
    public sealed class SamplePanel : MonoBehaviour
    {
        public ISampleClient Client { private get; set; }
        public Transform WorksiteVisual;
        private string address = "127.0.0.1", port = "17877";
        private bool selected;
        private void OnGUI()
        {
            if (Client == null) return;
            GUILayout.BeginArea(new Rect(20, 20, 440, 560), GUI.skin.box);
            GUILayout.Label("LAN CO-OP / YYGC SAMPLE");
            GUILayout.Label("Shared camp | 2-4 players | Host authority");
            GUILayout.Label("Address (LAN: enter the host's private IP)");
            address = GUILayout.TextField(address);
            port = GUILayout.TextField(port);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Host") && ushort.TryParse(port, out ushort hostPort)) Client.Connect(true, "127.0.0.1", hostPort);
            if (GUILayout.Button("Join") && ushort.TryParse(port, out ushort clientPort)) Client.Connect(false, address, clientPort);
            if (GUILayout.Button("Disconnect")) Client.Disconnect();
            GUILayout.EndHorizontal();
            GUILayout.Label("Status: " + Client.Status);
            var state = Client.Replica;
            if (state != null)
            {
                GUILayout.Label($"Epoch {state.Epoch} / revision {state.Revision}");
                GUILayout.Label($"Coins: {state.Coins} / purchases: {state.Purchases}");
                GUILayout.Label($"Worksite: {(state.Occupant < 0 ? "free" : "player " + state.Occupant)}");
                GUILayout.Label($"Simulation: {(state.Paused ? "paused" : "running")} / tick {state.SimulationTicks}");
            }
            selected = GUILayout.Toggle(selected, "Local selection (not synchronized)");
            GUI.enabled = Client.Ready;
            if (GUILayout.Button("Buy shared token (10 coins)")) Client.Send(SampleOperation.Buy);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Claim worksite")) Client.Send(SampleOperation.Claim);
            if (GUILayout.Button("Release")) Client.Send(SampleOperation.Release);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Pause / resume (Host)")) Client.Send(SampleOperation.Pause);
            if (GUILayout.Button("Reset world / epoch (Host)")) Client.Send(SampleOperation.Reset);
            if (GUILayout.Button("Resend last request")) Client.Send(SampleOperation.Buy, repeat: true);
            if (GUILayout.Button("Test invalid entity")) Client.Send(SampleOperation.Buy, 999);
            GUI.enabled = true;
            GUILayout.Label("Result: " + Client.LastResult);
            GUILayout.Label("Start paused; network and commands remain active.");
            GUILayout.EndArea();
        }
        private void Update()
        {
            if (WorksiteVisual == null) return;
            var state = Client?.Replica;
            WorksiteVisual.localScale = Vector3.one * (selected ? 1.2f : 1f);
            WorksiteVisual.localRotation = Quaternion.Euler(0, state?.SimulationTicks % 360 ?? 0, 0);
        }
    }
}
