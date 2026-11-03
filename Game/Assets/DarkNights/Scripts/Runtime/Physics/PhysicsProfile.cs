using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DarkNights.Core.Physics;
using UnityEngine;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 会话物理候选的规则与显示引用；位置尺寸由原生Scene提供，开局合并冻结，不在运行中切换数值域。
    /// 逻辑长度为像素、质量为kg、时间为秒；表现Prefab只展示，不携带权威刚体。
    /// </summary>
    public sealed class PhysicsProfile : ScriptableObject
    {
        public float GroundAcceleration = 2400;
        public float GroundBraking = 3600;
        public float ActorMass = 1;
        public float PropFriction = .6f;
        public float LinkReach = 112;
        public float LinkMaximum = 192;
        public SpriteRenderer PropViewPrefab;
        public LineRenderer RopeViewPrefab;
        public float PropMass = 2;

        public PhysicsWorldDefinition Freeze(IReadOnlyList<PhysicsPropDefinition> placements = null, IReadOnlyList<HangingRopeDefinition> hangingRopes = null)
        {
            if (PropViewPrefab == null || RopeViewPrefab == null)
                throw new InvalidOperationException("物理候选缺少显式表现绑定。");
            if (placements != null && (placements.Count > 32 || placements.Any(p => p == null)))
                throw new InvalidOperationException("物理道具放置超限或缺失。");
            var props = (placements ?? Array.Empty<PhysicsPropDefinition>()).OrderBy(p => p.Id).ToArray();
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes);
            writer.Write(PhysicsWorldDefinition.Backend);
            writer.Write("foot-box-16x44;terrain-shapes-v1;map-top-boundary-v2;hz60;contacts-v1;units100");
            writer.Write(Application.unityVersion);
            writer.Write(Physics2D.velocityIterations); writer.Write(Physics2D.positionIterations);
            writer.Write(Physics2D.defaultContactOffset); writer.Write(Physics2D.baumgarteScale);
            writer.Write(Physics2D.baumgarteTOIScale); writer.Write(Physics2D.maxTranslationSpeed);
            writer.Write(Physics2D.maxRotationSpeed); writer.Write(Physics2D.GetIgnoreLayerCollision(0, 0));
            writer.Write(GroundAcceleration); writer.Write(GroundBraking); writer.Write(ActorMass);
            writer.Write(PropFriction); writer.Write(LinkReach); writer.Write(LinkMaximum);
            writer.Write(props.Length);
            foreach (var prop in props)
            {
                writer.Write(prop.Id); writer.Write(prop.X); writer.Write(prop.Height);
                writer.Write(prop.Width); writer.Write(prop.BodyHeight); writer.Write(prop.Mass);
            }
            var chains = (hangingRopes ?? Array.Empty<HangingRopeDefinition>()).OrderBy(r => r.Id).ToArray();
            writer.Write(chains.Length);
            if (chains.Length != 0) writer.Write("hanging-chain-v1;grip52;hand34;damping0.08;reach-cast32;swing-cap600");
            foreach (var rope in chains)
            {
                writer.Write(rope.Id); writer.Write(rope.FirstBodyId); writer.Write(rope.Count);
                writer.Write(rope.AnchorX); writer.Write(rope.AnchorHeight); writer.Write(rope.SegmentLength);
                writer.Write(rope.Mass); writer.Write(rope.Reach); writer.Write(rope.ClimbSpeed); writer.Write(rope.SwingAcceleration);
            }
            writer.Flush();
            using var sha = SHA256.Create();
            string hash = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            return new PhysicsWorldDefinition(hash, GroundAcceleration, GroundBraking, ActorMass,
                PropFriction, LinkReach, LinkMaximum, props, chains);
        }
    }
}
