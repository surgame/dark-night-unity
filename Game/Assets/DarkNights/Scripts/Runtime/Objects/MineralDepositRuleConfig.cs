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
    }
}
