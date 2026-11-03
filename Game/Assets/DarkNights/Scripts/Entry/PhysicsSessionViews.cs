using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Physics;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Physics;
using DarkNights.Runtime.Objects;
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
        private readonly Dictionary<int, (HangingRopeView View, HangingRopeConfig Config)> hanging =
            new Dictionary<int, (HangingRopeView, HangingRopeConfig)>();

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
                if (!hanging.TryGetValue(rope.Id, out var installed))
                {
                    var created = network.ObjectResources.CreateRopeView(rope, parent);
                    try
                    {
                        var visual = created as HangingRopeView ??
                            throw new InvalidOperationException("吊绳定义需要HangingRopeView主视图。");
                        var behaviour = visual.Owner.GetBehaviour<HangingRopeBehaviour>() ??
                            throw new InvalidOperationException("吊绳视图缺少配置能力。");
                        behaviour.Bind(rope);
                        var config = behaviour.Configuration;
                        visual.Configure(new Vector3(rope.AnchorX, rope.AnchorHeight, 0) / 100,
                            config.LineWidth, config.CornerVertices, config.CapVertices);
                        visual.Owner.Activate();
                        installed = (visual, config);
                        hanging.Add(rope.Id, installed);
                    }
                    catch { network.ObjectResources.ReleaseRopeView(created); throw; }
                }
                DrawHangingRope(installed.View.Line, snapshot, rope, installed.Config);
            }
        }

        private void DrawHangingRope(LineRenderer visual, PhysicsWorldSnapshot snapshot,
            HangingRopeDefinition rope, HangingRopeConfig config)
        {
            int samplesPerSegment = config.SamplesPerSegment;
            float maximumOffset = config.MaximumCurveOffset;
            visual.positionCount = rope.Count * samplesPerSegment + 1;
            Vector3 current = new Vector3(rope.AnchorX / 100, rope.AnchorHeight / 100, -.01f);
            Vector3 previous = current;
            Vector3 next = RopePoint(snapshot, rope.FirstBodyId, 0);
            for (int i = 0; i < rope.Count; i++)
            {
                Vector3 following = i + 1 < rope.Count ? RopePoint(snapshot, rope.FirstBodyId - i - 1, 0) : next;
                for (int sample = 0; sample < samplesPerSegment; sample++)
                {
                    float t = sample / (float)samplesPerSegment;
                    Vector3 linear = Vector3.Lerp(current, next, t);
                    Vector3 curve = .5f * (2 * current + (-previous + next) * t +
                        (2 * previous - 5 * current + 4 * next - following) * (t * t) +
                        (-previous + 3 * current - 3 * next + following) * (t * t * t));
                    visual.SetPosition(i * samplesPerSegment + sample,
                        linear + Vector3.ClampMagnitude(curve - linear, maximumOffset));
                }
                previous = current; current = next; next = following;
            }
            visual.SetPosition(rope.Count * samplesPerSegment, current);
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
            foreach (var item in hanging.Values) network.ObjectResources.ReleaseRopeView(item.View);
            props.Clear(); ropes.Clear(); hanging.Clear(); epoch = -1;
        }
        private void OnDestroy() => Clear();
    }
}
