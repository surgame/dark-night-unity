using System;
using System.IO;
using System.Net;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 当前应用主菜单的Entry装配；房间、密码和Ready交给SessionNetwork拥有的原生服务，游戏命令仍走原入口。
    /// 新局后台选空槽、继续读取最近存档，确认后直接启动游戏；销毁时解除订阅，取消后旧尝试不能重新打开世界。
    /// </summary>
    internal sealed class MainMenuCoordinator : IDisposable
    {
        private readonly SessionNetwork network;
        private readonly Func<string, UniTask> execute;
        private readonly Func<int> slot;
        private readonly Action<int> selectSlot;
        private readonly MainMenuDocument document = new MainMenuDocument();
        private RoomListingData selected;
        private MainMenuConnectionState connection;
        private bool continuing, passwordAttempted, dirty = true, disposed, starting;
        private int recentSlot = -1;
        private double nextSaveCheck;
        internal MainMenuPanel Panel => document.Panel;
        internal string Address => network.Rooms.Active ? network.Rooms.TargetAddress : Panel.Address;

        internal MainMenuCoordinator(SessionNetwork network, Func<string, UniTask> execute,
            Func<int> slot, Action<int> selectSlot)
        {
            this.network = network; this.execute = execute; this.slot = slot; this.selectSlot = selectSlot;
        }

        internal async UniTask Initialize(Transform parent)
        {
            await document.Initialize(parent);
            if (disposed) { document.Dispose(); return; }
            Panel.SetVisible(false);
            connection = new MainMenuConnectionState(network, Panel);
            Panel.Requested += Requested; Panel.RoomChosen += Choose;
            network.Rooms.Changed += RoomChanged;
            network.Rooms.PasswordRequired += PasswordNeeded;
            network.Rooms.StartClientRequested += StartClient;
            network.Rooms.Rejected += Rejected;
            network.Failed += Failed;
        }

        internal void SetVisible(bool visible)
        {
            if (visible && network.Rooms.Stage == "Starting" && connection?.Pending == false &&
                !network.Client.Ready && network.Status == "连接已结束")
            {
                network.Disconnect();
                Panel.Failure("与房主的连接已断开，请重新加入房间。", true);
            }
            if (visible && !network.Rooms.Active && Panel?.Page == "waiting")
                Panel.Show("home");
            Panel?.SetVisible(visible);
        }

        internal void ReportFailure(Exception error) => Failed(error);

        internal void Present(int saveSlot, bool exists, bool visible)
        {
            if (Panel == null) return;
            if (!visible) return;
            connection.Present();
            RefreshSaves();
            Panel.PresentStart(recentSlot >= 0, !starting && !connection.Pending && network.Terrain?.Selecting != true);
            if (!dirty) return;
            dirty = false;
            if (Panel.Page == "browser")
                Panel.PresentRooms(Panel.Friends ? Array.Empty<RoomListingData>() : network.Rooms.Listings,
                    Panel.Friends ? "好友服务尚未接入。当前可使用局域网。" :
                    network.Rooms.Listings.Count == 0 ? "暂未发现待加入房间，可重新搜索或使用地址直连。" : "");
        }

        private void Requested(string action) => Handle(action).Forget();
        private async UniTask Handle(string action)
        {
            if (disposed) return;
            try
            {
                switch (action)
                {
                    case "Prepare": continuing = false; OpenStart(); break;
                    case "ContinuePrepare":
                        RefreshSaves(true);
                        if (recentSlot < 0) break;
                        selectSlot(recentSlot); continuing = true; OpenStart(); break;
                    case "Home":
                        if (network.Rooms.Active) LeaveConfirmation();
                        else { network.Rooms.Browse(false); Panel.ClearHostPassword(); Panel.Show("home"); }
                        break;
                    case "Browser": OpenBrowser(); break;
                    case "Direct": Panel.Show("direct"); break;
                    case "Settings": Panel.Show("settings"); break;
                    case "Local": Panel.SetMode("local"); break;
                    case "Lan": Panel.SetMode("lan"); break;
                    case "Friends": Panel.SetMode("friends"); break;
                    case "LanTab": Panel.SetSource(false); OpenBrowser(); break;
                    case "FriendsTab": Panel.SetSource(true); OpenBrowser(); break;
                    case "Refresh": network.Rooms.Browse(!Panel.Friends); dirty = true; break;
                    case "Create": await Create(); break;
                    case "DirectJoin": Direct(); break;
                    case "PasswordJoin": SubmitPassword(); break;
                    case "Cancel": Cancel(); break;
                    case "Retry": if (selected != null) Choose(selected); else OpenBrowser(); break;
                    case "LeaveConfirm": LeaveConfirmation(); break;
                    case "Leave": network.Disconnect(); connection.Cancel(); Panel.Show("home"); break;
                    case "QuitConfirm": Panel.Confirm("退出游戏？", "确认离开Dark Nights？", "Quit"); break;
                    case "Quit": await execute("Quit"); break;
                    case "Help": await execute("Help"); break;
                }
            }
            catch (Exception error) { Failed(error); }
        }

        private async UniTask Create()
        {
            if (starting || connection.Pending || !network.CanStartSession) return;
            if (Panel.Mode == "friends") { Panel.Text("prepareError", "好友服务尚未接入。"); return; }
            int target = continuing ? slot() : EmptySlot();
            if (target < 0 || continuing && !File.Exists(SavePath(target)))
            { Panel.Text("prepareError", continuing ? "该存档已不存在。" : "没有空存档位，请先整理现有存档。"); return; }
            starting = true;
            try
            {
                selectSlot(target);
                network.Rooms.Host(Panel.RoomName, Panel.Mode == "lan" ? Panel.HostPassword : "", Panel.Mode == "local");
                Panel.ClearHostPassword();
                network.Rooms.BeginStart(); connection.Begin(Panel.RoomName, true);
                await execute(continuing ? "Continue" : "NewGame");
            }
            finally { starting = false; }
        }

        private void OpenStart()
        { Panel.Text("prepareError", Panel.Mode == "friends" ? "好友服务尚未接入。" : ""); Panel.Show("prepare"); }

        private string SavePath(int value) => Path.Combine(network.SaveDirectory, $"slot-{value:D2}.dnsave.json");
        private int EmptySlot()
        { for (int i = 0; i < 10; i++) if (!File.Exists(SavePath(i))) return i; return -1; }

        private void RefreshSaves(bool force = false)
        {
            if (!force && Time.realtimeSinceStartupAsDouble < nextSaveCheck) return;
            nextSaveCheck = Time.realtimeSinceStartupAsDouble + 1;
            recentSlot = -1;
            DateTime newest = DateTime.MinValue;
            for (int i = 0; i < 10; i++)
            {
                if (!File.Exists(SavePath(i))) continue;
                DateTime changed = File.GetLastWriteTimeUtc(SavePath(i));
                if (recentSlot >= 0 && changed <= newest) continue;
                newest = changed; recentSlot = i;
            }
        }

        private void OpenBrowser()
        {
            if (network.Rooms.Active) return;
            Panel.Show("browser"); network.Rooms.Browse(!Panel.Friends); dirty = true;
        }

        private void Choose(RoomListingData value)
        {
            if (network.Rooms.Active || connection.Pending) return;
            selected = value; passwordAttempted = false;
            if (value.RequiresPassword) { Panel.Password(value.Name); return; }
            try { network.Rooms.Join(value, ""); connection.Joining(value.Name); }
            catch (Exception error) { Failed(error); }
        }

        private void Direct()
        {
            if (!IPAddress.TryParse(Panel.Address, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            { Panel.Text("directError", "请输入合法的IPv4地址。"); return; }
            selected = new RoomListingData("", Panel.Address, Panel.Address, 27777, 0, null, false, true);
            passwordAttempted = false;
            network.Rooms.JoinAddress(Panel.Address, 27777); connection.Joining(Panel.Address);
        }

        private void SubmitPassword()
        {
            if (Panel.Page != "password") return;
            string password = Panel.JoinPassword;
            if (password.Length == 0) { Panel.Text("passwordError", "请输入房间密码。"); return; }
            passwordAttempted = true; Panel.ClearPassword();
            if (network.Rooms.Stage == "Password") network.Rooms.SubmitPassword(password);
            else if (selected != null) network.Rooms.Join(selected, password);
            else return;
            connection.Joining(selected?.Name ?? "房间");
        }

        private void Cancel()
        {
            connection.Cancel();
            if (network.Rooms.Active || network.Client.Replica.Current != null) network.Disconnect();
            Panel.ClearPassword(); Panel.ClearHostPassword(); OpenBrowser();
        }

        private void LeaveConfirmation() => Panel.Confirm("离开队伍？",
            network.Rooms.IsHost ? "关闭房间后，队友将断开连接。不会删除任何存档。" : "离开当前队伍并返回主菜单。", "Leave");
        private void RoomChanged() { dirty = true; }
        private void PasswordNeeded()
        {
            connection.Cancel();
            Panel.Password(selected?.Name ?? "房间", passwordAttempted ? "密码不正确，请重试。" : "");
        }

        private void StartClient() => BeginClient().Forget();
        private async UniTask BeginClient()
        {
            connection.Begin(network.Rooms.Lobby?.Name ?? "房间", false);
            await execute("Join");
        }

        private void Rejected(string reason)
        {
            connection.Cancel(); network.Disconnect();
            string message = reason switch
            {
                "Started" => "房主已开始远征，该房间不再接受加入。",
                "Preparing" => "房主正在准备远征，请稍后使用地址重试。",
                "Full" => "房间已满，请选择其他房间。",
                "Closed" => "房主已关闭房间。",
                "Version" => "房间的游戏版本或内容与本机不一致。",
                "Admission" => "房间准入已失效，请重新加入。",
                "Network" => "局域网服务不可用，请检查网络或端口占用。",
                _ => "等待房主响应超时，请检查网络后重试。"
            };
            Panel.Failure(message, reason == "Timeout");
        }

        private void Failed(Exception error)
        {
            connection?.Cancel();
            if (network.Rooms.Active) network.Disconnect();
            Panel?.Failure(error.Message, false);
        }

        public void Dispose()
        {
            disposed = true;
            network.Rooms.Changed -= RoomChanged; network.Rooms.PasswordRequired -= PasswordNeeded;
            network.Rooms.StartClientRequested -= StartClient; network.Rooms.Rejected -= Rejected;
            network.Failed -= Failed;
            if (Panel != null) { Panel.Requested -= Requested; Panel.RoomChosen -= Choose; }
            document.Dispose();
        }
    }
}
