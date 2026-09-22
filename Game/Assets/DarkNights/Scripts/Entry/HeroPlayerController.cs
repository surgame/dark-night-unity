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
    /// 单个本地玩家的输入采样和控制请求适配；角色及背包以服务端冻结投影为准。
    /// 输入变化最高 30 Hz、无变化 10 Hz 保活，短按跨渲染帧保留至发送；不预测位置或结算道具。
    /// </summary>
    public sealed class HeroPlayerController : MonoBehaviour
    {
        private readonly HeroActionInput equipment = new HeroActionInput();
        private SessionNetwork network;
        private CampInput camp;
        private GameInputActions input;
        private PinewatchStage stage;
        private SessionEntityViews entities;
        private HeroHudBehaviour hud;
        private YYInputRebindingHandle rebind;
        private YYInteractionSessionHandle rebindModal;
        private bool preferHero = true, campControlEnabled, attempted, jumpPending, dropPending, sentJump, sentUse, sentDrop;
        private int epoch, actorId, lease, sentDirection, pendingItem = -1;
        private long connection, selectionRequest, claimRequest;
        private double nextSend, heartbeat, nextToggle;
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
            campControlEnabled = System.Environment.GetCommandLineArgs().Contains("--dn-camp-mode");
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
                jumpPending = dropPending = sentJump = sentUse = false; sentDirection = 0;
                actorId = Current?.Id ?? 0; lease = Current?.ControlLease ?? 0; nextSend = heartbeat = 0;
                pendingItem = -1; equipment.Cancel();
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
            hud.Present(Current, ready && !menu && !frame.Paused, jumpLabel, notice, campControlEnabled);
        }

        private void Update()
        {
            if (input == null || network.Client.Replica.Current == null || !network.Client.Ready) return;
            if (campControlEnabled)
            {
                var toggle = input.HeroMode ? input.HeroToggle : input.CampToggle;
                if (input.CanRead(toggle) && toggle.WasPressedThisFrame()) HandleAction("HeroToggle").Forget();
            }
            if (!input.HeroMode || Current == null) return;
            bool allowed = input.CanRead(input.Move) && !network.Client.Replica.Current.Paused;
            int direction = allowed ? Math.Sign(input.Move.ReadValue<float>()) : 0;
            bool jump = allowed && input.CanRead(input.Jump) && input.Jump.IsPressed();
            bool pilot = network.Client.Replica.Current.World.Expedition?.Ship?.PilotId == Current.Id;
            bool aboard = network.Client.Replica.Current.World.Expedition?.Crew.Any(a => a.Id == Current.Id && a.Boarded) == true;
            Vector3 hand = entities.Visual(Current.Id)?.transform.position ?? new Vector3(Current.X / 100, Current.Height / 100, 0);
            equipment.Sample(input, stage.SceneCamera, hand + Vector3.up * .09f, allowed && pendingItem < 0 && !aboard);
            bool use = equipment.Held;
            if (allowed)
            {
                jumpPending |= input.CanRead(input.Jump) && input.Jump.WasPressedThisFrame();
                dropPending = pilot ? input.CanRead(input.Drop) && input.Drop.IsPressed() : dropPending || input.CanRead(input.Drop) && input.Drop.WasPressedThisFrame();
            }
            else jumpPending = dropPending = false;
            double now = Time.unscaledTimeAsDouble;
            bool changed = direction != sentDirection || jump != sentJump || use != sentUse || jumpPending || dropPending != sentDrop || (!pilot && dropPending) || equipment.Changed;
            if (now >= nextSend && (changed || now >= heartbeat))
            {
                Send(direction, jump, use, jumpPending, dropPending).Forget();
                equipment.Consume();
                sentDirection = direction; sentJump = jump; sentUse = use; sentDrop = dropPending;
                jumpPending = dropPending = false;
                nextSend = now + 1.0 / 30; heartbeat = now + 0.1;
            }
            if (allowed && !aboard)
            {
                int selected = input.CanRead(input.Item1) && input.Item1.WasPressedThisFrame() ? 0 :
                    input.CanRead(input.Item2) && input.Item2.WasPressedThisFrame() ? 1 :
                    input.CanRead(input.Item3) && input.Item3.WasPressedThisFrame() ? 2 :
                    input.CanRead(input.Item4) && input.Item4.WasPressedThisFrame() ? 3 : -1;
                float scroll = input.PointerOverUi ? 0 : input.Scroll.ReadValue<Vector2>().y;
                if (selected < 0 && scroll != 0) selected = (Current.SelectedItem + (scroll > 0 ? 3 : 1)) % 4;
                if (selected >= 0) SelectItem(selected).Forget();
                if (Current.SelectedItem == 3 && pendingItem < 0 && input.CanRead(input.UseItem) && input.UseItem.WasPressedThisFrame()) Use().Forget();
            }
        }

        private void LateUpdate()
        {
            if (input == null || !input.HeroMode || Current == null || !network.Client.Ready ||
                network.Client.Replica.Current?.Epoch != epoch ||
                network.Client.ConnectionGeneration != connection ||
                YYInteractionSessionService.Instance.IsBlocked(YYInteractionBlockFlags.CameraInput)) return;
            // 等待所有 Update 完成，跟随本帧插值后的显示位置，避免与低频快照产生相对抖动。
            EntityView visual = entities.Visual(Current.Id);
            if (visual != null) stage.FocusHero(visual.transform.position);
        }

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

        private async UniTask Send(int direction, bool jump, bool use, bool pressed = false, bool drop = false)
        {
            try { await network.Client.SendInput(actorId, lease, direction, jump, use, pressed, drop, equipment.Aim, Current?.SelectionRevision ?? 0, equipment.Pressed, equipment.Released, equipment.Cancelled); }
            catch (Exception error) { notice = error.Message; }
        }
        private void StopInput()
        {
            jumpPending = dropPending = false;
            equipment.Cancel();
            if (actorId > 0) Send(0, false, false).Forget();
            sentDirection = 0; sentJump = sentUse = sentDrop = false;
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
