using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>飞船子终端 Definition 的服务身份与交互范围；启用状态由子物体决定，不持有收益。</summary>
    [Serializable]
    public sealed class ShipServiceConfig : IConfigData
    {
        public string Name => "飞船服务";
        public string Service = "";
        public float Radius = 18;

        public void Validate()
        {
            if (Service != "sale" && Service != "shop" || float.IsNaN(Radius) ||
                float.IsInfinity(Radius) || Radius < 8 || Radius > 48)
                throw new InvalidOperationException("飞船服务配置无效。");
        }
    }
}
