using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Objects;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 单次会话的原生执行资源，身份引用来自YYGC状态；不保存业务、输入或支付结果。
    /// 每个物体一个物理根，显式恢复才重设姿态，表现对象不挂在这些根上。
    /// </summary>
    internal sealed class NativePhysicsBodies : IDisposable
    {
        private readonly Scene scene;
        private readonly PhysicsWorldDefinition rules;
        private readonly Dictionary<int, Rigidbody2D> bodies = new Dictionary<int, Rigidbody2D>();
        private readonly Dictionary<int, Vector4> committed = new Dictionary<int, Vector4>();
        private readonly Dictionary<int, DistanceJoint2D> ropes = new Dictionary<int, DistanceJoint2D>();
        private readonly PhysicsMaterial2D actorMaterial, propMaterial;
        internal IEnumerable<KeyValuePair<int, Rigidbody2D>> All => bodies;
        internal Rigidbody2D this[int id] => bodies[id];
        internal bool Contains(int id) => bodies.ContainsKey(id);
        internal int Identity(Rigidbody2D body) => body == null ? 0 :
            bodies.FirstOrDefault(pair => pair.Value == body).Key;

        internal NativePhysicsBodies(Scene scene, PhysicsWorldDefinition rules)
        {
            this.scene = scene; this.rules = rules;
            actorMaterial = new PhysicsMaterial2D("Actor motor") { friction = 0, bounciness = 0 };
            propMaterial = new PhysicsMaterial2D("Cooperative props") { friction = rules.PropFriction, bounciness = 0 };
        }

        internal void Prepare(IReadOnlyList<ActorBehaviour> actors, PhysicsWorldState state)
        {
            var ids = new HashSet<int>(actors.Select(a => a.Id).Concat(state.Props.Select(p => p.Id)));
            foreach (int id in bodies.Keys.Where(id => !ids.Contains(id)).ToArray())
            { Retire(bodies[id].gameObject); bodies.Remove(id); committed.Remove(id); }
            foreach (var actor in actors)
            {
                ActorState value = actor.Read();
                if (!bodies.ContainsKey(actor.Id))
                    Create(actor.Id, 16, 44, rules.ActorMass, true);
                RestoreIfChanged(actor.Id, value.X, value.Height, value.HorizontalSpeed, value.VerticalSpeed);
            }
            foreach (var prop in state.Props)
            {
                if (!bodies.ContainsKey(prop.Id))
                {
                    var definition = rules.Props.Single(p => p.Id == prop.Id);
                    Create(prop.Id, definition.Width, definition.BodyHeight, definition.Mass, false);
                    bodies[prop.Id].rotation = prop.Angle;
                    bodies[prop.Id].angularVelocity = prop.AngularVelocity;
                }
                RestoreIfChanged(prop.Id, prop.X, prop.Height, prop.VelocityX, prop.VelocityY);
                if (prop.Sleeping) bodies[prop.Id].Sleep();
            }
        }

        private void Create(int id, float width, float height, float mass, bool actor)
        {
            var root = new GameObject("Physics body " + id) { hideFlags = HideFlags.HideInHierarchy };
            SceneManager.MoveGameObjectToScene(root, scene);
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic; body.mass = mass; body.gravityScale = 0;
            body.interpolation = RigidbodyInterpolation2D.None; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearDamping = 0; body.angularDamping = actor ? 0 : .05f;
            if (actor) body.constraints = RigidbodyConstraints2D.FreezeRotation;
            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(width, height) / PhysicsNumbers.PixelsPerUnit;
            collider.offset = new Vector2(0, height * .5f / PhysicsNumbers.PixelsPerUnit);
            collider.sharedMaterial = actor ? actorMaterial : propMaterial;
            bodies.Add(id, body);
        }

        private void RestoreIfChanged(int id, float x, float height, float vx, float vy)
        {
            var desired = new Vector4(x, height, vx, vy);
            if (committed.TryGetValue(id, out Vector4 last) && last == desired) return;
            Rigidbody2D body = bodies[id];
            body.position = new Vector2(x, height) / PhysicsNumbers.PixelsPerUnit;
            body.linearVelocity = new Vector2(vx, vy) / PhysicsNumbers.PixelsPerUnit;
            committed[id] = desired;
        }

        internal void MarkCommitted(int id)
        {
            Rigidbody2D body = bodies[id];
            committed[id] = new Vector4(body.position.x, body.position.y,
                body.linearVelocity.x, body.linearVelocity.y) * PhysicsNumbers.PixelsPerUnit;
        }

        internal void SetRopes(PhysicsRopeState[] links)
        {
            var active = new HashSet<int>(links.Where(r => r.HangingRopeId == 0).Select(r => r.OwnerId));
            foreach (int id in ropes.Keys.Where(id => !active.Contains(id) || !Contains(id)).ToArray())
            { if (ropes[id] != null) Retire(ropes[id]); ropes.Remove(id); }
            foreach (var link in links.Where(r => r.HangingRopeId == 0))
            {
                if (!Contains(link.OwnerId) || !Contains(link.TargetId)) continue;
                if (!ropes.TryGetValue(link.OwnerId, out DistanceJoint2D joint) || joint == null)
                {
                    joint = bodies[link.OwnerId].gameObject.AddComponent<DistanceJoint2D>();
                    ropes[link.OwnerId] = joint;
                }
                joint.autoConfigureConnectedAnchor = false; joint.autoConfigureDistance = false;
                joint.anchor = new Vector2(0, .22f);
                joint.connectedBody = bodies[link.TargetId];
                joint.connectedAnchor = new Vector2(0, link.TargetId > 0 ? .22f :
                    rules.Props.Single(p => p.Id == link.TargetId).BodyHeight / 200);
                joint.distance = link.Length / PhysicsNumbers.PixelsPerUnit;
                joint.maxDistanceOnly = true; joint.enableCollision = true;
            }
        }

        internal static void Retire(UnityEngine.Object value)
        {
            if (value is GameObject root) root.SetActive(false);
            if (value is Joint2D joint) joint.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            foreach (var body in bodies.Values) if (body != null) Retire(body.gameObject);
            bodies.Clear(); ropes.Clear(); committed.Clear();
            Retire(actorMaterial); Retire(propMaterial);
        }
    }
}
