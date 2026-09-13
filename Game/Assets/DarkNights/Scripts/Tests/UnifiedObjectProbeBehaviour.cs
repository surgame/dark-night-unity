using System;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner.DI;
using R3;

namespace DarkNights.Tests
{
    /// <summary>通过真实 YYGC 注入、状态池和生命周期验证会话托管；不模拟 GameCore 装配器或网络身份。</summary>
    [RequireConfig(typeof(UnifiedObjectProbeConfig))]
    public sealed partial class UnifiedObjectProbeBehaviour : StatefulBehaviour<UnifiedObjectProbeState>, IStart, IUpdate
    {
        [Inject] private UnifiedObjectProbeConfig config;
        [Inject] private UnifiedObjectProbeDependency dependency;
        public override SyncMode NetworkMode => SyncMode.Session;
        public int StartCount { get; private set; }
        public int UpdateCount { get; private set; }
        public bool FailRetirement { get; set; }
        public bool CanWrite => HasStateAuthority;

        protected override void Initialize() { }
        protected override void OnReset()
        {
            StartCount = 0;
            UpdateCount = 0;
            FailRetirement = false;
            if (HasStateAuthority) Change(config.Number + dependency.Number);
        }

        public void Start() { StartCount++; }
        public void Update(float deltaTime) { UpdateCount++; }
        public override void OnDespawn()
        {
            base.OnDespawn();
            if (FailRetirement) throw new InvalidOperationException("Expected retirement failure");
        }
        public void Change(int value) => MutateStateAsync(state => { state.Number = value; state.Items.Add(value); });
        public void ChangeThenThrow() => MutateStateAsync(state =>
        {
            state.Number = 999;
            state.Items.Clear();
            throw new InvalidOperationException("Expected rollback");
        });

        public void ChangeAcrossLifecycle(Action changeLifecycle)
        {
            using var scope = MutateState();
            scope.Value.Number = 999;
            changeLifecycle();
        }

        public void ObserveInSession(Action<UnifiedObjectProbeState> observer)
        {
            ReactiveState.Subscribe(observer).AddTo(SessionDisposables);
        }
    }
}
