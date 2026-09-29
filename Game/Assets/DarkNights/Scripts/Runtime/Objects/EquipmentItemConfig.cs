using System;
using DarkNights.Core.Config;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>可购买装备 Definition 的能力身份；价格由 balance 的 Trade 规则唯一提供。</summary>
    [Serializable]
    public sealed class EquipmentItemConfig : IConfigData
    {
        public string Name => "可购买装备";
        public string RuleKey = "";
        public bool Jetpack;
        public HeroEquipmentKind Handheld;

        public void Validate()
        {
            if (RuleKey != "pistol" && RuleKey != "pickaxe" && RuleKey != "jetpack" ||
                Jetpack != (RuleKey == "jetpack") ||
                (!Jetpack && Handheld == HeroEquipmentKind.Empty) ||
                Jetpack && Handheld != HeroEquipmentKind.Empty)
                throw new InvalidOperationException("装备定义的能力身份无效。");
        }
    }
}
