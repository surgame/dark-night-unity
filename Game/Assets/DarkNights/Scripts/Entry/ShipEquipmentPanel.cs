using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using GameCore.UI;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 远征的原生UITK面板；常驻区域仅显示四格装备，背包、商店和导航使用既有模态生命周期。
    /// 金额、货物和装备只读冻结投影，按钮只发出意图，不改变库存、船货或结算。
    /// </summary>
    [UIAsset("dark_nights.ui.ship_equipment")]
    public sealed class ShipEquipmentPanel : UIPanel
    {
        private VisualElement shop, inventory, equipment;
        private Button pistol, pickaxe, shopClose, inventoryClose;
        private ShipHudReadout readout;
        internal ShipNavigationContent Navigation { get; private set; }
        public event Action<string> BuyRequested;
        public event Action Closed;
        public bool ShopOpen { get; private set; }
        public bool InventoryOpen { get; private set; }
        public bool AttachedAndVisible => Root?.panel != null && Root.resolvedStyle.display != DisplayStyle.None &&
            Root.worldBound.width > 0 && Root.worldBound.height > 0;

        protected override void OnBind()
        {
            Root.AddToClassList("ship-equipment-panel");
            Root.pickingMode = PickingMode.Ignore;
            Root.Q("ship-ui-root").pickingMode = PickingMode.Ignore;
            equipment = Root.Q("equipment-root");
            readout = new ShipHudReadout(Root);
            shop = Root.Q("shop"); inventory = Root.Q("inventory");
            pistol = Root.Q<Button>("buy-pistol"); pickaxe = Root.Q<Button>("buy-pickaxe");
            shopClose = Root.Q<Button>("close"); inventoryClose = Root.Q<Button>("close-inventory");
            pistol.clicked += BuyPistol; pickaxe.clicked += BuyPickaxe;
            shopClose.clicked += Close; inventoryClose.clicked += Close;
            Navigation = new ShipNavigationContent(Root);
            SetShop(false); SetInventory(false);
        }

        public void Present(ActorViewData actor, int balance, int iron, int gold,
            ShipTradeDefinition prices, bool active)
        {
            Root.EnableInClassList("is-hidden", !active);
            equipment.EnableInClassList("is-hidden", actor == null || ShopOpen || InventoryOpen || Navigation.IsOpen);
            readout.Present(actor, balance, iron, gold);
            pistol.text = "手枪 · " + prices.PistolPrice;
            pickaxe.text = "矿镐 · " + prices.PickaxePrice;
            pistol.SetEnabled(balance >= prices.PistolPrice && !Holds(actor, 1) && Holds(actor, 0));
            pickaxe.SetEnabled(balance >= prices.PickaxePrice && !Holds(actor, 2) && Holds(actor, 0));
        }

        internal void PresentContext(string prompt, string key, bool permitted, bool blocked,
            JourneyViewData journey, ExpeditionViewData expedition) =>
            readout.PresentContext(prompt, key, permitted, blocked, journey, expedition);
        internal void PresentCargo(int iron, int gold, long income) => readout.PresentCargo(iron, gold, income);
        internal void ResetReadout() => readout.Reset();
        internal void PlaceContext(UnityEngine.Vector2 screen) => readout.PlaceContext(screen);
        internal void ShowReceipt(string value) => readout.ShowReceipt(value);

        public void SetShop(bool visible)
        {
            ShopOpen = visible;
            shop.EnableInClassList("is-open", visible);
            if (visible) shopClose.Focus();
        }

        public void SetInventory(bool visible)
        {
            InventoryOpen = visible;
            inventory.EnableInClassList("is-open", visible);
            if (visible) inventoryClose.Focus();
        }

        private static bool Holds(ActorViewData actor, int item) => actor != null &&
            (actor.Slot0 == item || actor.Slot1 == item || actor.Slot2 == item || actor.Slot3 == item);
        private void BuyPistol() => BuyRequested?.Invoke("pistol");
        private void BuyPickaxe() => BuyRequested?.Invoke("pickaxe");
        private void Close() { SetShop(false); SetInventory(false); Closed?.Invoke(); }

        protected override void OnDispose()
        {
            Navigation?.Dispose(); Navigation = null;
            pistol.clicked -= BuyPistol; pickaxe.clicked -= BuyPickaxe;
            shopClose.clicked -= Close; inventoryClose.clicked -= Close;
            BuyRequested = null; Closed = null;
        }
    }
}
