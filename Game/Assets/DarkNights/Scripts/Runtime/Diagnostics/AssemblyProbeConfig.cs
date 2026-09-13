using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Diagnostics
{
    /// <summary>装配验收使用的共享只读配置；只装入临时 Definition，不加入正式对象资产。</summary>
    [Serializable]
    public sealed class AssemblyProbeConfig : IConfigData
    {
        public string Name => "装配验收";
        public int Value = 7;
    }
}
