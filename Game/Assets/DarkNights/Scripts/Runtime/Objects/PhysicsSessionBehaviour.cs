using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Physics;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 合作道具和绳索的唯一YYGC状态所有者；角色结果仍归各自ActorState。
    /// 只在会话物理候选启用时分配执行后端，一次写事务内收集、模拟、提交，失败撤销原生缓存。
    /// </summary>
    public sealed partial class PhysicsSessionBehaviour : SessionStateBehaviour<PhysicsWorldState>
    {
        private UnityPhysicsBackend backend;
        public bool Enabled => Session?.Layout.Physics != null;
        public PhysicsTrace Trace { get; private set; } = new PhysicsTrace();
        protected override void OnReset()
        {
            base.OnReset(); Trace = new PhysicsTrace();
        }

        public void StartRecording()
        {
            if (!Enabled || !Session.Context.IsActive) throw new InvalidOperationException("物理会话尚未激活。");
            Trace.Begin(Session.SaveCodec.Serialize(Session.CaptureWorld()), Session.Layout.Physics,
                Session.Resources.MovementFingerprint, Session.Terrain?.Map?.CommitId ?? 0);
        }

        internal void Prepare()
        {
            PrepareState(new PhysicsWorldState
            {
                Props = Session.Layout.Physics?.Props.Select(p => new PhysicsBodyState
                    { Id = p.Id, X = p.X, Height = p.Height }).ToArray() ?? Array.Empty<PhysicsBodyState>()
            });
        }

        internal void Advance(IReadOnlyList<ActorBehaviour> actors, double delta)
        {
            if (!Enabled) throw new InvalidOperationException("当前会话未选择物理候选。");
            if (delta <= 0 || delta > 1.0 / 15) throw new ArgumentOutOfRangeException(nameof(delta));
            PhysicsWorldState state = Edit();
            Session.Mutations.OnRollback(Invalidate);
            ActorMotionInput[] inputTrace = Array.Empty<ActorMotionInput>();
            try
            {
                backend = backend ?? new UnityPhysicsBackend(Session);
                backend.Begin(actors, state);
                foreach (var actor in actors) actor.BeginMotion(delta);
                if (Trace.Recording) inputTrace = backend.FreezeInputs();
                backend.Solve(actors, state, delta);
                foreach (var actor in actors) actor.FinishMotion(delta);
                var snapshot = Capture();
                PhysicsSnapshotRules.Validate(snapshot, Session.Layout.Physics, actors.Select(a => a.Id));
                if (Trace.Recording)
                {
                    var contacts = backend.Contacts(out bool truncated);
                    var trace = new PhysicsTraceFrame(snapshot, delta, inputTrace, contacts, truncated);
                    var map = Session.Terrain?.Map;
                    var changed = map != null && map.CommitId != Trace.LastTerrainRevision
                        ? new PhysicsTerrainTrace(snapshot.Tick, map.CommitId, Session.Terrain.Capture()) : null;
                    Session.Mutations.AfterCommit(() => Trace.Record(trace, changed));
                }
            }
            catch (Exception error)
            {
                Trace.Fail(delta, inputTrace.Length > 0 ? inputTrace : backend?.FreezeInputs() ?? Array.Empty<ActorMotionInput>(), error);
                throw;
            }
        }

        internal void Submit(ActorMotionInput input, MovementDefinition rules)
        {
            if (!Enabled || backend == null) throw new InvalidOperationException("角色运动必须由会话物理批次调度。");
            backend.Submit(input, rules);
        }

        internal int ToggleLink(ActorBehaviour owner, int targetId)
        {
            if (!Enabled || owner == null || !owner.Read().ManualControl || owner.Hp <= 0) return 0;
            var state = Edit();
            if (state.Ropes.Any(r => r.OwnerId == owner.Id))
            {
                owner.Edit().TakeoffHorizontalSpeed = owner.Read().HorizontalSpeed;
                state.Ropes = state.Ropes.Where(r => r.OwnerId != owner.Id).ToArray(); return 1;
            }
            if (state.Ropes.Length >= PhysicsNumbers.MaximumRopes || targetId == owner.Id) return 0;
            var snapshot = Capture();
            var from = snapshot.Bodies.SingleOrDefault(b => b.Id == owner.Id);
            var target = snapshot.Bodies.SingleOrDefault(b => b.Id == targetId);
            if (from == null || target == null) return 0;
            var hanging = Session.Layout.Physics.HangingRopes.SingleOrDefault(r => r.Contains(targetId));
            if (hanging != null)
            {
                var grip = HangingRopeGeometry.Closest(hanging, snapshot, from.X, from.Height + HangingRopeGeometry.HandHeight);
                if (grip.Distance > hanging.Reach || grip.BodyId != targetId || grip.Position < Math.Min(40, hanging.Length * .1f) ||
                    grip.Position > hanging.Length - 4) return 0;
                if (state.Ropes.Any(r => r.HangingRopeId == hanging.Id &&
                    Math.Abs(r.Position - grip.Position) < HangingRopeDefinition.GripSpacing)) return 0;
                state.Ropes = state.Ropes.Append(new PhysicsRopeState
                {
                    OwnerId = owner.Id, TargetId = grip.BodyId, HangingRopeId = hanging.Id,
                    Position = grip.Position, Length = 0
                }).ToArray();
                return 1;
            }
            float distance = PhysicsRopeGeometry.Distance(from, target, Session.Layout.Physics);
            if (distance > Session.Layout.Physics.LinkReach) return 0;
            state.Ropes = state.Ropes.Append(new PhysicsRopeState
                { OwnerId = owner.Id, TargetId = targetId,
                    Length = Math.Clamp(distance + 8, 4, Session.Layout.Physics.LinkMaximum) }).ToArray();
            return 1;
        }

        internal PhysicsWorldSnapshot Capture(bool forSave = false) => Enabled
            ? PhysicsSessionSnapshot.Capture(Session, Read(), forSave) : null;

        internal void Restore(PhysicsWorldSnapshot snapshot)
        {
            PhysicsSessionSnapshot.Restore(Session, this, snapshot);
            Invalidate();
        }

        internal static PhysicsRopeState[] LiveRopes(PhysicsRopeState[] ropes, IEnumerable<ActorBehaviour> actors,
            PhysicsBodyState[] props)
        {
            var ids = new HashSet<int>(actors.Where(a => a.Hp > 0 && a.Read().ManualControl).Select(a => a.Id)
                .Concat(props.Select(p => p.Id)));
            return ropes.Where(r => ids.Contains(r.OwnerId) && ids.Contains(r.TargetId)).ToArray();
        }

        public void Invalidate()
        {
            backend?.Dispose(); backend = null;
        }

        public override void OnDespawn()
        {
            Invalidate(); Trace.Recording = false; base.OnDespawn();
        }
    }
}
