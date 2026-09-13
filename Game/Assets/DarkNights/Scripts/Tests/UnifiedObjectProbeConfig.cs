using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Tests
{
    /// <summary>用于验证 RequireConfig 及共享配置只读边界的测试配置；Number 不随实例写入改变。</summary>
    [Serializable]
    public sealed class UnifiedObjectProbeConfig : IConfigData
    {
        public string Name => "装配回归配置";
        public int Number = 10;
    }
}
