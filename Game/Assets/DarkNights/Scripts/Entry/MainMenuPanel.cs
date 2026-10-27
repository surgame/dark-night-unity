using System;
using System.Collections.Generic;
using DarkNights.Core.ViewData;
using GameCore.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 主菜单的YYGC原生UITK视图；UXML是布局来源，页面和输入只属于当前本地菜单。
    /// 仅发布意图与房间选择，不生成测试房间，不决定密码正确性、网络Ready或存档结果。
    /// </summary>
    [UIAsset("dark_nights.ui.main_menu")]
    public sealed class MainMenuPanel : UIPanel
    {
        private MainMenuRoomList roomList;
        private readonly List<(Button button, Action callback)> buttons = new List<(Button, Action)>();
        private string confirmAction, confirmReturn;
        public event Action<string> Requested;
        public event Action<RoomListingData> RoomChosen;
        public string Page { get; private set; } = "home";
        public string Mode { get; private set; } = "lan";
        public bool Friends { get; private set; }
        public string RoomName => Root.Q<TextField>("roomName").value;
        public string Address => Root.Q<TextField>("address").value.Trim();
        public string HostPassword => Root.Q<TextField>("hostPassword").value;
        public string JoinPassword => Root.Q<TextField>("roomPassword").value;

        protected override void OnBind()
        {
            Root.AddToClassList("main-menu-panel");
            Root.Query<Label>().ForEach(label => label.enableRichText = false);
            roomList = new MainMenuRoomList(Root.Q<ListView>("rooms"));
            roomList.Chosen += ChooseRoom;
            foreach (var binding in new[]
            {
                ("openPrepare","Prepare"),("continueGame","ContinuePrepare"),("openJoin","Browser"),
                ("openSettings","Settings"),("openQuit","QuitConfirm"),("prepareBack","Home"),
                ("createRoom","Create"),("browserBack","Home"),("openDirect","Direct"),
                ("refreshRooms","Refresh"),("directBack","Browser"),("joinAddress","DirectJoin"),
                ("passwordBack","Cancel"),("joinPassword","PasswordJoin"),("cancelJoin","Cancel"),
                ("failureBack","Cancel"),("retryJoin","Retry"),("settingsBack","Home"),
                ("openHelp","Help"),("confirmYes","ConfirmYes"),("confirmNo","ConfirmNo"),
                ("showPassword","ShowPassword"),("localMode","Local"),("lanMode","Lan"),
                ("friendsMode","Friends"),("friendsTab","FriendsTab"),("lanTab","LanTab")
            }) Bind(binding.Item1, binding.Item2);
            Root.RegisterCallback<KeyDownEvent>(Key);
            Root.RegisterCallback<GeometryChangedEvent>(Geometry);
            Root.Q<Slider>("volume").SetValueWithoutNotify(AudioListener.volume);
            Root.Q<Slider>("volume").RegisterValueChangedCallback(Volume);
            Root.Q<Toggle>("fullscreen").SetValueWithoutNotify(Screen.fullScreen);
            Root.Q<Toggle>("fullscreen").RegisterValueChangedCallback(Fullscreen);
            SetMode(Mode);
            Show("home");
        }

        private void Bind(string name, string action)
        {
            Button button = Root.Q<Button>(name);
            if (button == null) throw new InvalidOperationException("主菜单缺少UXML绑定：" + name);
            Action callback = () => Invoke(action);
            button.clicked += callback; buttons.Add((button, callback));
        }

        public void Invoke(string action)
        {
            if (action == "ConfirmYes") { Requested?.Invoke(confirmAction); return; }
            if (action == "ConfirmNo") { Show(confirmReturn); return; }
            if (action == "ShowPassword")
            {
                var field = Root.Q<TextField>("roomPassword");
                field.isPasswordField = !field.isPasswordField;
                Root.Q<Button>("showPassword").text = field.isPasswordField ? "显示密码" : "隐藏密码";
                return;
            }
            Requested?.Invoke(action);
        }

        public void Activate(string key)
        {
            string name = key switch { "NewGame" => "openPrepare", "Continue" => "continueGame",
                "Join" => "openJoin", "Help" => "openSettings", "Quit" => "openQuit",
                _ => key };
            Button button = Root.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy || button.worldBound.width <= 0 ||
                !Root.Q(Page).Contains(button)) throw new InvalidOperationException("主菜单按钮不可用：" + key);
            using var submit = NavigationSubmitEvent.GetPooled();
            button.SendEvent(submit);
        }

        public void Show(string page)
        {
            Page = page;
            foreach (string name in new[] { "home", "prepare", "browser", "direct", "password",
                "waiting", "failure", "settings", "confirm" })
                Root.Q(name).EnableInClassList("menu-hidden", name != page);
            if (page != "password") ClearPassword();
            if (page == "password") Root.Q<TextField>("roomPassword").Focus();
        }

        public void SetVisible(bool visible) => Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void SetMode(string mode)
        {
            Mode = mode;
            Root.Q("localMode").EnableInClassList("menu-selected", mode == "local");
            Root.Q("lanMode").EnableInClassList("menu-selected", mode == "lan");
            Root.Q("friendsMode").EnableInClassList("menu-selected", mode == "friends");
            Root.Q("hostFields").EnableInClassList("menu-host-fields-local", mode == "local");
            Root.Q("hostFields").SetEnabled(mode != "local");
            Text("prepareError", mode == "friends" ? "好友服务尚未接入。" : "");
            Root.Q<Button>("createRoom").SetEnabled(mode != "friends");
        }

        public void SetSource(bool friends)
        {
            Friends = friends;
            Root.Q("friendsTab").EnableInClassList("menu-selected", friends);
            Root.Q("lanTab").EnableInClassList("menu-selected", !friends);
        }

        public void PresentStart(bool hasSave, bool available)
        {
            Root.Q<Button>("continueGame").SetEnabled(hasSave);
            Root.Q<Button>("createRoom").SetEnabled(Mode != "friends" && available);
            Root.Q("hostFields").SetEnabled(Mode != "local" && available);
            Root.Q("localMode").SetEnabled(available);
            Root.Q("lanMode").SetEnabled(available);
            Root.Q("friendsMode").SetEnabled(available);
        }

        public void PresentRooms(IReadOnlyList<RoomListingData> rooms, string hint)
        {
            roomList.Present(rooms);
            Text("roomCount", Friends ? "" : rooms.Count + " 个待加入房间");
            Text("browserHint", hint);
        }

        public void Password(string room, string error = "")
        {
            Show("password"); Text("passwordRoom", room); Text("passwordError", error);
        }

        public void Waiting(string title, string room, int phase, string detail)
        {
            if (Page != "waiting") Show("waiting");
            Text("waitingTitle", title); Text("waitingRoom", room); Text("waitingDetail", detail);
            for (int i = 0; i < 4; i++)
            {
                Root.Q("phase" + i).EnableInClassList("menu-current", i == phase);
                Root.Q("phase" + i).EnableInClassList("menu-done", i < phase);
            }
        }

        public void Failure(string message, bool retry)
        {
            Show("failure"); Text("failureMessage", message); Root.Q("retryJoin").EnableInClassList("menu-hidden", !retry);
        }

        public void Confirm(string title, string text, string action)
        {
            confirmAction = action; confirmReturn = Page;
            Text("confirmTitle", title); Text("confirmText", text); Show("confirm");
        }

        public void ClearPassword()
        {
            Root.Q<TextField>("roomPassword").SetValueWithoutNotify("");
            Root.Q<TextField>("roomPassword").isPasswordField = true;
            Root.Q<Button>("showPassword").text = "显示密码";
        }

        public void ClearHostPassword() => Root.Q<TextField>("hostPassword").SetValueWithoutNotify("");
        public void Text(string name, string value)
        {
            var label = Root.Q<Label>(name);
            label.text = value ?? "";
            if (name == "prepareError") label.EnableInClassList("menu-hidden", string.IsNullOrEmpty(value));
        }
        private void ChooseRoom(RoomListingData room) { if (Page == "browser") RoomChosen?.Invoke(room); }
        private void Volume(ChangeEvent<float> value) => AudioListener.volume = value.newValue;
        private void Fullscreen(ChangeEvent<bool> value) => Screen.fullScreen = value.newValue;

        private void Geometry(GeometryChangedEvent value)
        {
            bool narrow = value.newRect.width < 900;
            Root.Q("menuRoot").EnableInClassList("menu-narrow", narrow);
            roomList.SetNarrow(narrow);
        }

        private void Key(KeyDownEvent value)
        {
            if (value.keyCode == KeyCode.Escape)
            {
                Invoke(Page == "confirm" ? "ConfirmNo" :
                    Page == "waiting" || Page == "password" || Page == "failure" ? "Cancel" : "Home");
                value.StopPropagation();
            }
            if (value.keyCode == KeyCode.Return && Page == "password")
            { Invoke("PasswordJoin"); value.StopPropagation(); }
        }

        protected override void OnDispose()
        {
            foreach (var binding in buttons) binding.button.clicked -= binding.callback;
            buttons.Clear(); roomList.Chosen -= ChooseRoom; roomList.Dispose();
            Root.UnregisterCallback<KeyDownEvent>(Key); Root.UnregisterCallback<GeometryChangedEvent>(Geometry);
            Root.Q<Slider>("volume").UnregisterValueChangedCallback(Volume);
            Root.Q<Toggle>("fullscreen").UnregisterValueChangedCallback(Fullscreen);
            ClearPassword(); ClearHostPassword(); Requested = null; RoomChosen = null;
        }
    }
}
