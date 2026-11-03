using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Physics
{
    /// <summary>会话物理后端的冻结规则与作者道具；不依赖Unity，不保存本局姿态，规则身份覆盖全部求解参数。</summary>
    public sealed class PhysicsWorldDefinition
    {
        public const string Backend = "unity-2d-float-v1";
        public string Identity { get; }
        public float GroundAcceleration { get; }
        public float GroundBraking { get; }
        public float ActorMass { get; }
        public float PropFriction { get; }
        public float LinkReach { get; }
        public float LinkMaximum { get; }
        public IReadOnlyList<PhysicsPropDefinition> Props { get; }
        public IReadOnlyList<HangingRopeDefinition> HangingRopes { get; }
        public PhysicsWorldDefinition(string identity, float acceleration, float braking, float actorMass,
            float friction, float linkReach, float linkMaximum, IReadOnlyList<PhysicsPropDefinition> props,
            IReadOnlyList<HangingRopeDefinition> hangingRopes = null)
        {
            if (identity == null || identity.Length != 64 || identity.Any(c => !Uri.IsHexDigit(c)))
                throw new ArgumentException("Physics identity must be SHA-256.");
            PhysicsNumbers.Require(acceleration, 1, 64000);
            PhysicsNumbers.Require(braking, 1, 64000);
            PhysicsNumbers.Require(actorMass, .1f, 100);
            PhysicsNumbers.Require(friction, 0, 2);
            PhysicsNumbers.Require(linkReach, 8, 256);
            PhysicsNumbers.Require(linkMaximum, linkReach, 512);
            if (props == null || props.Count > 32 || props.Any(p => p == null) ||
                props.Select(p => p.Id).Distinct().Count() != props.Count)
                throw new ArgumentException("Physics prop identities are missing or duplicated.");
            Identity = identity; GroundAcceleration = acceleration; GroundBraking = braking;
            ActorMass = actorMass; PropFriction = friction; LinkReach = linkReach; LinkMaximum = linkMaximum;
            Props = Array.AsReadOnly(props.OrderBy(p => p.Id).ToArray());
            var chains = (hangingRopes ?? Array.Empty<HangingRopeDefinition>()).ToArray();
            if (chains.Length > 8 || chains.Any(r => r == null) || chains.Select(r => r.Id).Distinct().Count() != chains.Length ||
                chains.SelectMany(r => r.InitialBodies()).Select(p => p.Id).Distinct().Count() != chains.Sum(r => r.Count) ||
                chains.SelectMany(r => r.InitialBodies()).Any(p => !props.Any(v => v.Id == p.Id &&
                    v.X == p.X && v.Height == p.Height && v.Width == p.Width && v.BodyHeight == p.BodyHeight && v.Mass == p.Mass)))
                throw new ArgumentException("吊绳拓扑与物理道具初值不匹配。");
            HangingRopes = Array.AsReadOnly(chains.OrderBy(r => r.Id).ToArray());
        }
    }
}
