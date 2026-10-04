using System;
using System.Linq;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using DarkNights.View;
using DarkNights.View.Expedition;
using GameCore.Interactions;
using GameCore.Logging;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using GameCore.Debugging;
#endif

namespace DarkNights.Entry
{
    /// <summary>驾驶台交互与远征调试页的命令装配；共用可信 Send，常驻按钮移入开发版 Hub，不预扣客户端资源。</summary>
    public sealed class ExpeditionHud : MonoBehaviour
    {
        private SessionNetwork network;
        private ExpeditionPanel panel;
        private DestinationPicker picker;
        private bool awaitingJourney;
        private long journeySequence;
        private CommandFeedback earlyFeedback;
        private float requestTime;
        private float emergencyConfirmation = -1;
        private string lastJourneyError = "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private ExpeditionDebugPanel debugPanel;
#endif
        public void Initialize(SessionNetwork network, ExpeditionPanel panel)
        {
            this.network = network; this.panel = panel; panel.Bind(Submit);
            picker = gameObject.AddComponent<DestinationPicker>();
            picker.Initialize(panel, YYInteractionSessionService.Instance, SelectDestination);
            network.Client.Feedback += OnFeedback;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugPanel = new ExpeditionDebugPanel(panel, Submit);
            RuntimeDebugHub.RegisterPanel(debugPanel);
#endif
        }
        private void Update()
        {
            var client = network?.Client; var frame = client?.Replica.Current;
            string error = frame?.World.Expedition?.Journey?.Error ?? "";
            if (error != lastJourneyError && error.Length != 0)
                YYLogger.LogError("航程: " + error, LoggingChannel.Gameplay);
            lastJourneyError = error;
            if (awaitingJourney && Time.unscaledTime - requestTime > 12)
            {
                awaitingJourney = false;
                YYLogger.LogWarning("暂未收到确认，请检查航程状态后重试。", LoggingChannel.Network);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            panel?.Present(frame?.World, client?.PlayerSlot ?? -1, client?.Ready == true, frame?.HostOnly == true, frame?.Paused == true);
#endif
            picker?.Present(frame, client?.PlayerSlot ?? -1, client?.Ready == true, awaitingJourney);
        }

        public void InteractAtCockpit()
        {
            if (!isActiveAndEnabled) return;
            var client = network?.Client; var frame = client?.Replica.Current;
            if (client?.Ready != true || frame == null || frame.Paused ||
                frame.HostOnly && client.PlayerSlot != 0 || !JourneyPresentationRules.AtCockpit(frame.World, client.PlayerSlot)) return;
            var journey = frame.World.Expedition.Journey;
            if (journey?.Enabled != true) return;
            if (journey.Phase == JourneyPhase.Preparing) Submit("cancel-flight");
            else if (journey.Phase is JourneyPhase.Orbit or JourneyPhase.Descent or JourneyPhase.Landed) Submit("pilot");
        }

        private void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugPanel != null) RuntimeDebugHub.RegisterPanel(debugPanel);
#endif
        }
        private void OnDisable()
        {
            picker?.Close();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugPanel?.Dispose();
#endif
        }
        private async void Submit(string operation)
        {
            try
            {
                var client = network.Client; var frame = client.Replica.Current;
                if (!client.Ready || frame?.World.Expedition == null) return;
                var journey = frame.World.Expedition.Journey;
                if (journey?.Enabled == true && operation == "pilot" && journey.Phase == JourneyPhase.Orbit)
                { picker.Open(); return; }
                if (journey?.Enabled == true && operation == "cancel-flight" && journey.Phase == JourneyPhase.Preparing)
                { SendJourney(true, journey.PlanetId); return; }
                if (operation == "emergency" && Time.unscaledTime > emergencyConfirmation)
                {
                    emergencyConfirmation = Time.unscaledTime + 4;
                    YYLogger.LogWarning("再次点击紧急起飞确认损失；4 秒后取消确认。", LoggingChannel.Gameplay);
                    return;
                }
                emergencyConfirmation = -1;
                bool personal = operation is "unload" or "board" or "mine" or "pilot" or "takeoff" or "land" or "cancel-flight" or "deploy";
                var actor = frame.World.Actors.FirstOrDefault(a => a.ControllerSlot == client.PlayerSlot);
                int target = 0;
                var minerals = network.Terrain?.Minerals;
                if (operation == "mine" && actor != null && minerals?.DataReady == true)
                {
                    double nearest = double.MaxValue;
                    var region = minerals.Region;
                    for (int v = region.MinV; v < region.MaxVExclusive; v++) for (int u = region.MinU; u < region.MaxUExclusive; u++)
                    {
                        var cell = new AnyRules.Next.CellCoord(u, v);
                        if (!minerals.Replica.Read(cell).TryGetCell(out var ore) || ore.IsEmpty ||
                            !network.Terrain.Replica.Read(cell).TryGetCell(out var wall) || !wall.IsEmpty) continue;
                        double distance = Math.Abs((u + .5f) * 16 - actor.X) + Math.Abs(632 + (v - .5f) * 16 - actor.Height);
                        if (distance < nearest) { nearest = distance; target = DarkNights.Core.Config.Terrain.MineralTaskTarget.Encode(u, v); }
                    }
                }
                await client.Send(SessionOperation.Expedition, personal && actor != null ? new[] { actor.Id } : null,
                    target: target, kind: operation, controlLease: personal ? actor?.ControlLease ?? 0 : 0);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void SelectDestination(string planetId) => SendJourney(false, planetId);

        private async void SendJourney(bool cancel, string planetId)
        {
            if (awaitingJourney) return;
            try
            {
                var client = network.Client; var frame = client.Replica.Current;
                var journey = frame?.World.Expedition?.Journey;
                var actor = frame?.World.Actors.FirstOrDefault(a => a.ControllerSlot == client.PlayerSlot && a.Hp > 0);
                if (!client.Ready || journey?.Enabled != true || actor == null) return;
                awaitingJourney = true; journeySequence = 0; earlyFeedback = null;
                requestTime = Time.unscaledTime;
                journeySequence = await client.Send(cancel ? SessionOperation.CancelJourney : SessionOperation.SelectDestination,
                    new[] { actor.Id }, target: frame.World.Expedition.Ship.Id, kind: cancel ? "" : planetId,
                    value: journey.Revision, controlLease: actor.ControlLease);
                if (earlyFeedback?.Sequence == journeySequence) CompleteFeedback(earlyFeedback);
            }
            catch (Exception error)
            {
                awaitingJourney = false;
                Debug.LogException(error);
            }
        }

        private void OnFeedback(CommandFeedback feedback)
        {
            if (!awaitingJourney || feedback.ReadyReply) return;
            if (journeySequence == 0) earlyFeedback = feedback;
            else if (feedback.Sequence == journeySequence) CompleteFeedback(feedback);
        }

        private void CompleteFeedback(CommandFeedback feedback)
        {
            awaitingJourney = false;
            string journeyFeedback = feedback.AffectedCount > 0 ? "航程操作已确认。" : "航程操作未接受：" + feedback.Code;
            if (feedback.AffectedCount > 0) YYLogger.LogInfo(journeyFeedback, LoggingChannel.Gameplay);
            else YYLogger.LogWarning(journeyFeedback, LoggingChannel.Gameplay);
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugPanel?.Dispose();
#endif
            if (network != null) network.Client.Feedback -= OnFeedback;
            if (picker != null) Destroy(picker);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>RuntimeDebugHub 的远征页；复用原生面板的冻结展示和按钮许可，激活时租用本地输入，退出或卸载时释放。</summary>
        private sealed class ExpeditionDebugPanel : IRuntimeDebugPanel, IRuntimeDebugPanelSizeProvider,
            IRuntimeDebugPanelLifecycle, IDisposable
        {
            private readonly ExpeditionPanel source;
            private readonly Action<string> submit;
            private YYInteractionSessionHandle modal;
            private Vector2 scroll;
            public string Title => "远征";
            public int SortOrder => 200;

            internal ExpeditionDebugPanel(ExpeditionPanel source, Action<string> submit)
            { this.source = source; this.submit = submit; }

            public Vector2 GetPreferredSize() => new Vector2(480, 440);

            public void Draw(RuntimeDebugPanelContext context)
            {
                if (source == null || !source.DebugAvailable)
                { GUILayout.Label("进入远征会话后可查看状态与调试操作。", context.LabelStyle); return; }
                if (modal?.Session?.IsActive != true) OnRuntimeDebugPanelActivated();
                bool enabled = GUI.enabled;
                scroll = GUILayout.BeginScrollView(scroll);
                try
                {
                    GUILayout.Label(source.Status.text, context.LabelStyle);
                    GUILayout.Space(12);
                    for (int i = 0; i < source.Actions.Length; i++)
                    {
                        var action = source.Actions[i];
                        if (!action.gameObject.activeSelf) continue;
                        GUI.enabled = enabled && modal?.Session?.IsActive == true && action.interactable;
                        if (GUILayout.Button(source.ActionLabel(i), context.ButtonStyle, GUILayout.Height(28)))
                        {
                            string command = source.Commands[i];
                            // 导航页使用自己的模态，先关闭 Hub，避免两层窗口互相遮挡或占用输入。
                            if (command == "pilot") RuntimeDebugHub.Toggle();
                            submit(command);
                        }
                    }
                }
                finally { GUI.enabled = enabled; GUILayout.EndScrollView(); }
            }

            public void OnRuntimeDebugPanelActivated()
            {
                if (modal?.Session?.IsActive == true) return;
                OnRuntimeDebugPanelDeactivated();
                var sessions = YYInteractionSessionService.Instance;
                if (sessions != null) sessions.TryBegin(new YYInteractionSessionDescriptor
                {
                    Kind = "dark_nights.expedition_debug", Owner = nameof(ExpeditionDebugPanel), Priority = 60,
                    Blocks = YYInteractionBlockFlags.All
                }, out modal);
            }

            public void OnRuntimeDebugPanelDeactivated() { modal?.Dispose(); modal = null; }
            public void Dispose()
            {
                RuntimeDebugHub.UnregisterPanel(this);
                OnRuntimeDebugPanelDeactivated();
            }
        }
#endif
    }
}
