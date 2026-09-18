using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// YYGC 单位家族的状态所有者；订单、行动时钟和工位关系只写 ActorState。
    /// 移动能力复用同一状态，会话按照固定顺序调用 Tick；不创建或引用旧 Core Actor。
    /// </summary>
    [RequireConfig(typeof(ActorRuleConfig))]
    public sealed partial class ActorBehaviour : SessionStateBehaviour<ActorState>, IActorCapability
    {
        [Inject] private ActorRuleConfig config;
        [Inject] private IMovementCapability movement;
        [Inject] private IActorCombatCapability combat;
        [Inject] private IAutomaticActorControl automatic;
        [Inject(Optional = true)] private HeroControlBehaviour hero;
        public int Id => Current?.Id ?? 0;
        public string RuleKey => config.RuleKey;
        public string DefinitionGuid => Object.Definition.Guid.ToString();
        public string PlacementKey => Current?.PlacementKey ?? "";
        public UnitDefinition Definition => Session.Catalog.Balance.Units[RuleKey];
        public float X => Current.X;
        public double Hp => Current.Hp;
        public double MaximumHp => Definition.Hp;
        public bool Enemy => Current.Enemy;
        public string Name => Current.Name;
        public ActorActivity Activity => Current.Activity;
        public int TargetId => Current.TargetId;
        public bool IsTraining => Activity == ActorActivity.Training || Activity == ActorActivity.TrainingMove;

        protected override void OnReset()
        {
            base.OnReset();
            if (config == null || string.IsNullOrWhiteSpace(config.RuleKey) || movement == null || combat == null || automatic == null)
                throw new InvalidOperationException("Actor requires a RuleKey, movement and combat capabilities.");
            if (Session != null && !Session.Catalog.Balance.Units.ContainsKey(config.RuleKey))
                throw new InvalidOperationException("Unknown actor RuleKey: " + config.RuleKey);
        }

        internal void Prepare(int id, float x, string placement, bool enemy, string name)
        {
            PrepareState(new ActorState
            {
                Id = id, PlacementKey = placement, X = x, Hp = Definition.Hp,
                Enemy = enemy, Name = string.IsNullOrEmpty(name) ? Definition.Name : name,
                Activity = ActorActivity.Idle, MoveX = x, RallyX = x,
                Face = enemy ? -1 : 1, AiClock = id * 0.07 % 0.25,
                JetpackFuel = Session.Catalog.Balance.HeroControl?.FuelSeconds ?? 0,
                ExplosiveCharges = enemy ? 0 : 3,
                DrillCharges = enemy ? 0 : 1,
                LastTerrainActionTick = -1000
            });
        }

        internal void OrderMove(float x)
        {
            if (IsTraining) return;
            Session.Work.Clear(this);
            ActorState state = Edit();
            state.MoveX = Math.Clamp(x, 16, Session.Layout.WorldWidth - 16);
            state.RallyX = state.MoveX;
            state.Activity = ActorActivity.Move;
        }

        internal void Tick(double delta)
        {
            if (Hp <= 0) return;
            ActorState state = Edit();
            state.ActionTime += delta;
            state.AttackClock = Math.Max(0, state.AttackClock - delta);
            state.HitFlash = Math.Max(0, state.HitFlash - delta);
            state.AiClock = Math.Max(0, state.AiClock - delta);
            state.Walking = false;
            if (hero == null || !hero.Tick(delta)) automatic.Tick(delta);
        }
    }
}
