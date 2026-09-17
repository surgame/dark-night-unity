using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using DarkNights.View;
using GameCore.Interactions;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.UI.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.Entry
{
    /// <summary>
    /// 把既有 UGUIManager 创建的面板、客户端输入与同一网络命令入口装配起来，不持有权威规则状态。
    /// 面板和 Interaction Session 随应用释放；每次连接或 epoch 变化清空选择，Ready 前不发业务命令。
    /// </summary>
    public sealed class SessionUiController : MonoBehaviour
    {
        private readonly Dictionary<string, ObjectInstance> panels = new Dictionary<string, ObjectInstance>();
        private SessionNetwork network;
        private CampInput input;
        private GameInputActions actions;
        private HeroPlayerController hero;
        private SessionEntityViews entities;
        private PinewatchStage stage;
        private MainMenuBehaviour main;
        private PauseMenuBehaviour pause;
        private ResultMenuBehaviour result;
        private HelpMenuBehaviour help;
        private CampHudBehaviour hud;
        private YYInteractionSessionHandle modal;
        private GameCatalog catalog;
        private string page = "MainMenu", returnPage = "";
        private int epoch;
        private long generation;
        private bool initialized;
        private int saveSlot;
        private bool continuePending;
        private string storageStatus = "";
        public CampInput Input => input;
        public string Page => page;
        public void PresentEvent(PresentationEvent value, double age) => hud.PresentEvent(value, age);

        public void ActivateButton(string panel, string key)
        {
            Button button = View(panel).Get<Button>(key);
            if (!button.gameObject.activeInHierarchy || !button.interactable)
                throw new InvalidOperationException("Button is not available: " + panel + "/" + key);
            button.onClick.Invoke();
        }

        public async UniTask Initialize(SessionNetwork session, GameCatalog rules, PinewatchStage stage, SessionEntityViews entities)
        {
            network = session;
            this.entities = entities;
            this.stage = stage;
            catalog = rules;
            actions = gameObject.AddComponent<GameInputActions>();
            actions.Initialize(stage.InputPlayer, YYInteractionSessionService.Instance);
            input = gameObject.AddComponent<CampInput>();
            input.Initialize(stage, entities, YYInteractionSessionService.Instance, actions);
            input.Intent += intent => Execute(intent).Forget();
            entities.SetIntentHandler(intent => Execute(intent).Forget());
            // Existing framework root owns the scaler; formal UI preserves source pixel sizes at each viewport.
            UGUIManager.Instance.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            UGUIManager.Instance.GetComponent<Canvas>().pixelPerfect = true;
            foreach (string name in new[] { "Chrome", "MainMenu", "PauseMenu", "Help", "Result", "Hero" })
            {
                var definition = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("ui." + name.ToLowerInvariant());
                ObjectInstance panel = await UGUIManager.Instance.CreatePanelInstanceAsync(definition);
                if (this == null) { if (panel != null) Destroy(panel.gameObject); return; }
                if (panel == null) throw new InvalidOperationException("Cannot create native UI: " + name);
                panels.Add(name, panel);
                MenuBehaviour behaviour = panel.GetAllBehaviors().OfType<MenuBehaviour>().Single();
                behaviour.Action += action => Execute(new InputIntent(action, input.ActorIds(),
                    action == "Repair" && input.Selected.Count == 1 ? input.Selected[0] : 0)).Forget();
                panel.gameObject.SetActive(false);
            }
            main = Behaviour<MainMenuBehaviour>("MainMenu");
            pause = Behaviour<PauseMenuBehaviour>("PauseMenu");
            result = Behaviour<ResultMenuBehaviour>("Result");
            help = Behaviour<HelpMenuBehaviour>("Help");
            hud = Behaviour<CampHudBehaviour>("Chrome");
            hud.Configure(catalog);
            hero = gameObject.AddComponent<HeroPlayerController>();
            hero.Initialize(network, input, actions, stage, Behaviour<HeroHudBehaviour>("Hero"), entities);
            network.Client.Feedback += Feedback;
            network.Failed += Failed;
            if (network.Terrain != null) View("MainMenu").Get<Button>("Map").onClick.AddListener(() => Execute(new InputIntent("SelectMap", Array.Empty<int>())).Forget());
            View("MainMenu").Get<Button>("Map").gameObject.SetActive(network.Terrain != null);
            initialized = true;
            Switch("MainMenu");
        }

        internal void SamplePresentation()
        {
            Update();
            hud.ResetMessages();
            Update();
        }

        private void Update()
        {
            if (!initialized) return;
            var frame = network.Client.Replica.Current;
            string slotPath = System.IO.Path.Combine(network.SaveDirectory, $"slot-{saveSlot:D2}.dnsave.json");
            bool exists = System.IO.File.Exists(slotPath);
            string slotLabel = $"存档槽位 {saveSlot + 1} / 10 · {(exists ? "已有存档" : "空槽位")} · 点击切换";
            View("MainMenu").Get<Text>("SlotLabel").text = slotLabel;
            View("PauseMenu").Get<Text>("SlotLabel").text = slotLabel;
            View("MainMenu").Get<Button>("Continue").interactable = exists && !continuePending;
            bool canStore = network.Client.Ready && network.Client.PlayerSlot == 0 && network.Server?.Storage.Busy == false;
            View("PauseMenu").Get<Button>("Save").interactable = canStore;
            View("PauseMenu").Get<Button>("Load").interactable = canStore && exists;
            View("Result").Get<Button>("NewGame").interactable = canStore;
            string currentStatus = network.Server?.Storage.Status ?? "";
            if (currentStatus != storageStatus) { storageStatus = currentStatus; if (storageStatus.Length != 0) hud.ShowMessage(storageStatus); }
            if (continuePending && network.Client.Ready)
            {
                continuePending = false;
                Execute(new InputIntent("Load", Array.Empty<int>())).Forget();
            }
            if (generation != network.Client.ConnectionGeneration || epoch != (frame?.Epoch ?? 0))
            {
                input.ResetLocal();
                hud.ResetMessages();
                generation = network.Client.ConnectionGeneration;
                epoch = frame?.Epoch ?? 0;
                // 终局菜单属于旧世界，新 epoch 到达时同时释放 Host 和来宾的旧模态。
                if (page == "Result") Switch("");
            }
            input.Present(frame, network.Client.Ready);
            actions.Present(network.Client.Ready);
            hero.Present(frame, page.Length != 0);
            panels["Hero"].gameObject.SetActive(frame != null && page.Length == 0);
            if (frame == null)
            {
                if (page != "MainMenu" && page != "Help") Switch("MainMenu");
                main.ShowStatus(network.Status == "未连接" || network.Status == "连接已结束"
                    ? (network.Terrain?.SelectionStatus ?? "创建房间后，可供同一局域网的玩家加入。") : network.Status);
                panels["Chrome"].gameObject.SetActive(false);
                return;
            }
            if (network.Client.Ready && page == "MainMenu") Switch("");
            panels["Chrome"].gameObject.SetActive(true);
            hud.Present(frame, input.Selected, input.BuildKind, input.Hover, network.Client.Ready,
                network.Client.PlayerSlot, Portrait(frame), page.Length != 0, actions.HeroMode);
            hud.PresentWorld(frame, input, entities, stage, network.Client.Ready && page.Length == 0,
                network.Terrain?.DataReady == true ? network.Terrain.Replica : null);
            pause.Present($"玩家 {frame.PlayerCount} / 4 · {(frame.HostOnly ? "仅房主控制" : "共享营地控制")}",
                network.Client.PlayerSlot == 0 ? (storageStatus.Length != 0 ? storageStatus : "房主拥有时间与营地控制设置权限。") : "来宾可操作共享营地，时间与存档由房主控制。");
            View("PauseMenu").Get<Button>("ControlMode").interactable = network.Client.Ready && network.Client.PlayerSlot == 0;
            if (frame.World.Camp.Mode == "Won" || frame.World.Camp.Mode == "Lost")
            {
                bool won = frame.World.Camp.Mode == "Won";
                result.Present(won, won ? "黎明如约而至" : "最后一盏灯熄灭了", won
                    ? "灰松谷守住了三个长夜，幸存者迎来新的清晨。"
                    : "酒馆已经失守。重新安排生产与防线，再守一次长夜。",
                    $"守候时间  {GameText.Clock(frame.Elapsed)}\n击退敌人  {frame.World.Camp.Kills}\n" +
                    $"幸存居民  {frame.World.Camp.Population}    ·    牺牲居民  {frame.World.Camp.Lost}");
                if (page == "") Switch("Result");
            }
        }

        private Sprite Portrait(SessionViewData frame)
        {
            int id = input.Selected.Count == 1 ? input.Selected[0] : input.Selected.Count > 1
                ? frame.World.Actors.FirstOrDefault(a => a.Kind == "spearman")?.Id ?? 0
                : frame.World.Buildings.FirstOrDefault(b => b.Kind == "tavern")?.Id ?? 0;
            return entities.Visual(id)?.Portrait;
        }

        private async UniTask Execute(InputIntent intent)
        {
            try
            {
                string action = intent.Action;
                if (await hero.HandleAction(action)) return;
                if (actions.HeroMode && (action.StartsWith("Build", StringComparison.Ordinal) ||
                    action.StartsWith("Train", StringComparison.Ordinal) || action == "Orders"))
                { hud.ShowMessage("当前默认主角操控，旧营地操控入口暂时隐藏。"); return; }
                if (action == "SelectMap") { await network.Terrain.SelectNew(); return; }
                if (action == "Slot") { saveSlot = (saveSlot + 1) % 10; return; }
                if (action == "Quit") { network.Disconnect(); Application.Quit(); return; }
                if (action == "NewGame" && network.Client.Replica.Current != null)
                {
                    // 等待新世界发布后关闭终局页，避免请求回执先到时被旧 Won／Lost 帧重新打开。
                    await network.Client.Send(SessionOperation.Restart); return;
                }
                if (action == "NewGame" || action == "Join" || action == "Continue")
                {
                    continuePending = action == "Continue";
                    if (action != "Join" && network.Terrain != null) await network.Terrain.EnsureSelected();
                    if (network.Client.Replica.Current != null) network.Disconnect();
                    while (network.Hosting) await UniTask.Yield();
                    await network.Connect(action != "Join", action == "Join" ? main.Address.Trim() : "127.0.0.1", 27777);
                    return;
                }
                if (action == "MainMenu") { continuePending = false; network.Disconnect(); input.ResetLocal(); Switch("MainMenu"); return; }
                if (action == "Help") { returnPage = page; Switch("Help"); return; }
                if (action == "Back") { Switch(returnPage); return; }
                if (action == "Resume") { Switch(""); return; }
                if (action == "Menu") { Switch(page.Length == 0 ? "PauseMenu" : ""); return; }
                if (action == "Mute")
                {
                    AudioListener.pause = !AudioListener.pause;
                    View("PauseMenu").Get<Text>("MuteLabel").text = AudioListener.pause ? "开启声音" : "关闭声音";
                    return;
                }
                if (action.StartsWith("Build", StringComparison.Ordinal) && action != "Build")
                {
                    input.BeginBuild(action.Substring(5).ToLowerInvariant()); return;
                }
                var frame = network.Client.Replica.Current;
                if (frame == null || !network.Client.Ready) return;
                if (action == "Orders") await network.Client.Send(SessionOperation.IssueOrders, intent.Actors, intent.Target, intent.X);
                else if (action == "Build") await network.Client.Send(SessionOperation.PlaceBuilding, intent.Actors, x: intent.X, kind: intent.Kind);
                else if (action.StartsWith("Train", StringComparison.Ordinal))
                    await network.Client.Send(SessionOperation.TrainActors, intent.Actors, kind: action.Substring(5).ToLowerInvariant());
                else if (action == "Recruit") await network.Client.Send(SessionOperation.Recruit);
                else if (action == "Repair") await network.Client.Send(SessionOperation.Repair, target: intent.Target);
                else if (action == "Pause") await network.Client.Send(SessionOperation.SetPaused, value: frame.Paused ? 0 : 1);
                else if (action == "Speed") await network.Client.Send(SessionOperation.SetSpeed, value: frame.Speed == 1 ? 2 : 1);
                else if (action == "Night") await network.Client.Send(SessionOperation.StartNight);
                else if (action == "ControlMode") await network.Client.Send(SessionOperation.SetControlMode, value: frame.HostOnly ? 0 : 1);
                else if (action == "Save") await network.Client.Send(SessionOperation.Save, value: saveSlot);
                else if (action == "Load") await network.Client.Send(SessionOperation.BeginLoad, value: saveSlot);
            }
            catch (Exception error) { Failed(error); }
        }

        private void Switch(string value)
        {
            modal?.Dispose(); modal = null;
            page = value;
            if (page == "Help") help.Present(hero.JumpBindingLabel);
            input.CancelBuild();
            if (page.Length != 0) modal = YYInteractionSessionService.Instance.Begin(new YYInteractionSessionDescriptor
            {
                Kind = "dark_nights.modal", Owner = "SessionUiController", Priority = 100,
                Blocks = YYInteractionBlockFlags.All, ConflictPolicy = YYInteractionConflictPolicy.CancelLowerPriority
            });
            foreach (var pair in panels) if (pair.Key != "Chrome" && pair.Key != "Hero") pair.Value.gameObject.SetActive(pair.Key == page);
        }

        private void Feedback(CommandFeedback value)
        {
            if (value.ReadyReply || value.Code == "Applied") return;
            hud.ShowMessage(value.Code switch
            {
                "NoEffect" => "当前无法执行该操作，请检查目标、资源和空余名额。",
                "PermissionDenied" => "当前控制权限不允许执行此操作。",
                "PolicyChanged" => "营地控制设置已变化，请重新操作。",
                "EpochChanged" => "营地已经载入，请等待同步完成。",
                "QueueFull" => "操作请求较多，请稍后重试。",
                "NotReady" => "正在同步营地，请稍候。",
                _ => "操作未执行，请检查当前连接和目标。"
            });
        }

        private void Failed(Exception error)
        {
            continuePending = false;
            main?.ShowStatus(error.Message);
            hud?.ShowMessage(error.Message);
            Debug.LogException(error);
        }

        private T Behaviour<T>(string key) where T : MenuBehaviour => panels[key].GetAllBehaviors().OfType<T>().Single();
        private UGUIView View(string key) => panels[key].GetView<UGUIView>();
        private void OnDestroy()
        {
            if (entities != null) entities.SetIntentHandler(null);
            modal?.Dispose();
            if (network != null) { network.Client.Feedback -= Feedback; network.Failed -= Failed; }
            foreach (ObjectInstance panel in panels.Values) if (panel != null) Destroy(panel.gameObject);
            panels.Clear();
        }
    }
}
