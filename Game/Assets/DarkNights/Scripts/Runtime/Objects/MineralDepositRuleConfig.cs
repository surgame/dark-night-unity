using System;
using GameCore.Objects.Behaviours.Interfaces;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>矿物作者 Definition 的静态采集配置；正式会话冻结到矿层规则，格耐久和储量归原生地图，旧 Behaviour 仅保留作者引用。</summary>
    [Serializable]
    public sealed class MineralDepositRuleConfig : IConfigData
    {
        public const string Rule = "mineral-deposit";
        public string Name => "矿床规则";
        [DarkNights.Runtime.Framework.RuleKey("mineral-deposits")]
        public string RuleKey = Rule;
        [InspectorName("最低采集等级")] public int RequiredMiningLevel = 1;
        public int HarvestDurability = 40;
        public int UnitsPerHarvest = 1;
        public string CommonResource = "iron";
        public string RareResource = "gold";
        public string Fingerprint()
        {
            Validate();
            return HarvestDurability.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                UnitsPerHarvest.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + CommonResource + "|" + RareResource + "|" + RequiredMiningLevel;
        }
        public void Validate()
        {
            if (RequiredMiningLevel < 1 || RequiredMiningLevel > 1000 || HarvestDurability < 1 || HarvestDurability > 1000000 || UnitsPerHarvest < 1 || UnitsPerHarvest > 1000 ||
                CommonResource != "iron" && CommonResource != "gold" || RareResource != "iron" && RareResource != "gold")
                throw new InvalidOperationException("矿床采集耐久、产量或资源不合法。");
        }
    }
}
