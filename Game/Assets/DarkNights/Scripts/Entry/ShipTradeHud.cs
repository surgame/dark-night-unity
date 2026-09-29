using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.View;
using GameCore.Interactions;
using GameCore.Objects.Runner.DI;
using GameCore.UI;
using UnityEngine;
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
        private YYInteractionSessionHandle modal;
        private GameCatalog catalog;
        private int shipId;
        private bool nearSale, nearShop;

        public async UniTask Initialize(SessionNetwork session, GameInputActions input, HeroPlayerController player,
            SessionEntityViews views, GameCatalog rules)
        {
            network = session; actions = input; hero = player; entities = views; catalog = rules;
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = settings;
            manager.Initialize(document.rootVisualElement, DIContainer.Root);
            panel = await manager.OpenAsync<ShipEquipmentPanel>();
            panel.BuyRequested += Buy;
            panel.Closed += ReleaseModal;
            panel.Present(null, 0, 0, 0, rules.Balance.HeroControl.FuelSeconds,
                rules.Balance.Expedition.Trade, "", false);
        }

        public void Present(SessionViewData frame, ActorViewData actor, bool gameplay)
        {
            if (panel == null) return;
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
            panel.Present(actor, frame?.World.Camp.Credits ?? 0, cargo?.Iron ?? 0, cargo?.Gold ?? 0,
                catalog.Balance.HeroControl.FuelSeconds, catalog.Balance.Expedition.Trade, prompt,
                gameplay && actor != null);
            if ((!gameplay || !nearShop) && panel.ShopOpen) Close();
            if (!gameplay || actor == null || panel.ShopOpen || !actions.ReadHero().InteractPressed) return;
            if (nearShop) Open();
            else if (nearSale && cargo != null && cargo.Iron + cargo.Gold > 0)
                Sell(actor, cargo).Forget();
        }

        private void Open()
        {
            hero.CancelWorldInput();
            panel.SetShop(true);
            modal = YYInteractionSessionService.Instance.Begin(new YYInteractionSessionDescriptor
            {
                Kind = "dark_nights.ship_shop", Owner = nameof(ShipTradeHud), Priority = 100,
                Blocks = YYInteractionBlockFlags.GameplayActions | YYInteractionBlockFlags.WorldConfirm,
                ConflictPolicy = YYInteractionConflictPolicy.CancelLowerPriority
            });
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
        private void OnDestroy()
        {
            ReleaseModal();
            if (panel != null) { panel.BuyRequested -= Buy; panel.Closed -= ReleaseModal; }
            manager.Dispose();
            if (settings != null) Destroy(settings);
        }
    }
}
