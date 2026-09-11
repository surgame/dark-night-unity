using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Entities;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Systems
{
    /// <summary>
    /// 本局库存与食物结算计时的唯一拥有者。多资源支付先完整验证再一次替换库存；生产统计、招募间隔和饥饿统一使用模拟秒数。
    /// </summary>
    public sealed class EconomyService
    {
        private readonly GameSession session;

        public EconomyService(GameSession session)
        {
            this.session = session;
        }

        public ResourceAmounts Stock { get; internal set; } = new();
        public double UpkeepElapsed { get; internal set; }
        public double StarvationElapsed { get; internal set; }
        public double RecruitCooldown { get; internal set; }
        public int Population => session.World.Actors.Count(a => !a.Enemy && a.Hp > 0);
        public int Capacity => session.World.Buildings.Where(b => b.IsComplete).Sum(b => b.Definition.Capacity);

        internal void Reset()
        {
            Stock = session.Catalog.Balance.Economy.StartingResources;
            UpkeepElapsed = 0;
            StarvationElapsed = 0;
            RecruitCooldown = 0;
        }

        public bool CanPay(ResourceAmounts cost) => cost.IsValid() && GameText.ResourceIds.All(id => Stock.Get(id) >= cost.Get(id));

        public bool Pay(ResourceAmounts cost)
        {
            if (!CanPay(cost))
                return false;
            Stock = Stock.Subtract(cost);
            return true;
        }

        public void Credit(ResourceAmounts values) => Stock = Stock.Add(values);

        internal void AddResource(string kind, double amount)
        {
            Stock = Stock.With(kind, Stock.Get(kind) + amount);
            session.Stats.Gathered = session.Stats.Gathered.With(kind, session.Stats.Gathered.Get(kind) + amount);
        }

        internal void Tick(double delta)
        {
            var rules = session.Catalog.Balance.Economy;
            RecruitCooldown = Math.Max(0, RecruitCooldown - delta);
            UpkeepElapsed += delta;
            while (UpkeepElapsed >= rules.UpkeepInterval)
            {
                UpkeepElapsed -= rules.UpkeepInterval;
                Stock = Stock.With("food", Math.Max(0, Stock.Food - Population * rules.FoodPerPerson));
            }
            if (Stock.Food > 0)
            {
                StarvationElapsed = 0;
                return;
            }
            StarvationElapsed += delta;
            if (StarvationElapsed < rules.StarvationInterval)
                return;
            StarvationElapsed = 0;
            session.Feedback.Notify("食物耗尽！安排工人耕作，居民正在挨饿。", true);
            foreach (var actor in session.World.Actors.ToArray())
                if (!actor.Enemy)
                    session.Combat.Damage(actor, 1, true);
        }
    }
}
