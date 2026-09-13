using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// YYGC 建筑家族的唯一生命与施工状态所有者；占用关系通过会话事务维护。
    /// 完工增量保留受伤差额，动作与音效由提交后的冻结反馈驱动。
    /// </summary>
    [RequireConfig(typeof(BuildingRuleConfig))]
    public sealed partial class BuildingBehaviour : SessionStateBehaviour<BuildingState>, IBuildingCapability
    {
        [Inject] private BuildingRuleConfig config;
        public int Id => Current?.Id ?? 0;
        public string RuleKey => config.RuleKey;
        public string DefinitionGuid => Object.Definition.Guid.ToString();
        public string PlacementKey => Current?.PlacementKey ?? "";
        public BuildingDefinition Definition => Session.Catalog.Balance.Buildings[RuleKey];
        public float X => Current.X;
        public double Hp => Current.Hp;
        public double Progress => Current.Progress;
        public int WorkerId => Current.WorkerId;
        public int FarmSiteId => Current.FarmSiteId;
        public bool IsComplete => Progress >= 1 && Hp > 0;

        protected override void OnReset()
        {
            base.OnReset();
            if (config == null || string.IsNullOrWhiteSpace(config.RuleKey) ||
                (Session != null && !Session.Catalog.Balance.Buildings.ContainsKey(config.RuleKey)))
                throw new InvalidOperationException("Unknown building RuleKey.");
        }

        internal void Prepare(int id, float x, string placement, bool complete)
        {
            PrepareState(new BuildingState
            {
                Id = id, PlacementKey = placement, X = x, Progress = complete ? 1 : 0,
                Hp = Definition.Hp * (complete ? 1 : 0.2)
            });
        }

        internal void Tick(double delta)
        {
            if (Hp <= 0) return;
            BuildingState state = Edit();
            state.HitFlash = Math.Max(0, state.HitFlash - delta);
            state.AttackClock = Math.Max(0, state.AttackClock - delta);
            if (IsComplete) return;
            var worker = Session.Index.Find<ActorBehaviour>(WorkerId);
            if (worker == null || worker.Activity != ActorActivity.Build || worker.TargetId != Id) return;
            double increment = Math.Min(1 - Progress, delta / Definition.BuildSeconds);
            state.Progress = Math.Min(1, state.Progress + increment);
            state.Hp = Math.Min(Definition.Hp, state.Hp + increment * Definition.Hp * 0.8);
            if (!IsComplete) return;
            Session.Work.Clear(worker);
            Session.Notify(Definition.Name + "已建成。");
            Session.Mutations.AfterCommit(() => Session.Feedback.PlaySound("snd_upgrade_bld"));
        }
    }
}
