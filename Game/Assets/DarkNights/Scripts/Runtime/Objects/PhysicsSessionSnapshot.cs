using System;
using System.Linq;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Physics;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 从各自状态所有者冻结关联物体检查点，恢复时在原事务内分别写回相应State。
    /// 保存副本撤除连接租约和输入序号，保留真实运动、起跳门闩与约束；原生接触缓存重新建立。
    /// </summary>
    internal static class PhysicsSessionSnapshot
    {
        internal static PhysicsWorldSnapshot Capture(ObjectSession world, PhysicsWorldState state, bool forSave)
        {
            var actors = world.Index.Actors.Where(a => a.Hp > 0 && a.Read().ManualControl).ToArray();
            var bodies = actors.Select(actor =>
            {
                ActorState a = actor.Read();
                return new PhysicsBodySnapshot(a.Id, a.X, a.Height, a.HorizontalSpeed, a.VerticalSpeed,
                    grounded: a.SupportPlatform >= 0, normalX: a.PhysicsGroundNormalX,
                    normalY: a.PhysicsGroundNormalY, takeoffSpeed: a.TakeoffHorizontalSpeed,
                    jumpBuffer: (float)a.JumpBufferRemaining, ascending: a.JumpAscending,
                    inputSequence: forSave ? 0 : a.LastInputSequence, controlLease: forSave ? 0 : a.ControlLease);
            }).Concat(state.Props.Select(p => new PhysicsBodySnapshot(p.Id, p.X, p.Height,
                p.VelocityX, p.VelocityY, p.Angle, p.AngularVelocity, p.Sleeping))).ToArray();
            var ropes = PhysicsSessionBehaviour.LiveRopes(state.Ropes, actors, state.Props)
                .Select(r => new PhysicsRopeSnapshot(r.OwnerId, r.TargetId, r.Length, r.HangingRopeId, r.Position)).ToArray();
            var map = world.Terrain?.Map;
            return new PhysicsWorldSnapshot(world.Layout.Physics.Identity, state.Tick,
                map?.World.WorldId.ToString().Replace("-", "") ?? "", map?.World.Epoch ?? 0,
                state.CollisionRevision, bodies, ropes);
        }

        internal static void Restore(ObjectSession world, PhysicsSessionBehaviour owner, PhysicsWorldSnapshot snapshot)
        {
            var actors = world.Index.Actors.Where(a => a.Hp > 0 && a.Read().ManualControl).ToArray();
            PhysicsSnapshotRules.Validate(snapshot, world.Layout.Physics, actors.Select(a => a.Id));
            if (snapshot == null) return;
            PhysicsWorldState state = owner.Edit();
            state.Tick = snapshot.Tick;
            state.CollisionRevision = world.Terrain?.Map?.CommitId ?? 0;
            state.Props = snapshot.Bodies.Where(b => b.Id < 0).Select(b => new PhysicsBodyState
            {
                Id = b.Id, X = b.X, Height = b.Height, VelocityX = b.VelocityX, VelocityY = b.VelocityY,
                Angle = b.Angle, AngularVelocity = b.AngularVelocity, Sleeping = b.Sleeping
            }).ToArray();
            state.Ropes = snapshot.Ropes.Select(r => new PhysicsRopeState
                { OwnerId = r.OwnerId, TargetId = r.TargetId, Length = r.Length, HangingRopeId = r.HangingRopeId, Position = r.Position }).ToArray();
            foreach (var actor in actors)
            {
                var body = snapshot.Bodies.Single(b => b.Id == actor.Id);
                ActorState a = actor.Edit();
                a.X = body.X; a.Height = body.Height; a.HorizontalSpeed = body.VelocityX; a.VerticalSpeed = body.VelocityY;
                a.TakeoffHorizontalSpeed = body.TakeoffSpeed;
                a.JumpBufferRemaining = body.JumpBuffer; a.JumpAscending = body.JumpAscending;
                a.SupportPlatform = body.Grounded ? 0 : -1;
                a.PhysicsGroundNormalX = body.GroundNormalX; a.PhysicsGroundNormalY = body.GroundNormalY;
            }
            world.Mutations.OnRollback(owner.Invalidate);
        }
    }
}
