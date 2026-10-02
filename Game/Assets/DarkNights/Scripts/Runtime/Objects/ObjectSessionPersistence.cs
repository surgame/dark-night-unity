using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Save;
using DarkNights.Runtime.Save;

namespace DarkNights.Runtime.Objects
{
    /// <summary>会话存储合同与冻结重开基线；仅保留原有保存副本，恢复先验证候选再原子交换，不另建运行世界。</summary>
    internal sealed class ObjectSessionPersistence
    {
        private readonly ObjectSession world;
        private SessionSnapshot initial;
        public ObjectWorldSaveJson Codec { get; }
        internal ObjectSessionPersistence(ObjectSession world, IReadOnlyList<ObjectPlacement> placements)
        {
            this.world = world;
            Codec = new ObjectWorldSaveJson(world.Catalog, world.Layout,
                world.Resources.Definitions.ToDictionary(ObjectSessionResources.Rule, d => d.Guid.ToString()),
                placements.ToDictionary(p => p.PlacementKey, p => ObjectSessionResources.Rule(p.Definition)), world.Projectiles.Settings,
                (world.Terrain?.Rules.Fingerprint ?? "") + "|" + world.Resources.Equipment.Fingerprint + "|" + string.Join(";", world.Resources.Definitions
                    .OrderBy(value => value.Guid.ToString(), StringComparer.Ordinal)
                    .SelectMany(value => value.SharedConfigs.OfType<MineralDepositRuleConfig>()
                        .Select(config => value.Guid + ":" + config.Fingerprint()))));
        }
        internal void CaptureInitial() => initial = world.CaptureWorld();
        internal void Restart() => Restore(initial);
        internal void Restore(string json) => Restore(Codec.Parse(json));
        private void Restore(SessionSnapshot snapshot)
        {
            if ((world.Terrain == null) != (snapshot.Terrain == null)) throw new FormatException("存档地图类型不匹配。");
            var journey = snapshot.Expedition?.Journey;
            if (world.Flow == null ? journey != null : !world.Flow.Accepts(journey))
                throw new FormatException("存档航程配置与当前会话不一致。");
            Terrain.TerrainMapAuthority map = world.Terrain?.Prepare(snapshot.Terrain);
            try
            {
                using var candidate = new ObjectWorldRestore(world, snapshot);
                candidate.Commit();
                world.Flow?.ResetPending();
                if (map != null) { world.Terrain.Replace(map, snapshot.Terrain); map = null; }
            }
            finally { map?.Dispose(); }
        }
    }
}
