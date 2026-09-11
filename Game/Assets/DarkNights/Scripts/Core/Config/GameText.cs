using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 从共享配置格式化原版资源名称、成本、时钟和建筑说明；不缓存状态、不结算支付，也不复制规则数字。
    /// 文案沿用冻结玩法，供各客户端在同一只读目录下独立展示。
    /// </summary>
    public static class GameText
    {
        public static IReadOnlyList<string> ResourceIds { get; } = Array.AsReadOnly(new[] { "food", "wood", "stone", "iron", "gold" });
        public static string ResourceName(string id) => id switch
        {
            "food" => "食物", "wood" => "木材", "stone" => "石材", "iron" => "铁", "gold" => "金币", _ => ""
        };
        public static string ShortName(string id) => id switch
        {
            "food" => "食", "wood" => "木", "stone" => "石", "iron" => "铁", "gold" => "金", _ => ""
        };
        public static string Number(double number) => number.ToString("0.##", CultureInfo.InvariantCulture);
        public static string Cost(ResourceAmounts cost) => string.Join("  ", ResourceIds.Where(id => cost.Get(id) != 0)
            .Select(id => ShortName(id) + Number(cost.Get(id))));
        public static string Clock(double seconds)
        {
            int value = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"{value / 60:00}:{value % 60:00}";
        }
        public static string Building(GameCatalog catalog, string kind)
        {
            BuildingDefinition definition = catalog.Balance.Buildings[kind];
            return kind switch
            {
                "house" => $"人口容量 +{definition.Capacity}",
                "farm" => $"一名工人耕作 · 每{Number(catalog.Balance.Worksites["food"].Interval)}秒产出{catalog.Balance.Worksites["food"].Yield}食物",
                "tower" => $"自动射击 · 射程{Number(definition.Range)} · 伤害{definition.Damage[0]}–{definition.Damage[1]}",
                "barracks" => $"工人逐一训练 · 队列最多{catalog.Balance.Economy.TrainingQueueLimit}人",
                _ => definition.Description
            };
        }
    }
}
