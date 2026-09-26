using System;
using System.Linq;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using DarkNights.View;
using DarkNights.View.Expedition;
using GameCore.Interactions;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>将原生远征面板接到现有可信命令链；房主与来宾共用 Send，客户端不预扣任何资源。</summary>
    public sealed class ExpeditionHud : MonoBehaviour
    {
        private SessionNetwork network;
        private ExpeditionPanel panel;
        private DestinationPicker picker;
        private bool awaitingJourney;
        private long journeySequence;
        private CommandFeedback earlyFeedback;
        private string journeyFeedback = "";
        private float requestTime, feedbackUntil;
        private float emergencyConfirmation = -1;
        public void Initialize(SessionNetwork network, ExpeditionPanel panel)
        {
            this.network = network; this.panel = panel; panel.Bind(Submit);
            picker = gameObject.AddComponent<DestinationPicker>();
            picker.Initialize(panel, YYInteractionSessionService.Instance, SelectDestination);
            network.Client.Feedback += OnFeedback;
        }
        private void Update()
        {
            var client = network?.Client; var frame = client?.Replica.Current;
            if (awaitingJourney && Time.unscaledTime - requestTime > 12)
            { awaitingJourney = false; journeyFeedback = "暂未收到确认，请检查航程状态后重试。"; feedbackUntil = Time.unscaledTime + 8; }
            if (Time.unscaledTime > feedbackUntil) journeyFeedback = "";
            panel?.Present(frame?.World, client?.PlayerSlot ?? -1, client?.Ready == true, frame?.HostOnly == true, frame?.Paused == true);
            picker?.Present(frame, client?.PlayerSlot ?? -1, client?.Ready == true, awaitingJourney, journeyFeedback);
            if (panel != null && journeyFeedback.Length != 0) panel.Status.text += "\n" + journeyFeedback;
            if (panel != null && Time.unscaledTime <= emergencyConfirmation)
                panel.Status.text += "\n再次点击紧急起飞确认以下损失；4 秒后取消确认。";
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
                requestTime = Time.unscaledTime; journeyFeedback = "";
                journeySequence = await client.Send(cancel ? SessionOperation.CancelJourney : SessionOperation.SelectDestination,
                    new[] { actor.Id }, target: frame.World.Expedition.Ship.Id, kind: cancel ? "" : planetId,
                    value: journey.Revision, controlLease: actor.ControlLease);
                if (earlyFeedback?.Sequence == journeySequence) CompleteFeedback(earlyFeedback);
            }
            catch (Exception error)
            {
                awaitingJourney = false; journeyFeedback = error.Message; feedbackUntil = Time.unscaledTime + 8;
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
            journeyFeedback = feedback.AffectedCount > 0 ? "航程操作已确认。" : "航程操作未接受：" + feedback.Code;
            feedbackUntil = Time.unscaledTime + 8;
        }

        private void OnDestroy()
        {
            if (network != null) network.Client.Feedback -= OnFeedback;
            if (picker != null) Destroy(picker);
        }
    }
}
