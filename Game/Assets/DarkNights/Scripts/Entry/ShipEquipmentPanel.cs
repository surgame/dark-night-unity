using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using GameCore.UI;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>船内四格装备与独立喷气能力的 YYGC UIToolkit 面板；只订阅冻结副本并发送购买意图。</summary>
    [UIAsset("dark_nights.ui.ship_equipment")]
    public sealed class ShipEquipmentPanel : UIPanel
    {
        private readonly Subject<string> credits = new Subject<string>();
        private readonly Subject<string> hint = new Subject<string>();
        private readonly Subject<string>[] slots =
            { new Subject<string>(), new Subject<string>(), new Subject<string>(), new Subject<string>() };
        private readonly VisualElement[] fuelTicks = new VisualElement[16];
        private VisualElement shop;
        private Label jetpack;
        private Button pistol, pickaxe, pack;
        public event Action<string> BuyRequested;
        public event Action Closed;
        public bool ShopOpen { get; private set; }
        public bool AttachedAndVisible => Root?.panel != null && Root.resolvedStyle.display != DisplayStyle.None &&
            Root.worldBound.width > 0 && Root.worldBound.height > 0;

        protected override void OnBind()
        {
            Root.AddToClassList("ship-equipment-panel");
            Root.pickingMode = PickingMode.Ignore;
            Root.style.position = Position.Absolute;
            Root.style.left = Root.style.top = Root.style.right = Root.style.bottom = 0;
            Disposables.Add(credits.BindText(Root.Q<Label>("credits")));
            Disposables.Add(hint.BindText(Root.Q<Label>("hint")));
            for (int i = 0; i < slots.Length; i++)
                Disposables.Add(slots[i].BindText(Root.Q<Label>("slot" + i)));
            shop = Root.Q<VisualElement>("shop");
            jetpack = Root.Q<Label>("jetpack");
            var ring = Root.Q<VisualElement>("fuel-ring");
            for (int i = 0; i < fuelTicks.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / fuelTicks.Length;
                var tick = new VisualElement { name = "fuel-tick-" + i };
                tick.AddToClassList("fuel-tick");
                tick.style.left = 24 + Mathf.Sin(angle) * 19;
                tick.style.top = 24 - Mathf.Cos(angle) * 19;
                ring.Add(tick); fuelTicks[i] = tick;
            }
            pistol = Root.Q<Button>("buy-pistol"); pickaxe = Root.Q<Button>("buy-pickaxe");
            pack = Root.Q<Button>("buy-jetpack");
            pistol.clicked += BuyPistol; pickaxe.clicked += BuyPickaxe; pack.clicked += BuyPack;
            Root.Q<Button>("close").clicked += Close;
            SetShop(false);
        }

        public void Present(ActorViewData actor, int balance, int iron, int gold, double fuelCapacity,
            ShipTradeDefinition prices, string interaction, bool active)
        {
            Root.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            credits.OnNext("信用点 " + balance + "  ·  装备库存");
            hint.OnNext(interaction);
            int[] items = { actor?.Slot0 ?? 0, actor?.Slot1 ?? 0, actor?.Slot2 ?? 0, actor?.Slot3 ?? 0 };
            for (int i = 0; i < slots.Length; i++)
            {
                string name = items[i] switch { 1 => "手枪", 2 => "矿镐", 3 => "炸药", _ => "空" };
                slots[i].OnNext((actor?.SelectedItem == i ? "▶ " : "") + (i + 1) + "  " + name);
            }
            bool owned = actor?.JetpackOwned == true;
            jetpack.text = owned ? "喷气背包 · 已购买（暂未开放使用）" : "喷气背包 · 未购买";
            float ratio = owned && fuelCapacity > 0 ? Mathf.Clamp01((float)(actor.JetpackFuel / fuelCapacity)) : 0;
            for (int i = 0; i < fuelTicks.Length; i++)
                fuelTicks[i].style.opacity = owned && i < Mathf.CeilToInt(ratio * fuelTicks.Length) ? 1f : .18f;
            pistol.text = "手枪 · " + prices.PistolPrice;
            pickaxe.text = "矿镐 · " + prices.PickaxePrice;
            pack.text = "喷气背包 · " + prices.JetpackPrice;
            pistol.SetEnabled(balance >= prices.PistolPrice && Array.IndexOf(items, 1) < 0 && Array.IndexOf(items, 0) >= 0);
            pickaxe.SetEnabled(balance >= prices.PickaxePrice && Array.IndexOf(items, 2) < 0 && Array.IndexOf(items, 0) >= 0);
            pack.SetEnabled(balance >= prices.JetpackPrice && !owned);
        }

        public void SetShop(bool visible)
        {
            ShopOpen = visible;
            if (shop != null) shop.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) Root.Q<Button>("close").Focus();
        }

        private void BuyPistol() => BuyRequested?.Invoke("pistol");
        private void BuyPickaxe() => BuyRequested?.Invoke("pickaxe");
        private void BuyPack() => BuyRequested?.Invoke("jetpack");
        private void Close() { SetShop(false); Closed?.Invoke(); }
        protected override void OnDispose()
        {
            pistol.clicked -= BuyPistol; pickaxe.clicked -= BuyPickaxe; pack.clicked -= BuyPack;
            Root.Q<Button>("close").clicked -= Close;
            BuyRequested = null; Closed = null;
            credits.Dispose(); hint.Dispose();
            foreach (var slot in slots) slot.Dispose();
        }
    }
}
