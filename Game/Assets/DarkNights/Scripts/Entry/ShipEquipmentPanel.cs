using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using GameCore.UI;
using R3;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>船内四格装备的 YYGC UIToolkit 面板；只订阅冻结副本并发送有效商品的购买意图，喷气暂退。</summary>
    [UIAsset("dark_nights.ui.ship_equipment")]
    public sealed class ShipEquipmentPanel : UIPanel
    {
        private readonly Subject<string> credits = new Subject<string>();
        private readonly Subject<string> hint = new Subject<string>();
        private readonly Subject<string>[] slots =
            { new Subject<string>(), new Subject<string>(), new Subject<string>(), new Subject<string>() };
        private VisualElement shop;
        private Button pistol, pickaxe, deposit;
        internal ShipNavigationContent Navigation { get; private set; }
        public event Action<string> BuyRequested;
        public event Action Closed;
        public event Action DepositRequested;
        public bool ShopOpen { get; private set; }
        public bool AttachedAndVisible => Root?.panel != null && Root.resolvedStyle.display != DisplayStyle.None &&
            Root.worldBound.width > 0 && Root.worldBound.height > 0;

        protected override void OnBind()
        {
            Root.AddToClassList("ship-equipment-panel");
            Root.pickingMode = PickingMode.Ignore;
            Root.Q("ship-ui-root").pickingMode = PickingMode.Ignore;
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.top = Root.style.right = Root.style.bottom = 0;
            Disposables.Add(credits.BindText(Root.Q<Label>("credits")));
            Disposables.Add(hint.BindText(Root.Q<Label>("hint")));
            for (int i = 0; i < slots.Length; i++)
                Disposables.Add(slots[i].BindText(Root.Q<Label>("slot" + i)));
            shop = Root.Q<VisualElement>("shop");
            pistol = Root.Q<Button>("buy-pistol"); pickaxe = Root.Q<Button>("buy-pickaxe");
            Root.Q<Button>("buy-jetpack").SetEnabled(false);
            pistol.clicked += BuyPistol; pickaxe.clicked += BuyPickaxe;
            Root.Q<Button>("close").clicked += Close;
            SetShop(false);
            Navigation = new ShipNavigationContent(Root);
            deposit = Root.Q<Button>("deposit-cargo"); deposit.clicked += Deposit;
        }

        public void Present(ActorViewData actor, int balance, int iron, int gold,
            ShipTradeDefinition prices, string interaction, bool active)
        {
            Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            Root.Q("equipment-root").style.display = actor == null ? DisplayStyle.None : DisplayStyle.Flex;
            credits.OnNext("信用点 " + balance + "  ·  货袋 铁 " + iron + " / 金 " + gold);
            hint.OnNext(interaction);
            int[] items = { actor?.Slot0 ?? 0, actor?.Slot1 ?? 0, actor?.Slot2 ?? 0, actor?.Slot3 ?? 0 };
            for (int i = 0; i < slots.Length; i++)
            {
                string name = items[i] switch { 1 => "手枪", 2 => "矿镐", 3 => "炸药", 4 => "手电筒", _ => "空" };
                slots[i].OnNext((actor?.SelectedItem == i ? "▶ " : "") + (i + 1) + "  " + name);
            }
            pistol.text = "手枪 · " + prices.PistolPrice;
            pickaxe.text = "矿镐 · " + prices.PickaxePrice;
            pistol.SetEnabled(balance >= prices.PistolPrice && Array.IndexOf(items, 1) < 0 && Array.IndexOf(items, 0) >= 0);
            pickaxe.SetEnabled(balance >= prices.PickaxePrice && Array.IndexOf(items, 2) < 0 && Array.IndexOf(items, 0) >= 0);
        }

        public void SetShop(bool visible)
        {
            ShopOpen = visible;
            if (shop != null) shop.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) Root.Q<Button>("close").Focus();
        }

        internal void PresentCargo(int iron, int gold, long income, bool canDeposit)
        {
            Root.Q<Label>("ship-cargo").text = "船货 铁 " + iron + " / 金 " + gold + " · 预计收入 " + income;
            deposit.SetEnabled(canDeposit);
        }

        private void BuyPistol() => BuyRequested?.Invoke("pistol");
        private void BuyPickaxe() => BuyRequested?.Invoke("pickaxe");
        private void Close() { SetShop(false); Closed?.Invoke(); }
        private void Deposit() => DepositRequested?.Invoke();
        protected override void OnDispose()
        {
            Navigation?.Dispose(); Navigation = null;
            deposit.clicked -= Deposit; DepositRequested = null;
            pistol.clicked -= BuyPistol; pickaxe.clicked -= BuyPickaxe;
            Root.Q<Button>("close").clicked -= Close;
            BuyRequested = null; Closed = null;
            credits.Dispose(); hint.Dispose();
            foreach (var slot in slots) slot.Dispose();
        }
    }
}
