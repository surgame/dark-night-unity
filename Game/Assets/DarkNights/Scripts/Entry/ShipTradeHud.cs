using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.View;
using DarkNights.View.Expedition;
using GameCore.Interactions;
using GameCore.Objects.Runner.DI;
using GameCore.UI;
using Runtime.Utils;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>本地飞船服务提示与 YYGC UIManager 装配；服务端再次核验距离、货袋和购买版本。</summary>
    public sealed class ShipTradeHud : MonoBehaviour
    {
        private readonly UIManager manager = new UIManager();
        private SessionNetwork network;
        private GameInputActions actions;
        private HeroPlayerController hero;
        private SessionEntityViews entities;
        private ShipEquipmentPanel panel;
        private UIDocument document;
        private PanelSettings settings;
        private FontAsset font;
        private YYInteractionSessionHandle modal;
        private GameCatalog catalog;
        private int shipId;
        private bool nearShop, nearCargo;
        private int epoch, actorId, controlLease;
        private long connection;
        public bool ShopOpen => panel?.ShopOpen == true || panel?.Navigation?.IsOpen == true;

        public async UniTask Initialize(SessionNetwork session, GameInputActions input, HeroPlayerController player,
            SessionEntityViews views, GameCatalog rules)
        {
            network = session; actions = input; hero = player; entities = views; catalog = rules;
            var source = await AssetProvider.LoadAssetAsync<PanelSettings>("dark_nights.ui.ship_equipment_settings");
            if (source == null || source.themeStyleSheet == null)
                throw new InvalidOperationException("飞船装备 UI 缺少 PanelSettings 或运行时主题。");
            if (this == null) return;
            settings = Instantiate(source);
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            manager.Initialize(document.rootVisualElement, DIContainer.Root);
            panel = await manager.OpenAsync<ShipEquipmentPanel>();
            font = FontAsset.CreateFontAsset("Microsoft YaHei", "Regular", 48);
            if (font == null) throw new InvalidOperationException("无法加载飞船界面的中文字体 Microsoft YaHei。");
            panel.Root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
            panel.BuyRequested += Buy;
            panel.Closed += ReleaseModal;
            panel.Navigation.OpenRequested += OpenNavigation;
            panel.Navigation.CloseRequested += Close;
            panel.Navigation.DestinationRequested += Navigate;
            panel.Navigation.OperationRequested += ExpeditionAction;
            panel.DepositRequested += Deposit;
            panel.Present(null, 0, 0, 0,
                rules.Balance.Expedition.Trade, "", false);
        }

        public void Present(SessionViewData frame, ActorViewData actor, bool gameplay)
        {
            if (panel == null) return;
            if (!isActiveAndEnabled) { panel.OnHide(); return; }
            if (epoch != (frame?.Epoch ?? 0) || connection != network.Client.ConnectionGeneration ||
                actorId != (actor?.Id ?? 0) || controlLease != (actor?.ControlLease ?? 0)) Close();
            epoch = frame?.Epoch ?? 0; connection = network.Client.ConnectionGeneration;
            actorId = actor?.Id ?? 0; controlLease = actor?.ControlLease ?? 0;
            var expedition = frame?.World.Expedition;
            var ship = expedition?.Ship;
            shipId = ship?.Id ?? 0;
            nearShop = nearCargo = false;
            if (gameplay && actor != null && ship != null && ship.PilotId == 0 &&
                ship.Phase is 0 or 1 or 3 && ship.DoorClock == 0 &&
                expedition.Crew.Any(value => value.Id == actor.Id && value.Boarded))
            {
                var body = frame.World.Buildings.FirstOrDefault(value => value.Id == shipId);
                var shipDevice = expedition.Devices.FirstOrDefault(value => value.Id == shipId);
                var visual = entities.Visual(shipId);
                if (body != null && shipDevice != null && visual != null)
                    foreach (var anchor in visual.GetComponentsInChildren<ShipServiceAnchor>(true))
                    {
                        if (!anchor.gameObject.activeSelf || anchor.Definition.IsEmpty) continue;
                        var config = anchor.Config;
                        if (config == null) continue;
                        bool nearby = Math.Abs(actor.X - body.X - anchor.transform.localPosition.x * 100) <= config.Radius &&
                            Math.Abs(actor.Height - shipDevice.Height - anchor.transform.localPosition.y * 100) <= config.Radius;
                        if (config.Service == "shop") nearShop = nearby && expedition.Journey.Phase is
                            JourneyPhase.Orbit or JourneyPhase.Transit or JourneyPhase.Landed or JourneyPhase.Gathering;
                        if (config.Service == "sale") nearCargo = nearby && expedition.Journey.Phase == JourneyPhase.Landed;
                    }
            }
            var cargo = expedition?.Crew.FirstOrDefault(value => value.Id == actor?.Id);
            string prompt = nearCargo ? "E 存入货舱 · 安全撤离后自动回收" : nearShop ? "E 打开装备商店" : "Shift 加速 · 空格跳跃";
            bool atCockpit = !nearShop && gameplay && expedition?.Journey?.Enabled == true &&
                JourneyPresentationRules.AtCockpit(frame.World, network.Client.PlayerSlot);
            if (atCockpit) prompt = "E 打开导航台 · 查看星图／召集撤离";
            if (gameplay && !nearShop && !atCockpit && !ShopOpen && actor != null)
            {
                if (!string.IsNullOrEmpty(hero.MiningHint)) prompt = hero.MiningHint;
                else if (actor.Charging) prompt = "蓄力 " + actor.ChargeSeconds.ToString("F1") + "s · 松开左键投掷";
                else prompt = "鼠标瞄准 · 左键使用 · 1–4 切换 · Shift 加速 · " + hero.JumpBindingLabel + " 跳跃";
            }
            panel.Present(actor, frame?.World.Camp.Credits ?? 0, cargo?.Iron ?? 0, cargo?.Gold ?? 0,
                catalog.Balance.Expedition.Trade, prompt,
                gameplay);
            var shipCargo = expedition?.Devices.FirstOrDefault(d => d.Id == shipId);
            panel.PresentCargo(shipCargo?.Iron ?? 0, shipCargo?.Gold ?? 0,
                catalog.Balance.Expedition.Trade.SaleValue(shipCargo?.Iron ?? 0, shipCargo?.Gold ?? 0),
                nearCargo && actor != null && cargo != null && cargo.Iron + cargo.Gold > 0 &&
                (!frame.HostOnly || network.Client.PlayerSlot == 0));
            panel.Navigation.Present(expedition?.Journey, atCockpit &&
                (!frame.HostOnly || network.Client.PlayerSlot == 0), expedition);
            if (panel.Navigation.IsOpen && (!gameplay || modal?.Session?.IsActive != true)) Close();
            if (panel.ShopOpen && (!gameplay || !nearShop || modal?.Session?.IsActive != true ||
                !panel.AttachedAndVisible)) Close();
            if (!gameplay || actor == null || panel.ShopOpen || !actions.ReadHero().InteractPressed) return;
            if (nearCargo) Deposit();
            else if (nearShop) Open();
            else if (atCockpit) OpenNavigation();
        }

        private void Open()
        {
            if (!panel.AttachedAndVisible || ShopOpen) return;
            modal = YYInteractionSessionService.Instance.Begin(new YYInteractionSessionDescriptor
            {
                Kind = "dark_nights.ship_shop", Owner = nameof(ShipTradeHud), Priority = 100,
                Blocks = YYInteractionBlockFlags.GameplayActions | YYInteractionBlockFlags.WorldConfirm,
                ConflictPolicy = YYInteractionConflictPolicy.RejectIfBlocked
            });
            if (!modal.IsValid) { modal = null; return; }
            hero.CancelWorldInput();
            panel.SetShop(true);
        }

        private void OpenNavigation()
        {
            if (panel == null || !panel.AttachedAndVisible || ShopOpen) return;
            modal = YYInteractionSessionService.Instance.Begin(new YYInteractionSessionDescriptor
            {
                Kind = "dark_nights.ship_navigation", Owner = nameof(ShipTradeHud), Priority = 100,
                Blocks = YYInteractionBlockFlags.GameplayActions | YYInteractionBlockFlags.WorldConfirm,
                ConflictPolicy = YYInteractionConflictPolicy.RejectIfBlocked
            });
            if (!modal.IsValid) { modal = null; return; }
            hero.CancelWorldInput(); panel.Navigation.SetOpen(true);
        }

        private async void Navigate(string target)
        {
            var actor = hero.Current; var journey = network.Client.Replica.Current?.World.Expedition?.Journey;
            if (actor == null || journey == null) return;
            try { await network.Client.Send(SessionOperation.SelectDestination, new[] { actor.Id }, shipId,
                kind: target, value: journey.Revision, controlLease: actor.ControlLease); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void Deposit() => ExpeditionAction("unload");
        private async void ExpeditionAction(string operation)
        {
            var actor = hero.Current; var journey = network.Client.Replica.Current?.World.Expedition?.Journey;
            if (actor == null || journey == null) return;
            try
            {
                bool cancelJourney = operation == "cancel-flight" && journey.Phase is JourneyPhase.Preparing or JourneyPhase.Transit or JourneyPhase.Ascent;
                await network.Client.Send(cancelJourney ? SessionOperation.CancelJourney : SessionOperation.Expedition,
                    new[] { actor.Id }, shipId, kind: cancelJourney ? "" : operation,
                    value: journey.Revision, controlLease: actor.ControlLease);
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        public void Close() { panel?.SetShop(false); panel?.Navigation?.SetOpen(false); ReleaseModal(); }
        private void ReleaseModal() { modal?.Dispose(); modal = null; }
        private void Buy(string key)
        {
            ActorViewData actor = hero.Current;
            if (!nearShop || actor == null || !panel.ShopOpen) return;
            Purchase(actor, key).Forget();
        }

        private async UniTask Purchase(ActorViewData actor, string key)
        {
            try { await network.Client.Send(SessionOperation.BuyEquipment, new[] { actor.Id }, shipId, 0,
                key, actor.InventoryRevision, actor.ControlLease); }
            catch (Exception error) { Debug.LogException(error); }
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            ReleaseModal();
            if (panel != null)
            {
                panel.BuyRequested -= Buy; panel.Closed -= ReleaseModal;
                panel.Navigation.OpenRequested -= OpenNavigation; panel.Navigation.CloseRequested -= Close;
                panel.Navigation.DestinationRequested -= Navigate;
                panel.Navigation.OperationRequested -= ExpeditionAction; panel.DepositRequested -= Deposit;
            }
            if (document != null) manager.Dispose();
            if (settings != null) Destroy(settings);
            if (font != null)
            {
                foreach (var texture in font.atlasTextures) if (texture != null) Destroy(texture);
                if (font.material != null) Destroy(font.material);
                Destroy(font);
            }
        }
    }
}
