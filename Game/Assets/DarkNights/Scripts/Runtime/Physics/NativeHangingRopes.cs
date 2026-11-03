using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Objects;
using UnityEngine;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 同一PhysicsScene2D中的环境绳节及人物抓附关节；仅为会话状态的执行资源。
    /// 攀爬移动抓点且保留绳总长，角色仍由既有刚体提交，退出和回滚随后端整体退休。
    /// </summary>
    internal sealed class NativeHangingRopes
    {
        private readonly NativePhysicsBodies bodies;
        private readonly PhysicsWorldDefinition rules;
        private readonly Dictionary<int, HingeJoint2D> grips = new Dictionary<int, HingeJoint2D>();
        private readonly HashSet<int> installed = new HashSet<int>();
        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
        private int[] collisionBodies = Array.Empty<int>();

        internal NativeHangingRopes(NativePhysicsBodies bodies, PhysicsWorldDefinition rules)
        { this.bodies = bodies; this.rules = rules; }

        internal void Prepare()
        {
            int[] bodyIds = bodies.All.Select(p => p.Value.GetInstanceID()).OrderBy(id => id).ToArray();
            bool collisionChanged = !collisionBodies.SequenceEqual(bodyIds);
            foreach (var rope in rules.HangingRopes)
            {
                bool create = !installed.Contains(rope.Id);
                for (int i = 0; i < rope.Count; i++)
                {
                    int id = rope.FirstBodyId - i;
                    var body = bodies[id];
                    if (create)
                    {
                        body.angularDamping = rope.AngularDamping;
                        var joint = body.gameObject.AddComponent<HingeJoint2D>();
                        joint.autoConfigureConnectedAnchor = false;
                        joint.anchor = new Vector2(0, rope.SegmentLength / 100);
                        joint.connectedBody = i == 0 ? null : bodies[id + 1];
                        joint.connectedAnchor = i == 0 ? new Vector2(rope.AnchorX, rope.AnchorHeight) / 100 : Vector2.zero;
                        joint.enableCollision = false;
                    }
                    ApplyTangentialDamping(body, rope);
                    if (!create && !collisionChanged) continue;
                    foreach (var other in bodies.All)
                        if (other.Key != id)
                            Physics2D.IgnoreCollision(body.GetComponent<Collider2D>(), other.Value.GetComponent<Collider2D>());
                }
                installed.Add(rope.Id);
            }
            collisionBodies = bodyIds;
        }

        internal bool Apply(ActorBehaviour actor, ActorMotionInput input, PhysicsWorldState state)
        {
            int index = Array.FindIndex(state.Ropes, r => r.OwnerId == actor.Id && r.HangingRopeId != 0);
            if (index < 0) { Remove(actor.Id); return false; }
            if (input.JumpPressed)
            {
                state.Ropes = state.Ropes.Where(r => r.OwnerId != actor.Id).ToArray();
                actor.Edit().TakeoffHorizontalSpeed = bodies[actor.Id].linearVelocity.x * 100;
                Remove(actor.Id); HeroJumpMotion.Clear(actor.Edit(), clearVelocity: false); return false;
            }
            var link = state.Ropes[index];
            var rope = rules.HangingRopes.Single(r => r.Id == link.HangingRopeId);
            Rigidbody2D body = bodies[actor.Id];
            float next = Math.Clamp(link.Position - input.Climb * rope.ClimbSpeed * (float)input.DeltaSeconds,
                Math.Min(40, rope.Length * .1f), rope.Length - 4);
            foreach (var other in state.Ropes.Where(r => r.OwnerId != actor.Id && r.HangingRopeId == rope.Id))
            {
                if (other.Position > link.Position) next = Math.Min(next, other.Position - HangingRopeDefinition.GripSpacing);
                else next = Math.Max(next, other.Position + HangingRopeDefinition.GripSpacing);
            }
            var current = HangingRopeGeometry.Locate(rope, link.Position);
            var proposed = HangingRopeGeometry.Locate(rope, next);
            Vector2 before = Point(bodies[current.BodyId], current.LocalHeight);
            Vector2 after = Point(bodies[proposed.BodyId], proposed.LocalHeight);
            Vector2 delta = after - before;
            if (delta.sqrMagnitude > .0000001f)
            {
                var filter = new ContactFilter2D(); filter.NoFilter(); filter.useTriggers = false;
                int count = body.Cast(delta.normalized, filter, hits, delta.magnitude + .01f);
                float fraction = count == hits.Length ? 0 : 1;
                for (int i = 0; i < count; i++)
                {
                    int other = bodies.Identity(hits[i].rigidbody);
                    if (rules.HangingRopes.Any(r => r.Contains(other))) continue;
                    if (Vector2.Dot(hits[i].normal, delta) >= -.00001f) continue;
                    fraction = Math.Min(fraction, Math.Max(0, hits[i].distance - .01f) / delta.magnitude);
                }
                next = link.Position + (next - link.Position) * fraction;
            }
            var target = HangingRopeGeometry.Locate(rope, next);
            link.Position = next; link.TargetId = target.BodyId; state.Ropes[index] = link;
            if (!grips.TryGetValue(actor.Id, out HingeJoint2D grip) || grip == null)
            {
                grip = body.gameObject.AddComponent<HingeJoint2D>(); grips[actor.Id] = grip;
                grip.autoConfigureConnectedAnchor = false;
                grip.anchor = new Vector2(0, HangingRopeGeometry.HandHeight / 100);
                grip.enableCollision = false;
            }
            grip.connectedBody = bodies[target.BodyId];
            grip.connectedAnchor = new Vector2(0, target.LocalHeight / 100);
            var a = actor.Edit();
            HeroJumpMotion.Clear(a, clearVelocity: false);
            a.TakeoffHorizontalSpeed = body.linearVelocity.x * 100;
            a.SupportPlatform = -1;
            float threshold = rope.SwingSpeedThreshold / 100;
            float projectedSpeed = body.linearVelocity.x * input.Direction;
            float gain = Mathf.Clamp01((threshold - projectedSpeed) / (threshold * .25f));
            float assist = input.Direction * rope.SwingAcceleration * gain;
            ApplyTangentialDamping(body, rope);
            body.AddForce(new Vector2(assist, -actor.World.Catalog.Balance.HeroControl.Gravity) * body.mass / 100);
            return true;
        }

        private static void ApplyTangentialDamping(Rigidbody2D body, HangingRopeDefinition rope)
        {
            Vector2 radial = body.worldCenterOfMass - new Vector2(rope.AnchorX, rope.AnchorHeight) / 100;
            Vector2 tangent = new Vector2(-radial.y, radial.x).normalized;
            float speed = Vector2.Dot(body.linearVelocity, tangent);
            body.AddForce(-tangent * (speed * rope.TangentialDamping * body.mass), ForceMode2D.Force);
        }

        private void Remove(int actor)
        {
            if (!grips.TryGetValue(actor, out HingeJoint2D grip)) return;
            if (grip != null) NativePhysicsBodies.Retire(grip);
            grips.Remove(actor);
        }

        private static Vector2 Point(Rigidbody2D body, float localHeight)
        {
            float angle = body.rotation * Mathf.Deg2Rad;
            return body.position + new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * (localHeight / 100);
        }

        internal void Clean(PhysicsRopeState[] links)
        {
            foreach (int actor in grips.Keys.Where(id => !bodies.Contains(id) ||
                !links.Any(r => r.OwnerId == id && r.HangingRopeId != 0)).ToArray()) Remove(actor);
        }
    }
}
