using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;

namespace DarkNights.Core.Logic.Entities
{
    /// <summary>
    /// 一处单人工位，独占生产计时和有限存量；农田以-1表示无限存量。只有占用者已到达并保持双向关系时结算，解除任务会清除未完成进度。
    /// </summary>
    public sealed class Worksite : Entity
    {
        private readonly GameSession _session;
        public WorksiteDefinition Definition { get; }
        public int WorkerId { get; internal set; }
        public int Amount { get; internal set; }
        public double Progress { get; internal set; }
        public int Variant { get; }
        public int FarmId { get; }

        internal Worksite(GameSession session, int id, string kind, float x, int variant, int farmId) : base(id, kind, x)
        {
            _session = session;
            Definition = session.Catalog.Balance.Worksites[kind];
            Amount = Definition.Amount;
            Variant = variant;
            FarmId = farmId;
        }

        internal void Tick(double delta)
        {
            if (WorkerId == 0 || Amount == 0)
                return;
            var worker = _session.World.Find<Actor>(WorkerId);
            if (worker == null || worker.Hp <= 0 || worker.TargetId != Id)
            {
                WorkerId = 0;
                Progress = 0;
                return;
            }
            if (worker.State != ActorActivity.Work)
                return;
            Progress += delta;
            while (Progress >= Definition.Interval)
            {
                Progress -= Definition.Interval;
                int quantity = Amount < 0 ? Definition.Yield : Math.Min(Amount, Definition.Yield);
                _session.Economy.AddResource(Kind, quantity);
                _session.Feedback.Emit(new("resource", X, _session.GroundY - 22, $"+{quantity}", Kind));
                if (Amount > 0)
                    Amount -= quantity;
                if (Amount != 0)
                    continue;
                worker.ClearOrder();
                _session.Feedback.Notify($"{Definition.Name}已采尽，工人等待新安排。");
                break;
            }
        }
    }
}
