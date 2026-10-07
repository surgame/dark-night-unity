#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using DarkNights.Core.ViewData;
using GameCore.Objects.Definition;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>原四格持有装备及独立喷气能力的展示区；保留槽位布局，移除按钮发送定义身份而不改本地库存。</summary>
    internal sealed class DebugHeldEquipment
    {
        private readonly VisualElement root;
        private readonly Action<ObjectDefinition> remove;
        private readonly ObjectDefinition[] held = new ObjectDefinition[4];
        private ObjectDefinition jetpack;
        internal DebugHeldEquipment(VisualElement root, Action<ObjectDefinition> remove)
        {
            this.root = root; this.remove = remove;
            for (int index = 0; index < 4; index++)
            {
                int slot = index; root.Q<Button>("removeSlot" + index).clicked += () => { if (held[slot] != null) this.remove(held[slot]); };
            }
            root.Q<Button>("removeJetpack").clicked += () => { if (jetpack != null) this.remove(jetpack); };
        }
        internal void Present(ActorViewData actor, bool allowed)
        {
            for (int index = 0; index < 4; index++)
            {
                held[index] = DebugObjectCatalog.Equipment(DebugObjectCatalog.Slot(actor, index));
                string name = held[index]?.Name ?? "空槽位";
                if (held[index] != null && DebugObjectCatalog.Item(held[index])?.RuleKey == "bomb") name += " ×" + actor.ExplosiveCharges;
                root.Q<Label>("slot" + index).text = name;
                var button = root.Q<Button>("removeSlot" + index);
                button.SetEnabled(allowed && held[index] != null); button.EnableInClassList("rdh-hidden", held[index] == null);
            }
            jetpack = actor?.JetpackOwned == true ? ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.jetpack") : null;
            root.Q<Label>("jetpackStatus").text = jetpack == null ? "未持有 · 独立能力" : "已装备 · 燃料 " + actor.JetpackFuel.ToString("0.0");
            root.Q<Button>("removeJetpack").SetEnabled(allowed && jetpack != null);
            root.Q<Button>("removeJetpack").EnableInClassList("rdh-hidden", jetpack == null);
        }
    }
}
#endif
