using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>矿床 ObjectDefinition 的静态规则入口；容量和稀有度实例值由地图蓝图复制到 MineralDepositState。</summary>
    [Serializable]
    public sealed class MineralDepositRuleConfig : IConfigData
    {
        public const string Rule = "mineral-deposit";
        public string Name => "矿床规则";
        [DarkNights.Runtime.Framework.RuleKey("mineral-deposits")]
        public string RuleKey = Rule;
        public int HarvestDurability = 40;
        public int UnitsPerHarvest = 1;
        public string CommonResource = "iron";
        public string RareResource = "gold";
        public string Fingerprint()
        {
            Validate();
            return HarvestDurability.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                UnitsPerHarvest.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + CommonResource + "|" + RareResource;
        }
        public void Validate()
        {
            if (HarvestDurability < 1 || HarvestDurability > 1000000 || UnitsPerHarvest < 1 || UnitsPerHarvest > 1000 ||
                CommonResource != "iron" && CommonResource != "gold" || RareResource != "iron" && RareResource != "gold")
                throw new InvalidOperationException("矿床采集耐久、产量或资源不合法。");
        }
    }
}
