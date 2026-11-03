using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 会话浮点2D物理的唯一推进器；所有角色先提交意图，统一模拟一次，再提交角色和道具结果。
    /// 原生场景不参与Unity自动模拟，失败或加载后整体退休执行缓存，由YYGC状态恢复。
    /// </summary>
    internal sealed class UnityPhysicsBackend : IDisposable
    {
        private readonly ObjectSession world;
        private readonly Scene scene;
        private readonly PhysicsScene2D physics;
        private readonly NativePhysicsBodies bodies;
        private readonly NativePhysicsTerrain terrain;
        private NativeHangingRopes hanging;
        private readonly Dictionary<int, (ActorMotionInput Input, MovementDefinition Rules)> inputs =
            new Dictionary<int, (ActorMotionInput, MovementDefinition)>();
        private bool collecting;

        internal UnityPhysicsBackend(ObjectSession world)
        {
            this.world = world;
            scene = SceneManager.CreateScene("Dark Nights Physics " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
            bodies = new NativePhysicsBodies(scene, world.Layout.Physics);
            terrain = new NativePhysicsTerrain(scene, world.Layout.Physics.PropFriction);
        }

        internal void Begin(IReadOnlyList<ActorBehaviour> actors, PhysicsWorldState state)
        {
            if (collecting) throw new InvalidOperationException("物理批次不能嵌套。");
            if (actors.Count + state.Props.Length > PhysicsNumbers.MaximumBodies)
                throw new InvalidOperationException("物理活动对象超过上限。");
            terrain.Synchronize(world.Terrain?.Map, world.Layout, world.Catalog.Balance.HeroControl.MaximumHeight);
            state.CollisionRevision = world.Terrain?.Map?.CommitId ?? 0;
            bodies.Prepare(actors, state);
            inputs.Clear(); collecting = true;
        }

        internal void Submit(ActorMotionInput input, MovementDefinition rules)
        {
            if (!collecting || input.DeltaSeconds <= 0 || input.DeltaSeconds > 1.0 / 15 ||
                input.FlightHeight.HasValue || !bodies.Contains(input.ActorId) || inputs.ContainsKey(input.ActorId))
                throw new InvalidOperationException("物理输入须属于当前唯一批次，且不接受旧飞行运动。");
            inputs.Add(input.ActorId, (input, rules));
        }
        internal ActorMotionInput[] FreezeInputs() => inputs.Values.Select(i => i.Input).OrderBy(i => i.ActorId).ToArray();
        internal PhysicsContactSample[] Contacts(out bool truncated)
        {
            var points = new ContactPoint2D[32];
            var result = new List<PhysicsContactSample>();
            truncated = false;
            foreach (var pair in bodies.All.OrderBy(p => p.Key))
            {
                int count = pair.Value.GetContacts(points);
                if (count == points.Length) truncated = true;
                for (int i = 0; i < count; i++)
                {
                    var point = points[i];
                    int other = bodies.Identity(point.rigidbody == pair.Value ? point.otherRigidbody : point.rigidbody);
                    if (other != 0 && pair.Key > other) continue;
                    if (result.Count == 256) { truncated = true; continue; }
                    result.Add(new PhysicsContactSample(pair.Key, other, point.point.x * 100, point.point.y * 100,
                        point.normal.x, point.normal.y, point.normalImpulse));
                }
            }
            return result.ToArray();
        }

        internal void Solve(IReadOnlyList<ActorBehaviour> actors, PhysicsWorldState state, double delta)
        {
            if (!collecting || inputs.Count != actors.Count) throw new InvalidOperationException("物理角色意图不完整。");
            state.Ropes = PhysicsSessionBehaviour.LiveRopes(state.Ropes, actors, state.Props);
            if (hanging == null) hanging = new NativeHangingRopes(bodies, world.Layout.Physics);
            hanging.Prepare();
            hanging.Clean(state.Ropes);
            bodies.SetRopes(state.Ropes);
            foreach (var actor in actors)
            {
                var input = inputs[actor.Id];
                if (!hanging.Apply(actor, input.Input, state))
                    NativeActorMotor.Apply(world, actor, input.Rules, input.Input, bodies[actor.Id]);
            }
            foreach (var prop in state.Props)
            {
                Rigidbody2D body = bodies[prop.Id];
                body.AddForce(Vector2.down * (world.Catalog.Balance.HeroControl.Gravity * body.mass /
                    PhysicsNumbers.PixelsPerUnit), ForceMode2D.Force);
            }
            if (!physics.Simulate((float)delta)) throw new InvalidOperationException("会话物理场景未执行模拟。");
            foreach (var actor in actors) NativeActorMotor.Commit(actor, bodies[actor.Id]);
            var props = new PhysicsBodyState[state.Props.Length];
            for (int i = 0; i < props.Length; i++)
            {
                Rigidbody2D body = bodies[state.Props[i].Id];
                Vector2 p = body.position * 100, v = body.linearVelocity * 100;
                props[i] = new PhysicsBodyState { Id = state.Props[i].Id, X = p.x, Height = p.y,
                    VelocityX = v.x, VelocityY = v.y, Angle = Mathf.DeltaAngle(0, body.rotation), AngularVelocity = body.angularVelocity,
                    Sleeping = body.IsSleeping() };
            }
            state.Props = props; state.Tick = checked(state.Tick + 1);
            foreach (var pair in bodies.All) bodies.MarkCommitted(pair.Key);
            collecting = false; inputs.Clear();
        }

        public void Dispose()
        {
            collecting = false; inputs.Clear(); bodies.Dispose(); terrain.Dispose();
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
