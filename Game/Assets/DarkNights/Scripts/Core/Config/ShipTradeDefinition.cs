using System;

namespace DarkNights.Core.Config
{
    /// <summary>飞船交易的唯一数值规则，矿石售价和装备成本来自 balance；定义资产只引用商品身份。</summary>
    public sealed class ShipTradeDefinition
    {
        public int StartingCredits { get; }
        public int IronPrice { get; }
        public int GoldPrice { get; }
        public int PistolPrice { get; }
        public int PickaxePrice { get; }
        public int JetpackPrice { get; }

        public ShipTradeDefinition(int startingCredits, int ironPrice, int goldPrice,
            int pistolPrice, int pickaxePrice, int jetpackPrice)
        {
            foreach (int value in new[] { startingCredits, ironPrice, goldPrice, pistolPrice, pickaxePrice, jetpackPrice })
                if (value < 0 || value > 100000) throw new ArgumentOutOfRangeException(nameof(value));
            if (ironPrice == 0 || goldPrice == 0 || pickaxePrice == 0)
                throw new ArgumentException("Ore and basic mining tool require positive prices.");
            StartingCredits = startingCredits; IronPrice = ironPrice; GoldPrice = goldPrice;
            PistolPrice = pistolPrice; PickaxePrice = pickaxePrice; JetpackPrice = jetpackPrice;
        }

        public long SaleValue(int iron, int gold)
        {
            if (iron < 0 || gold < 0 || iron > 10000 || gold > 10000)
                throw new ArgumentOutOfRangeException(nameof(iron));
            return (long)iron * IronPrice + (long)gold * GoldPrice;
        }
    }
}
