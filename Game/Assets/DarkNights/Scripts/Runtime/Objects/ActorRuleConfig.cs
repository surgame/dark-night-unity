using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 单位能力使用的只读规则引用；RuleKey 明确定位 balance.json 的对应条目。
    /// 不依赖 Definition 的显示名称或 Key 后缀，不保存实例生命、订单或生产进度。
    /// </summary>
    [Serializable]
    public sealed class ActorRuleConfig : IConfigData
    {
        public string Name => "单位规则";
        [DarkNights.Runtime.Framework.RuleKey("units")]
        public string RuleKey = "";
    }
}
