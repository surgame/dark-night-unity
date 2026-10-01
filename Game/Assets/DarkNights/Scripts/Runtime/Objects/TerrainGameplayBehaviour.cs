using System;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>由 WorldSession Definition 装配的网格业务能力；确认唯一 SessionTerrain 的配置身份，不创建第二张地图或复制格子耐久。</summary>
    [RequireConfig(typeof(TerrainProfileConfig))]
    public sealed partial class TerrainGameplayBehaviour : PooledBehaviour
    {
        [Inject] private TerrainProfileConfig config;
        public FrozenTerrainRules Rules { get; private set; }

        public void InitializeSession(ObjectSession session)
        {
            if (config == null || session == null) throw new InvalidOperationException("缺少网格业务装配配置。");
            if (session.Terrain == null) { Rules = null; return; }
            Rules = session.Terrain.Rules;
            if (Rules.Fingerprint != config.Freeze(session.Terrain.Definition).Fingerprint)
                throw new InvalidOperationException("网格与 ObjectDefinition 配置身份不一致。");
        }
        protected override void OnSpawn()
        {
            base.OnSpawn();
            Rules = null;
        }

        public override void OnDespawn()
        {
            Rules = null;
            base.OnDespawn();
        }
    }
}
