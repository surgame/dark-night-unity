using DarkNights.Runtime.Network;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 主菜单只读连接阶段与有界等待；阶段取真实兼容、完整投影及Ready，不播放伪造百分比。
    /// 超时通过SessionNetwork终止本次尝试，取消后不再推进，权威状态与身份不由此视图助手修改。
    /// </summary>
    internal sealed class MainMenuConnectionState
    {
        private readonly SessionNetwork network;
        private readonly MainMenuPanel panel;
        private bool gameplay, hosting;
        private double deadline;
        private string name;
        internal bool Pending { get; private set; }

        internal MainMenuConnectionState(SessionNetwork network, MainMenuPanel panel)
        { this.network = network; this.panel = panel; }

        internal void Joining(string room)
        {
            name = room; Pending = true; gameplay = false; deadline = Time.realtimeSinceStartupAsDouble + 20;
            Present();
        }

        internal void Begin(string room, bool host)
        {
            name = room; Pending = gameplay = true; hosting = host;
            deadline = Time.realtimeSinceStartupAsDouble + 60; Present();
        }

        internal void Cancel() { Pending = false; }

        internal void Present()
        {
            if (!Pending || panel.Page == "password") return;
            if (Time.realtimeSinceStartupAsDouble > deadline)
            {
                Pending = false; network.Disconnect();
                panel.Failure("连接或同步超时，请检查房主状态后重新加入。", true); return;
            }
            if (!gameplay)
            {
                panel.Waiting("正在加入房间", name, network.Rooms.JoiningPhase, "等待房主校验房间与加入权限。");
                return;
            }
            int phase = network.Client.Ready ? 3 : network.Client.Replica.Current != null ? 2 :
                network.Rooms.CompatibilityAccepted ? 1 : 0;
            string detail = network.Client.Replica.Current == null ? network.Status : network.Client.Status;
            if (!hosting) panel.Waiting("正在连接房主", name, phase, detail);
            if (network.Client.Ready) Pending = false;
            else if (!hosting && (network.Status == "连接已结束" || network.Status.StartsWith("定义协议") || network.Status.StartsWith("房间准入")))
            { Pending = false; panel.Failure(network.Status, true); }
        }
    }
}
