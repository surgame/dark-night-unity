using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Physics;
using DarkNights.View;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 合作物体的会话只读表现；与角色共享相同插值时间线，绳端跟随同一帧的显示位置。
    /// 换连接／epoch时整体撤除；客户端不创建业务刚体，不从表现Transform反馈权威位置。
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class PhysicsSessionViews : MonoBehaviour
    {
        private SessionNetwork network;
        private PhysicsWorldDefinition rules;
        private SpriteRenderer propTemplate;
        private LineRenderer ropeTemplate;
        private SessionEntityViews entities;
        private Transform parent;
        private int epoch = -1;
        private long connection;
        private readonly Dictionary<int, SpriteRenderer> props = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, LineRenderer> ropes = new Dictionary<int, LineRenderer>();
        private readonly Dictionary<int, LineRenderer> hanging = new Dictionary<int, LineRenderer>();

        public void Initialize(SessionNetwork network, PhysicsProfile profile, PhysicsWorldDefinition rules,
            PinewatchStage stage, SessionEntityViews entities)
        {
            this.network = network; this.rules = rules; this.entities = entities;
            parent = stage.RuntimeGroup("Cooperative Physics");
            if (profile.Freeze(rules.Props, rules.HangingRopes).Identity != rules.Identity) throw new InvalidOperationException("物理配置在会话装配期间变化。");
            propTemplate = profile.PropViewPrefab; ropeTemplate = profile.RopeViewPrefab;
        }

        private void LateUpdate()
        {
            var frame = network?.Client?.Replica.Current;
            if (frame == null || !network.Client.Ready)
            { Clear(); return; }
            if (epoch != frame.Epoch || connection != network.Client.ConnectionGeneration)
            { Clear(); epoch = frame.Epoch; connection = network.Client.ConnectionGeneration; }
            var snapshot = frame.World.Physics;
            if (snapshot == null) { Clear(); return; }
            foreach (var body in snapshot.Bodies.Where(b => b.Id < 0 && !rules.HangingRopes.Any(r => r.Contains(b.Id))))
            {
                if (!props.TryGetValue(body.Id, out SpriteRenderer visual))
                {
                    visual = Instantiate(propTemplate, parent); visual.name = "Cooperative box " + body.Id;
                    var size = rules.Props.Single(p => p.Id == body.Id);
                    float width = size.Width, height = size.BodyHeight;
                    visual.transform.localScale = new Vector3(width / (100 * visual.sprite.bounds.size.x),
                        height / (100 * visual.sprite.bounds.size.y), 1);
                    props.Add(body.Id, visual);
                }
                var shown = entities.SamplePhysics(body);
                visual.transform.SetPositionAndRotation(new Vector3(shown.X / 100, shown.Height / 100, 0),
                    Quaternion.Euler(0, 0, shown.Angle));
            }
            foreach (int id in ropes.Keys.Where(id => snapshot.Ropes.All(r => r.OwnerId != id)).ToArray())
            { Destroy(ropes[id].gameObject); ropes.Remove(id); }
            foreach (var link in snapshot.Ropes)
            {
                if (!ropes.TryGetValue(link.OwnerId, out LineRenderer visual))
                {
                    visual = Instantiate(ropeTemplate, parent); ropes.Add(link.OwnerId, visual);
                }
                visual.useWorldSpace = true; visual.positionCount = 2;
                visual.SetPosition(0, Anchor(snapshot, link.OwnerId));
                if (link.HangingRopeId == 0) visual.SetPosition(1, Anchor(snapshot, link.TargetId));
                else
                {
                    var rope = rules.HangingRopes.Single(r => r.Id == link.HangingRopeId);
                    var grip = HangingRopeGeometry.Locate(rope, link.Position);
                    visual.SetPosition(1, RopePoint(snapshot, grip.BodyId, grip.LocalHeight));
                    visual.SetPosition(0, RopePoint(snapshot, link.OwnerId, HangingRopeGeometry.HandHeight));
                }
            }
            foreach (var rope in rules.HangingRopes)
            {
                if (!props.ContainsKey(rope.Id))
                {
                    var anchor = Instantiate(propTemplate, parent); anchor.name = "固定吊绳锚点 " + rope.Id;
                    anchor.transform.localScale = new Vector3(.16f / anchor.sprite.bounds.size.x,
                        .16f / anchor.sprite.bounds.size.y, 1);
                    anchor.transform.position = new Vector3(rope.AnchorX / 100, rope.AnchorHeight / 100, -.01f);
                    props.Add(rope.Id, anchor);
                }
                if (!hanging.TryGetValue(rope.Id, out LineRenderer visual))
                { visual = Instantiate(ropeTemplate, parent); hanging.Add(rope.Id, visual); }
                visual.useWorldSpace = true;
                visual.positionCount = rope.Count + 1;
                visual.SetPosition(0, new Vector3(rope.AnchorX / 100, rope.AnchorHeight / 100, -.01f));
                for (int i = 0; i < rope.Count; i++)
                    visual.SetPosition(i + 1, RopePoint(snapshot, rope.FirstBodyId - i, 0));
            }
        }

        private Vector3 RopePoint(PhysicsWorldSnapshot snapshot, int id, float localHeight)
        {
            var body = entities.SamplePhysics(snapshot.Bodies.Single(b => b.Id == id));
            float angle = body.Angle * Mathf.Deg2Rad;
            return new Vector3((body.X - Mathf.Sin(angle) * localHeight) / 100,
                (body.Height + Mathf.Cos(angle) * localHeight) / 100, -.01f);
        }

        private Vector3 Anchor(PhysicsWorldSnapshot snapshot, int id)
        {
            var body = snapshot.Bodies.Single(b => b.Id == id);
            var shown = entities.SamplePhysics(body);
            var anchor = PhysicsRopeGeometry.Anchor(id, shown.X, shown.Height, shown.Angle, rules);
            return new Vector3(anchor.X / 100, anchor.Height / 100, -.01f);
        }

        private void Clear()
        {
            foreach (var view in props.Values) if (view != null) Destroy(view.gameObject);
            foreach (var view in ropes.Values) if (view != null) Destroy(view.gameObject);
            foreach (var view in hanging.Values) if (view != null) Destroy(view.gameObject);
            props.Clear(); ropes.Clear(); hanging.Clear(); epoch = -1;
        }
        private void OnDestroy() => Clear();
    }
}
