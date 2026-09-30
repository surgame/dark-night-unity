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
        private bool nearSale, nearShop;
        private int epoch, actorId, controlLease;
        private long connection;
        public bool ShopOpen => panel?.ShopOpen == true;
        public Action CockpitRequested { get; set; }

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
            panel.Present(null, 0, 0, 0, rules.Balance.HeroControl.FuelSeconds,
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
            nearSale = nearShop = false;
            if (gameplay && actor != null && ship != null && ship.PilotId == 0 &&
                ship.Phase is 0 or 3 && ship.DoorClock == 0 &&
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
                        if (config.Service == "sale") nearSale = nearby;
                        if (config.Service == "shop") nearShop = nearby;
                    }
            }
            var cargo = expedition?.Crew.FirstOrDefault(value => value.Id == actor?.Id);
            string prompt = nearShop ? "E 打开装备商店" : nearSale ? "E 出售身上矿石" : "Shift 加速 · 1–4 切换装备";
            if (!nearShop && !nearSale && gameplay && hero.MiningHint.Length != 0) prompt = hero.MiningHint;
            bool atCockpit = !nearShop && !nearSale && gameplay && expedition?.Journey?.Enabled == true &&
                JourneyPresentationRules.AtCockpit(frame.World, network.Client.PlayerSlot);
            if (atCockpit) prompt = expedition.Journey.Phase == JourneyPhase.Orbit ? "E 选择目的地" :
                expedition.Journey.Phase == JourneyPhase.Preparing ? "E 取消航程" :
                expedition.Journey.Phase is JourneyPhase.Descent or JourneyPhase.Landed ?
                    (ship.PilotId == actor.Id ? "E 离开驾驶位" : ship.PilotId == 0 ? "E 接管驾驶" : "驾驶位已占用") :
                    "航行中，请等待到达";
            panel.Present(actor, frame?.World.Camp.Credits ?? 0, cargo?.Iron ?? 0, cargo?.Gold ?? 0,
                catalog.Balance.HeroControl.FuelSeconds, catalog.Balance.Expedition.Trade, prompt,
                gameplay && actor != null);
            if (panel.ShopOpen && (!gameplay || !nearShop || modal?.Session?.IsActive != true ||
                !panel.AttachedAndVisible)) Close();
            if (!gameplay || actor == null || panel.ShopOpen || !actions.ReadHero().InteractPressed) return;
            if (nearShop) Open();
            else if (nearSale && cargo != null && cargo.Iron + cargo.Gold > 0)
                Sell(actor, cargo).Forget();
            else if (atCockpit) CockpitRequested?.Invoke();
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

        public void Close() { panel?.SetShop(false); ReleaseModal(); }
        private void ReleaseModal() { modal?.Dispose(); modal = null; }
        private void Buy(string key)
        {
            ActorViewData actor = hero.Current;
            if (!nearShop || actor == null || !panel.ShopOpen) return;
            Purchase(actor, key).Forget();
        }

        private async UniTask Sell(ActorViewData actor, ExpeditionActorData cargo)
        {
            try { await network.Client.Send(SessionOperation.SellCarriedOre, new[] { actor.Id }, shipId,
                cargo.Gold, "sale", cargo.Iron, actor.ControlLease); }
            catch (Exception error) { Debug.LogException(error); }
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
            CockpitRequested = null;
            ReleaseModal();
            if (panel != null) { panel.BuyRequested -= Buy; panel.Closed -= ReleaseModal; }
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
