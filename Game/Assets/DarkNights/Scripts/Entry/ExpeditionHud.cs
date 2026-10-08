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
        private string lastJourneyError = "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IDisposable debugRegistration;
#endif
        public void Initialize(SessionNetwork network, ExpeditionPanel panel)
        {
            this.network = network; this.panel = panel; panel.Bind(Submit);
            picker = gameObject.AddComponent<DestinationPicker>();
            picker.Initialize(panel, YYInteractionSessionService.Instance, SelectDestination);
            network.Client.Feedback += OnFeedback;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RegisterDebugPanel();
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
            if (panel != null) RegisterDebugPanel();
#endif
        }
        private void OnDisable()
        {
            picker?.Close();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugRegistration?.Dispose(); debugRegistration = null;
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
                if (operation is not ("pilot" or "takeoff" or "land" or "cancel-flight")) return;
                var actor = frame.World.Actors.FirstOrDefault(a => a.ControllerSlot == client.PlayerSlot);
                if (actor == null) return;
                await client.Send(SessionOperation.Expedition, new[] { actor.Id }, kind: operation, controlLease: actor.ControlLease);
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
            debugRegistration?.Dispose(); debugRegistration = null;
#endif
            if (network != null) network.Client.Feedback -= OnFeedback;
            if (picker != null) Destroy(picker);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void RegisterDebugPanel()
        {
            if (debugRegistration == null) debugRegistration = RuntimeDebugHub.RegisterPanel(
                new RuntimeDebugPanelDescriptor("dark_nights.ship", "飞船", 200),
                () => new ExpeditionDebugPanel(panel, Submit));
        }
#endif
    }
}
