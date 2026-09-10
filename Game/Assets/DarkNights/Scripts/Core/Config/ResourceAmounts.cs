using System;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 五类资源的不可变数值，用于成本、补给及库存快照。运算返回新值，
    /// 不修改共享配置；支付是否允许由权威经济规则判断，不在此处截断负数。
    /// </summary>
    public sealed class ResourceAmounts
    {
        public double Food { get; }
        public double Wood { get; }
        public double Stone { get; }
        public double Iron { get; }
        public double Gold { get; }

        public ResourceAmounts(double food = 0, double wood = 0, double stone = 0, double iron = 0, double gold = 0)
        {
            Food = food;
            Wood = wood;
            Stone = stone;
            Iron = iron;
            Gold = gold;
        }

        public double Get(string id) => id switch
        {
            "food" => Food,
            "wood" => Wood,
            "stone" => Stone,
            "iron" => Iron,
            "gold" => Gold,
            _ => throw new ArgumentException("Unknown resource: " + id, nameof(id))
        };

        public ResourceAmounts With(string id, double value) => id switch
        {
            "food" => new ResourceAmounts(value, Wood, Stone, Iron, Gold),
            "wood" => new ResourceAmounts(Food, value, Stone, Iron, Gold),
            "stone" => new ResourceAmounts(Food, Wood, value, Iron, Gold),
            "iron" => new ResourceAmounts(Food, Wood, Stone, value, Gold),
            "gold" => new ResourceAmounts(Food, Wood, Stone, Iron, value),
            _ => throw new ArgumentException("Unknown resource: " + id, nameof(id))
        };

        public ResourceAmounts Add(ResourceAmounts value) => new ResourceAmounts(
            Food + value.Food, Wood + value.Wood, Stone + value.Stone, Iron + value.Iron, Gold + value.Gold);

        public ResourceAmounts Subtract(ResourceAmounts value) => new ResourceAmounts(
            Food - value.Food, Wood - value.Wood, Stone - value.Stone, Iron - value.Iron, Gold - value.Gold);

        public bool IsValid(double maximum = 10000000) =>
            Valid(Food, maximum) && Valid(Wood, maximum) && Valid(Stone, maximum) &&
            Valid(Iron, maximum) && Valid(Gold, maximum);

        private static bool Valid(double value, double maximum) =>
            !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= maximum;
    }
}
