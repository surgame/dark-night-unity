using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// YYGC 工位家族的存量与生产状态所有者；生产只接受仍然一致的双向工人占用。
    /// 到达前不累积生产，解除任务清空余量，耗尽反馈与库存一起提交。
    /// </summary>
    [RequireConfig(typeof(WorksiteRuleConfig))]
    public sealed partial class WorksiteBehaviour : SessionStateBehaviour<WorksiteState>, IWorksiteCapability
    {
        [Inject] private WorksiteRuleConfig config;
        public int Id => Current?.Id ?? 0;
        public string RuleKey => config.RuleKey;
        public string DefinitionGuid => Object.Definition.Guid.ToString();
        public string PlacementKey => Current?.PlacementKey ?? "";
        public WorksiteDefinition Definition => Session.Catalog.Balance.Worksites[config.RuleKey];
        public float X => Current.X;
        public int WorkerId => Current.WorkerId;
        public int Amount => Current.Amount;
        public int FarmId => Current.FarmId;

        protected override void OnReset()
        {
            base.OnReset();
            if (config == null || string.IsNullOrWhiteSpace(config.RuleKey) ||
                (Session != null && !Session.Catalog.Balance.Worksites.ContainsKey(config.RuleKey)))
                throw new InvalidOperationException("Unknown worksite RuleKey.");
        }

        internal void Prepare(int id, float x, string placement, int variant, int farmId)
        {
            PrepareState(new WorksiteState
            {
                Id = id, PlacementKey = placement, X = x, Variant = variant,
                FarmId = farmId, Amount = Definition.Amount
            });
        }

        internal void Tick(double delta)
        {
            if (WorkerId == 0 || Amount == 0) return;
            var worker = Session.Index.Find<ActorBehaviour>(WorkerId);
            WorksiteState state = Edit();
            if (worker == null || worker.Hp <= 0 || worker.TargetId != Id)
            {
                state.WorkerId = 0;
                state.Progress = 0;
                return;
            }
            if (worker.Activity != ActorActivity.Work) return;
            state.Progress += delta;
            while (state.Progress >= Definition.Interval)
            {
                state.Progress -= Definition.Interval;
                int quantity = state.Amount < 0 ? Definition.Yield : Math.Min(state.Amount, Definition.Yield);
                Session.Economy.AddResource(RuleKey, quantity);
                Session.Emit(new VisualCue("resource", X, Session.Layout.GroundY - 22, "+" + quantity, RuleKey));
                if (state.Amount > 0) state.Amount -= quantity;
                if (state.Amount != 0) continue;
                Session.Work.Clear(worker);
                Session.Notify(Definition.Name + "已采尽，工人等待新安排。");
                break;
            }
        }

    }
}
