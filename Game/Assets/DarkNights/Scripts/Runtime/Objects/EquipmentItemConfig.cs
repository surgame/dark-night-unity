using System;
using DarkNights.Core.Config;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>库存道具 Definition 的能力身份；空商品键允许仅配发的道具，价格仍唯一归 Trade 规则。</summary>
    [Serializable]
    public sealed class EquipmentItemConfig : IConfigData
    {
        public string Name => "库存道具";
        public string RuleKey = "";
        public bool Jetpack;
        public HeroEquipmentKind Handheld;

        public void Validate()
        {
            if (RuleKey == "" && !Jetpack && Handheld == HeroEquipmentKind.Flashlight) return;
            HeroEquipmentKind expected = RuleKey switch
            {
                "pistol" => HeroEquipmentKind.Pistol, "pickaxe" => HeroEquipmentKind.Pickaxe,
                "bomb" => HeroEquipmentKind.Bomb, "jetpack" => HeroEquipmentKind.Empty, _ => (HeroEquipmentKind)(-1)
            };
            if (expected != Handheld || Jetpack != (RuleKey == "jetpack"))
                throw new InvalidOperationException("装备定义的能力身份无效。");
        }
    }
}
