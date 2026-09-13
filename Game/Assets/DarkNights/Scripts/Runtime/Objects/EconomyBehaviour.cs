using System;
using System.Linq;
using DarkNights.Core.Config;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话对象上的经济能力，独占库存与消耗计时；人口和容量仅查询本局 YYGC 对象索引。
    /// 成本先整体核验，扣款与工位或建筑变更在同一个事务中发布。
    /// </summary>
    public sealed partial class EconomyBehaviour : SessionStateBehaviour<EconomyState>
    {
        public ResourceAmounts Stock => new ResourceAmounts(Current.Food, Current.Wood, Current.Stone, Current.Iron, Current.Gold);
        public ResourceAmounts Gathered => new ResourceAmounts(Current.GatheredFood, Current.GatheredWood,
            Current.GatheredStone, Current.GatheredIron, Current.GatheredGold);
        public int Population => Session.Index.Actors.Count(a => !a.Enemy && a.Hp > 0);
        public int Capacity => Session.Index.Buildings.Where(b => b.IsComplete).Sum(b => b.Definition.Capacity);

        internal void Prepare()
        {
            ResourceAmounts values = Session.Catalog.Balance.Economy.StartingResources;
            PrepareState(new EconomyState
            {
                Food = values.Food, Wood = values.Wood, Stone = values.Stone,
                Iron = values.Iron, Gold = values.Gold
            });
        }

        public bool CanPay(ResourceAmounts cost) => cost != null && cost.IsValid() &&
            GameText.ResourceIds.All(id => Stock.Get(id) >= cost.Get(id));

        internal bool Pay(ResourceAmounts cost)
        {
            if (!CanPay(cost)) return false;
            SetStock(Stock.Subtract(cost));
            return true;
        }

        internal void SetStock(ResourceAmounts value)
        {
            EconomyState state = Edit();
            state.Food = value.Food;
            state.Wood = value.Wood;
            state.Stone = value.Stone;
            state.Iron = value.Iron;
            state.Gold = value.Gold;
        }

        internal void AddResource(string kind, int amount)
        {
            SetStock(Stock.With(kind, Stock.Get(kind) + amount));
            EconomyState state = Edit();
            switch (kind)
            {
                case "food": state.GatheredFood += amount; break;
                case "wood": state.GatheredWood += amount; break;
                case "stone": state.GatheredStone += amount; break;
                case "iron": state.GatheredIron += amount; break;
                case "gold": state.GatheredGold += amount; break;
                default: throw new ArgumentException("Unknown resource: " + kind);
            }
        }

        internal void Tick(double delta)
        {
            EconomyDefinition rules = Session.Catalog.Balance.Economy;
            EconomyState state = Edit();
            state.RecruitCooldown = Math.Max(0, state.RecruitCooldown - delta);
            state.UpkeepElapsed += delta;
            while (state.UpkeepElapsed >= rules.UpkeepInterval)
            {
                state.UpkeepElapsed -= rules.UpkeepInterval;
                state.Food = Math.Max(0, state.Food - Population * rules.FoodPerPerson);
            }
            if (state.Food > 0) { state.StarvationElapsed = 0; return; }
            state.StarvationElapsed += delta;
            if (state.StarvationElapsed < rules.StarvationInterval) return;
            state.StarvationElapsed = 0;
            Session.Notify("食物耗尽！安排工人耕作，居民正在挨饿。", true);
            foreach (ActorBehaviour actor in Session.Index.Actors.ToArray())
                if (!actor.Enemy) Session.Starve(actor);
        }
    }
}
