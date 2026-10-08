using System;
using DarkNights.Core.Config;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>手电工具的作者配置；通过 YYGC 生成注入冻结，不承担购买或角色状态。</summary>
    [Serializable]
    public sealed class FlashlightToolConfig : IConfigData
    {
        public string Name => "手电照明能力";
        public bool Starter = true;
        public float Range = 14;
        public float Cone = 90;
        public float Intensity = 1.35f;
        public float NearRange = 2.2f;
        public float NearIntensity = .55f;
        public float Red = 1, Green = .88f, Blue = .69f;

        public FlashlightRules Freeze() => new FlashlightRules(Range, Cone, Intensity,
            NearRange, NearIntensity, Red, Green, Blue);
    }
}
