using System;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>船体 Definition 对出售与商店子模块的显式引用和初始开关；替换 Prefab 时维持服务身份检查。</summary>
    [Serializable]
    public sealed class ShipServicesConfig : IConfigData
    {
        public string Name => "船舱服务模块";
        public DefinitionReference Sale;
        public DefinitionReference Shop;
        public bool SaleEnabled = true;
        public bool ShopEnabled = true;

        public void Validate()
        {
            if (Sale.IsEmpty || Shop.IsEmpty || Sale.GuidString == Shop.GuidString ||
                Sale.Resolve() == null || Shop.Resolve() == null)
                throw new InvalidOperationException("飞船服务定义引用无效。");
        }
    }
}
