using System;
using System.Linq;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using DarkNights.View;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>将原生远征面板接到现有可信命令链；房主与来宾共用 Send，客户端不预扣任何资源。</summary>
    public sealed class ExpeditionHud : MonoBehaviour
    {
        private SessionNetwork network;
        private ExpeditionPanel panel;
        private float emergencyConfirmation = -1;
        public void Initialize(SessionNetwork network, ExpeditionPanel panel)
        { this.network = network; this.panel = panel; panel.Bind(Submit); }
        private void Update()
        {
            panel?.Present(network?.Client.Replica.Current?.World, network?.Client.PlayerSlot ?? -1, network?.Client.Ready == true);
            if (panel != null && Time.unscaledTime <= emergencyConfirmation)
                panel.Status.text += "\n再次点击紧急起飞确认以下损失；4 秒后取消确认。";
        }
        private async void Submit(string operation)
        {
            try
            {
                var client = network.Client; var frame = client.Replica.Current;
                if (!client.Ready || frame?.World.Expedition == null) return;
                if (operation == "emergency" && Time.unscaledTime > emergencyConfirmation)
                { emergencyConfirmation = Time.unscaledTime + 4; return; }
                emergencyConfirmation = -1;
                bool personal = operation is "unload" or "board" or "relay" or "mine" or "pilot" or "takeoff" or "land" or "cancel-flight" or "deploy";
                var actor = frame.World.Actors.FirstOrDefault(a => a.ControllerSlot == client.PlayerSlot);
                int target = operation == "mine" && actor != null ? frame.World.Worksites.Where(w => w.IsMineralDeposit && w.Amount > 0)
                    .OrderBy(w => Math.Abs(w.X - actor.X) + Math.Abs(632 - (w.Y + .5) * 16 - actor.Height)).FirstOrDefault()?.Id ?? 0 : 0;
                await client.Send(SessionOperation.Expedition, personal && actor != null ? new[] { actor.Id } : null,
                    target: target, kind: operation, controlLease: personal ? actor?.ControlLease ?? 0 : 0);
            }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
