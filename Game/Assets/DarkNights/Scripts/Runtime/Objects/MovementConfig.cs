using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 移动能力的到达判定参数；默认值保留原规则的 0.8 坐标容差。
    /// 速度仍读取 ActorRuleConfig 指向的只读 balance 条目，不在此重复维护。
    /// </summary>
    [Serializable]
    public sealed class MovementConfig : IConfigData
    {
        public string Name => "单位移动";
        public float ArrivalDistance = 0.8f;
    }
}
