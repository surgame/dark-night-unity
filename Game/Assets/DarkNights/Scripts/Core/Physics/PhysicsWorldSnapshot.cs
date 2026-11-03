using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Physics
{
    /// <summary>物理网络／恢复切点的深度冻结集合，绑定后端、地图和步号；不承担角色经济、HP或地形的权威所有权。</summary>
    public sealed class PhysicsWorldSnapshot
    {
        public string DefinitionHash { get; }
        public long Tick { get; }
        public string WorldId { get; }
        public ulong MapEpoch { get; }
        public ulong TerrainRevision { get; }
        public IReadOnlyList<PhysicsBodySnapshot> Bodies { get; }
        public IReadOnlyList<PhysicsRopeSnapshot> Ropes { get; }
        public PhysicsWorldSnapshot(string hash, long tick, string worldId, ulong mapEpoch, ulong revision,
            IReadOnlyList<PhysicsBodySnapshot> bodies, IReadOnlyList<PhysicsRopeSnapshot> ropes)
        {
            if (hash == null || hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)) || tick < 0 ||
                worldId == null || worldId.Length != 0 && (worldId.Length != 32 || worldId.Any(c => !Uri.IsHexDigit(c))))
                throw new ArgumentException("Invalid physics snapshot identity.");
            if (worldId.Length == 0 && (mapEpoch != 0 || revision != 0))
                throw new ArgumentException("Mapless physics cannot reference a terrain epoch or revision.");
            if (bodies == null || bodies.Count > PhysicsNumbers.MaximumBodies || bodies.Any(b => b == null) ||
                bodies.Select(b => b.Id).Distinct().Count() != bodies.Count)
                throw new ArgumentException("Invalid physics body collection.");
            if (ropes == null || ropes.Count > PhysicsNumbers.MaximumRopes || ropes.Any(r => r == null) ||
                ropes.Select(r => r.OwnerId).Distinct().Count() != ropes.Count)
                throw new ArgumentException("Invalid physics rope collection.");
            var ids = new HashSet<int>(bodies.Select(b => b.Id));
            if (ropes.Any(r => !ids.Contains(r.OwnerId) || !ids.Contains(r.TargetId)))
                throw new ArgumentException("Rope references an absent body.");
            DefinitionHash = hash; Tick = tick; WorldId = worldId; MapEpoch = mapEpoch; TerrainRevision = revision;
            Bodies = Array.AsReadOnly(bodies.OrderBy(b => b.Id).ToArray());
            Ropes = Array.AsReadOnly(ropes.OrderBy(r => r.OwnerId).ToArray());
        }
    }
}
