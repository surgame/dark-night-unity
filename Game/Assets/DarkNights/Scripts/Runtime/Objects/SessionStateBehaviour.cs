using System;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话托管状态的公共写入边界；StatefulBehaviour 仍是状态及池生命周期的唯一所有者。
    /// 临时草稿仅存在于一次同步事务，整个提交结束前查询读取草稿，外部表现只消费冻结帧。
    /// </summary>
    public abstract class SessionStateBehaviour<TState> : StatefulBehaviour<TState>, IObjectMutation
        where TState : class, IStateData, new()
    {
        private TState draft;
        private SessionStateChange prepared;
        protected ObjectSession Session { get; private set; }
        internal ObjectSession World => Session;
        protected TState Current => draft ?? State;
        public ObjectInstance Object => _context.Owner;
        public override SyncMode NetworkMode => SyncMode.Session;

        protected override void Initialize() { }

        protected override void OnReset()
        {
            draft = null;
            Session = _context.Session?.Container.Resolve<ObjectSession>();
        }

        internal TState Edit()
        {
            if (Session == null || !HasStateAuthority)
                throw new InvalidOperationException("Object is not bound to the current authoritative session.");
            Session.Mutations.RequireWriting();
            if (draft == null)
            {
                draft = CaptureState() ?? new TState();
                Session.Mutations.Touch(this);
            }
            return draft;
        }

        internal TState Read() => Current;

        internal void PrepareState(TState state)
        {
            if (Session == null || Object.IsActive || !HasStateAuthority || State != null)
                throw new InvalidOperationException("Initial state requires an unactivated authoritative object.");
            MutateStateAsync(value => value.CopyFrom(state));
        }

        SessionStateChange IObjectMutation.PrepareCommit()
        {
            if (!HasStateAuthority || draft == null)
                throw new InvalidOperationException("Object retired before its transaction could commit.");
            prepared = PrepareStateChange(draft);
            return prepared;
        }

        void IObjectMutation.Complete()
        {
            try { prepared?.Dispose(); }
            finally { prepared = null; draft = null; }
        }

        public override void OnDespawn()
        {
            draft = null;
            Session = null;
            base.OnDespawn();
        }
    }
}
