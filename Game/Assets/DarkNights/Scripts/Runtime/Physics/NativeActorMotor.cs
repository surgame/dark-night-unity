using System;
using DarkNights.Core.Config;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Objects;
using UnityEngine;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 目标速度转有界力的唯一原生适配；普通行走施力，跳跃施一次冲量，重力与早松键沿既有规则。
    /// 接触决定支撑，马达不设置Transform；状态只在会话写事务中准备及提交。
    /// </summary>
    internal static class NativeActorMotor
    {
        private const float Scale = PhysicsNumbers.PixelsPerUnit;
        private static readonly RaycastHit2D[] Supports = new RaycastHit2D[32];

        internal static bool Ground(Rigidbody2D body, out Vector2 normal, out Vector2 supportVelocity)
        {
            normal = Vector2.up; supportVelocity = Vector2.zero;
            var filter = new ContactFilter2D(); filter.NoFilter(); filter.useTriggers = false;
            int count = body.Cast(Vector2.down, filter, Supports, Physics2D.defaultContactOffset * 2 + .001f);
            bool found = false; float best = .5f;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D contact = Supports[i];
                if (contact.normal.y <= best) continue;
                if (contact.point.y > body.position.y + .08f) continue;
                best = contact.normal.y; normal = contact.normal;
                Rigidbody2D other = contact.rigidbody;
                supportVelocity = other != null ? other.GetPointVelocity(contact.point) * Scale : Vector2.zero;
                found = true;
            }
            return found;
        }

        internal static void Apply(ObjectSession world, ActorBehaviour actor, MovementDefinition movement,
            ActorMotionInput input, Rigidbody2D body)
        {
            ActorState state = actor.Edit();
            var rules = world.Catalog.Balance.HeroControl;
            float seconds = (float)input.DeltaSeconds;
            Vector2 velocity = body.linearVelocity * Scale;
            bool grounded = Ground(body, out Vector2 normal, out Vector2 support) &&
                Vector2.Dot(velocity - support, normal) <= 1;
            state.HorizontalSpeed = grounded ? velocity.x - support.x : velocity.x;
            state.VerticalSpeed = velocity.y;
            MovementHorizontalMotion.Prepare(state, rules, movement, grounded, seconds,
                world.DebugHeroSpeedMultiplier, input.Direction, input.NormalSpeed);
            float target = state.HorizontalSpeed + (grounded ? support.x : 0);
            if (input.GoalX.HasValue && grounded && Math.Abs(input.GoalX.Value - state.X) < Math.Abs(target * seconds))
                target = (input.GoalX.Value - state.X) / seconds;
            float limit = input.Direction == 0 ? world.Layout.Physics.GroundBraking : world.Layout.Physics.GroundAcceleration;
            float acceleration = Math.Clamp((target - velocity.x) / seconds, -limit, limit);
            state.SupportPlatform = grounded ? 0 : -1;
            if (movement.CanJump && HeroJumpMotion.TryStart(state, rules, grounded))
            {
                state.TakeoffHorizontalSpeed = velocity.x;
                state.VerticalSpeed += support.y;
                body.AddForce(Vector2.up * ((state.VerticalSpeed - velocity.y) * body.mass / Scale), ForceMode2D.Impulse);
                grounded = false; velocity.y = state.VerticalSpeed;
            }
            // 接地力沿真实支撑切线，避免下坡时只向前加速而离开坡面；起跳后恢复空中水平施力。
            Vector2 drive = grounded ? new Vector2(1, -normal.x / normal.y) : Vector2.right;
            body.AddForce(drive * (acceleration * body.mass / Scale), ForceMode2D.Force);
            state.VerticalSpeed = velocity.y;
            HeroJumpMotion.Accelerate(state, rules, seconds, input.JumpHeld, jetpack: false);
            float vertical = (state.VerticalSpeed - velocity.y) / seconds;
            // 地面也保留重力以维持真实接触，坡面切向由求解器约束；不把支撑位置吸回格子。
            body.AddForce(Vector2.up * (vertical * body.mass / Scale), ForceMode2D.Force);
            state.HorizontalSpeed = velocity.x;
        }

        internal static void Commit(ActorBehaviour actor, Rigidbody2D body)
        {
            ActorState state = actor.Edit();
            float previous = state.X;
            state.X = body.position.x * Scale; state.Height = body.position.y * Scale;
            state.HorizontalSpeed = body.linearVelocity.x * Scale; state.VerticalSpeed = body.linearVelocity.y * Scale;
            bool grounded = Ground(body, out Vector2 normal, out Vector2 support) &&
                Vector2.Dot(body.linearVelocity * Scale - support, normal) <= 1;
            state.PhysicsGroundNormalX = normal.x; state.PhysicsGroundNormalY = normal.y;
            state.SupportPlatform = grounded ? 0 : -1;
            if (grounded) { state.JumpAscending = false; state.TakeoffHorizontalSpeed = state.HorizontalSpeed; }
            else if (state.VerticalSpeed <= 0) state.JumpAscending = false;
            state.Walking = Math.Abs(state.X - previous) > .001f;
        }
    }
}
