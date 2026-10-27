using System;
using System.Linq;
using DarkNights.Core.ViewData;
using GameCore.Objects.Definition;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 四格图标、背包货物和情境提示的本地只读展示；图标使用装备定义的显式资源引用。
    /// 短提示按非缩放时间自动结束，缓存只保留四格身份，换连接或世界时由宿主重置。
    /// </summary>
    internal sealed class ShipHudReadout
    {
        private readonly VisualElement[] slots = new VisualElement[4];
        private readonly Image[] icons = new Image[4];
        private readonly VisualElement[] lights = new VisualElement[4];
        private readonly Label[] entries = new Label[4];
        private readonly string[] definitions = new string[4];
        private readonly Label name, hint, key, receipt, rally, credits, shopCredits, minerals, cargo;
        private readonly VisualElement interaction, root;
        private int actorId, selected = -1, selectedItem = -1;
        private double nameUntil, receiptUntil;

        internal ShipHudReadout(VisualElement root)
        {
            this.root = root;
            for (int i = 0; i < 4; i++)
            {
                slots[i] = root.Q("slot" + i);
                icons[i] = root.Q<Image>("slot-icon" + i);
                icons[i].scaleMode = ScaleMode.ScaleToFit;
                lights[i] = root.Q("slot-light" + i);
                entries[i] = root.Q<Label>("inventory-slot" + i);
            }
            name = root.Q<Label>("equipment-name"); hint = root.Q<Label>("interaction-text");
            key = root.Q<Label>("interaction-key"); interaction = root.Q("interaction");
            receipt = root.Q<Label>("receipt"); rally = root.Q<Label>("rally");
            credits = root.Q<Label>("inventory-credits"); shopCredits = root.Q<Label>("credits");
            minerals = root.Q<Label>("inventory-minerals"); cargo = root.Q<Label>("ship-cargo");
        }

        internal void Present(ActorViewData actor, int balance, int iron, int gold)
        {
            int index = actor?.SelectedItem ?? -1;
            bool sameActor = actorId == (actor?.Id ?? 0);
            if (!sameActor) { Reset(); actorId = actor?.Id ?? 0; }
            for (int i = 0; i < 4; i++)
            {
                int item = Item(actor, i);
                string identity = Identity(actor, i);
                if (definitions[i] != identity)
                {
                    definitions[i] = identity;
                    icons[i].sprite = ResolveIcon(identity);
                }
                slots[i].EnableInClassList("is-selected", i == index);
                slots[i].EnableInClassList("is-empty", item == 0);
                entries[i].text = (i + 1) + "  " + Name(item);
                lights[i].EnableInClassList("is-hidden", item != 4 || actor?.LightEnabled != true);
            }
            int currentItem = index < 0 ? 0 : Item(actor, index);
            if (sameActor && (selected != index || selectedItem != currentItem) && currentItem != 0)
            {
                name.text = Name(currentItem);
                nameUntil = Time.unscaledTimeAsDouble + 1.5;
            }
            selected = index; selectedItem = currentItem;
            name.EnableInClassList("is-hidden", Time.unscaledTimeAsDouble >= nameUntil);
            receipt.EnableInClassList("is-hidden", Time.unscaledTimeAsDouble >= receiptUntil);
            credits.text = "共享信用点  " + balance;
            shopCredits.text = credits.text;
            minerals.text = "铁  " + iron + "     金  " + gold;
        }

        internal void PresentContext(string prompt, string binding, bool permitted, bool blocked,
            JourneyViewData journey, ExpeditionViewData expedition)
        {
            hint.text = prompt; key.text = binding;
            interaction.EnableInClassList("is-hidden", blocked || string.IsNullOrEmpty(prompt));
            interaction.EnableInClassList("is-unavailable", !permitted);
            bool gathering = journey?.Phase == JourneyPhase.Gathering && expedition != null;
            rally.EnableInClassList("is-hidden", blocked || !gathering);
            if (gathering)
                rally.text = "等待登船   " + expedition.Crew.Count(value => value.Boarded) + " / " + expedition.Crew.Count;
        }

        internal void PresentCargo(int iron, int gold, long income) =>
            cargo.text = "船货  铁 " + iron + " / 金 " + gold + " · 预计收入 " + income;

        internal void ShowReceipt(string value)
        {
            receipt.text = value;
            receiptUntil = Time.unscaledTimeAsDouble + 2.6;
        }

        internal void PlaceContext(Vector2 screen)
        {
            if (root.panel == null || root.layout.width < 200) return;
            Vector2 point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            interaction.style.left = Mathf.Clamp(point.x - 90, 12, root.layout.width - 192);
            interaction.style.top = Mathf.Clamp(point.y - 48, 12, root.layout.height - 120);
        }

        internal void Reset()
        {
            actorId = 0; selected = selectedItem = -1; nameUntil = receiptUntil = 0;
            name.EnableInClassList("is-hidden", true); receipt.EnableInClassList("is-hidden", true);
        }

        private static Sprite ResolveIcon(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return null;
            var definition = ObjectDefinitionDatabase.Instance.Definitions
                .FirstOrDefault(value => value != null && value.Guid.ToString() == identity);
            if (definition == null || definition.Icon == null)
                throw new InvalidOperationException("装备HUD缺少已注册定义或图标：" + identity);
            return definition.Icon;
        }

        private static int Item(ActorViewData actor, int slot) => actor == null ? 0 : slot switch
        { 0 => actor.Slot0, 1 => actor.Slot1, 2 => actor.Slot2, _ => actor.Slot3 };
        private static string Identity(ActorViewData actor, int slot) => actor == null ? "" : slot switch
        { 0 => actor.Slot0Definition, 1 => actor.Slot1Definition, 2 => actor.Slot2Definition, _ => actor.Slot3Definition };
        private static string Name(int item) => item switch
        { 1 => "手枪", 2 => "矿镐", 3 => "炸药", 4 => "手电筒", _ => "空" };
    }
}
