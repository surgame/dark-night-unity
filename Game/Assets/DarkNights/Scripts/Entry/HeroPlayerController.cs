using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.View;
using GameCore.Interactions;
using GameCore.PlayerInputs;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 单个本地玩家的角色上下文、控制请求和输入发送装配；角色及背包以服务端冻结投影为准。
    /// View 采样器负责逐帧短按和节流，本类不预测位置或结算道具。
    /// </summary>
    public sealed class HeroPlayerController : MonoBehaviour
    {
        private HeroInputSampler sampler;
        private SessionNetwork network;
        private CampInput camp;
        private GameInputActions input;
        private PinewatchStage stage;
        private SessionEntityViews entities;
        private HeroHudBehaviour hud;
        private YYInputRebindingHandle rebind;
        private YYInteractionSessionHandle rebindModal;
        private bool preferHero = true, campControlEnabled, replayOnly, attempted;
        private int epoch, actorId, lease, pendingItem = -1;
        private long connection, selectionRequest, claimRequest;
        private double nextToggle;
        private string notice = "", jumpLabel;
        public ActorViewData Current { get; private set; }
        public string JumpBindingLabel => jumpLabel;

        /// <summary>设置本地模式偏好；连接前仅记录，连接后通过同一权威入口接管或释放角色。</summary>
        public async UniTask SetHeroMode(bool value)
        {
            preferHero = value; attempted = false;
            network.Client.RequestDefaultHero = value;
            if (!network.Client.Ready) return;
            if (value && Current == null) { attempted = true; await Claim(); }
            else if (!value && Current != null)
            {
                attempted = true;
                StopInput();
                await Release();
            }
        }

        public void Initialize(SessionNetwork session, CampInput campInput, GameInputActions actions,
            PinewatchStage scene, HeroHudBehaviour panel, SessionEntityViews visuals)
        {
            network = session; camp = campInput; input = actions; stage = scene; hud = panel;
            entities = visuals;
            sampler = new HeroInputSampler(actions, scene.SceneCamera);
            campControlEnabled = System.Environment.GetCommandLineArgs().Contains("--dn-camp-mode");
            replayOnly = System.Environment.GetCommandLineArgs().Contains("--dn-role") &&
                System.Environment.GetCommandLineArgs().Contains("--dn-input-replay");
            preferHero = !campControlEnabled;
            network.Client.RequestDefaultHero = preferHero;
            input.Unavailable += StopInput;
            network.Client.Feedback += Feedback;
            jumpLabel = YYInputRebindingService.GetBindingDisplayString(input.Jump, 0);
        }

        public void Present(SessionViewData frame, bool menu)
        {
            bool ready = network.Client.Ready && frame != null;
            if (connection != network.Client.ConnectionGeneration || epoch != (frame?.Epoch ?? 0))
            {
                StopInput(); attempted = false; actorId = lease = 0; pendingItem = -1;
                connection = network.Client.ConnectionGeneration; epoch = frame?.Epoch ?? 0;
            }
            Current = null;
            if (ready)
                foreach (var actor in frame.World.Actors)
                    if (actor.ControllerSlot == network.Client.PlayerSlot) { Current = actor; break; }
            if ((Current?.Id ?? 0) != actorId || (Current?.ControlLease ?? 0) != lease)
            {
                actorId = Current?.Id ?? 0; lease = Current?.ControlLease ?? 0;
                pendingItem = -1; sampler.ResetControl();
                if (Current != null) camp.SelectEntity(Current.Id);
            }
            bool controlling = preferHero && (Current != null || !campControlEnabled);
            if (input.HeroMode != controlling)
            { camp.ResetLocal(); input.SetHero(controlling); if (Current != null) camp.SelectEntity(Current.Id); }
            if (Current != null && pendingItem == Current.SelectedItem) pendingItem = -1;
            if (ready && !attempted && !menu && Time.unscaledTimeAsDouble >= nextToggle)
            {
                if (!preferHero && Current != null)
                {
                    attempted = true;
                    Release().Forget();
                }
                else if (campControlEnabled && preferHero && Current == null && (!frame.HostOnly || network.Client.PlayerSlot == 0))
                {
                    attempted = true;
                    Claim().Forget();
                }
            }
            bool aboard = frame?.World.Expedition?.Crew.Any(a => a.Id == Current?.Id && a.Boarded) == true;
            hud.Present(aboard ? null : Current, ready && !menu && !frame.Paused, jumpLabel, notice, campControlEnabled && !aboard);
        }

        private void Update()
        {
            if (replayOnly || input == null || network.Client.Replica.Current == null || !network.Client.Ready) return;
            SampleModeToggle();
            if (!input.HeroMode || Current == null) return;
            if (sampler.Sample(Current, network.Client.Replica.Current, entities, pendingItem >= 0,
                Time.unscaledTimeAsDouble, out HeroInputSampler.Packet packet)) Send(packet).Forget();
            if (sampler.SelectedItem >= 0) SelectItem(sampler.SelectedItem).Forget();
            if (sampler.UseItemRequested) Use().Forget();
        }

        private void SampleModeToggle()
        {
            if (!campControlEnabled) return;
            var toggle = input.HeroMode ? input.HeroToggle : input.CampToggle;
            if (input.CanRead(toggle) && toggle.WasPressedThisFrame()) HandleAction("HeroToggle").Forget();
        }

        private void LateUpdate()
        {
            if (!CanFollowCamera()) return;
            // 等待所有 Update 完成，跟随本帧插值后的显示位置，避免与低频快照产生相对抖动。
            stage.FollowControlledActor(Current.Id, network.Client.Replica.Current, entities);
        }

        private bool CanFollowCamera() =>
            input != null && input.HeroMode && Current != null && network.Client.Ready &&
                network.Client.Replica.Current?.Epoch == epoch &&
                network.Client.ConnectionGeneration == connection &&
                !YYInteractionSessionService.Instance.IsBlocked(YYInteractionBlockFlags.CameraInput);

        public async UniTask<bool> HandleAction(string action)
        {
            if (!action.StartsWith("Hero", StringComparison.Ordinal)) return false;
            if (rebind != null || !network.Client.Ready) return true;
            if (action == "HeroToggle")
            {
                if (!campControlEnabled) return true;
                if (Time.unscaledTimeAsDouble < nextToggle) return true;
                nextToggle = Time.unscaledTimeAsDouble + 0.5;
                await SetHeroMode(Current == null);
            }
            else if (action == "HeroRebind") BeginRebind();
            else if (action == "HeroItem0") await SelectItem(0);
            else if (action == "HeroItem1") await SelectItem(1);
            else if (action == "HeroItem2") await SelectItem(2);
            else if (action == "HeroItem3") await SelectItem(3);
            return true;
        }

        private async UniTask Claim()
        {
            var frame = network.Client.Replica.Current;
            if (frame == null) return;
            var actors = frame.World.Actors;
            ActorViewData target = actors.FirstOrDefault(a => !a.Enemy && a.ControllerSlot < 0 &&
                !a.Activity.StartsWith("Training", StringComparison.Ordinal) && camp.Selected.Contains(a.Id)) ??
                actors.FirstOrDefault(a => !a.Enemy && a.ControllerSlot < 0 && !a.Activity.StartsWith("Training", StringComparison.Ordinal));
            if (target == null) { notice = "没有可接管的居民。"; return; }
            notice = "";
            try { claimRequest = await network.Client.Send(SessionOperation.ClaimHero, new[] { target.Id }); }
            catch (Exception error) { notice = error.Message; }
        }

        private async UniTask Release()
        {
            ActorViewData current = Current;
            if (current == null) return;
            try { await network.Client.Send(SessionOperation.ReleaseHero, new[] { current.Id }, controlLease: current.ControlLease); }
            catch (Exception error) { notice = error.Message; }
        }

        private async UniTask SelectItem(int index)
        {
            if (Current == null || index == Current.SelectedItem || pendingItem == index) return;
            StopInput(); pendingItem = index;
            try { selectionRequest = await network.Client.Send(SessionOperation.SelectHeroItem, new[] { Current.Id }, value: index, controlLease: Current.ControlLease); }
            catch (Exception error) { pendingItem = -1; notice = error.Message; }
        }

        private async UniTask Use()
        {
            if (Current.SelectedItem != 3) return;
            try
            {
                await network.Client.Send(SessionOperation.UseHeroItem, new[] { Current.Id }, target: 0,
                    kind: HeroInventoryBehaviour.ItemKey(Current.SelectedItem), value: Current.SelectionRevision, controlLease: Current.ControlLease);
            }
            catch (Exception error) { notice = error.Message; }
        }

        private async UniTask Send(HeroInputSampler.Packet packet)
        {
            try
            {
                await network.Client.SendInput(actorId, lease, packet.Direction, packet.JumpHeld, packet.UseHeld,
                    packet.JumpPressed, packet.DropPressed, packet.Aim, packet.SelectionRevision,
                    packet.UsePressed, packet.UseReleased, packet.CancelUse);
            }
            catch (Exception error) { notice = error.Message; }
        }
        private void StopInput()
        {
            HeroInputSampler.Packet packet = sampler?.Stop(Current?.SelectionRevision ?? 0) ?? default;
            if (actorId > 0) Send(packet).Forget();
        }
        private void BeginRebind()
        {
            StopInput(); input.RebindingActive = true;
            rebindModal = YYInteractionSessionService.Instance.Begin(new YYInteractionSessionDescriptor
            { Kind = "dark_nights.rebind", Owner = nameof(HeroPlayerController), Priority = 200, Blocks = YYInteractionBlockFlags.All });
            notice = "请按新的跳跃键，Esc 取消。";
            try { rebind = YYInputRebindingService.StartManagedInteractiveRebind(input.Jump, 0, () => false, () => EndRebind(true), () => EndRebind(false)); }
            catch { EndRebind(false); throw; }
        }
        private void EndRebind(bool save)
        {
            rebind = null; rebindModal?.Dispose(); rebindModal = null; input.RebindingActive = false;
            input.SuppressMenu();
            notice = save && !YYInputSettingsStore.SaveBindingOverrides(input.Asset) ? "改键已生效，但保存失败。" : "";
            jumpLabel = YYInputRebindingService.GetBindingDisplayString(input.Jump, 0);
        }
        private void OnDisable()
        {
            StopInput(); rebind?.Dispose(); rebindModal?.Dispose();
            if (input != null) input.RebindingActive = false;
        }
        private void OnDestroy()
        {
            if (network != null) network.Client.Feedback -= Feedback;
            if (input != null) input.Unavailable -= StopInput;
        }

        private void Feedback(CommandFeedback result)
        {
            if (result.ReadyReply || result.Code == "Applied") return;
            if (result.Sequence == selectionRequest) pendingItem = -1;
            if (result.Sequence == claimRequest && result.Code == "NoEffect")
            { attempted = false; nextToggle = Time.unscaledTimeAsDouble + 0.5; }
        }
    }
}
