using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 一局完整的 v6 冻结恢复合同，复制集合并保持明确的定义与放置身份。
    /// 仅保存玩法状态，不保存本地镜头、选择或房间权限；完整验证后才允许替换活动对象。
    /// </summary>
    public sealed class SessionSnapshot
    {
        public ExpeditionViewData Expedition { get; }
        public const int CurrentVersion = 10;
        public int SchemaVersion { get; }
        public Config.Terrain.PlayableTerrain Terrain { get; }
        public string LevelId { get; }
        public EconomySnapshot Economy { get; }
        public WaveSnapshot Wave { get; }
        public double Elapsed { get; }
        public double Speed { get; }
        public bool Paused { get; }
        public int NextEntityId { get; }
        public string RngSeed { get; }
        public string RngState { get; }
        public IReadOnlyList<ActorSnapshot> Actors { get; }
        public IReadOnlyList<BuildingSnapshot> Buildings { get; }
        public IReadOnlyList<WorksiteSnapshot> Worksites { get; }
        public IReadOnlyList<ProjectileSnapshot> Projectiles { get; }
        public StatisticsSnapshot Stats { get; }
        public SessionMode Mode { get; }
        public IReadOnlyList<EntityIdentityData> Identities { get; }

        public SessionSnapshot(
            int schemaVersion,
            string levelId,
            EconomySnapshot economy,
            WaveSnapshot wave,
            double elapsed,
            double speed,
            bool paused,
            int nextEntityId,
            string rngSeed,
            string rngState,
            IReadOnlyList<ActorSnapshot> actors,
            IReadOnlyList<BuildingSnapshot> buildings,
            IReadOnlyList<WorksiteSnapshot> worksites,
            IReadOnlyList<ProjectileSnapshot> projectiles,
            StatisticsSnapshot stats,
            SessionMode mode = SessionMode.Playing,
            IReadOnlyList<EntityIdentityData> identities = null, Config.Terrain.PlayableTerrain terrain = null, ExpeditionViewData expedition = null)
        {
            Expedition = expedition;
            SchemaVersion = schemaVersion;
            Terrain = terrain;
            Mode = mode;
            Identities = new List<EntityIdentityData>(identities ?? Array.Empty<EntityIdentityData>()).AsReadOnly();
            LevelId = levelId;
            Economy = economy;
            Wave = wave;
            Elapsed = elapsed;
            Speed = speed;
            Paused = paused;
            NextEntityId = nextEntityId;
            RngSeed = rngSeed;
            RngState = rngState;
            Actors = actors == null ? null : new List<ActorSnapshot>(actors).AsReadOnly();
            Buildings = buildings == null ? null : new List<BuildingSnapshot>(buildings).AsReadOnly();
            Worksites = worksites == null ? null : new List<WorksiteSnapshot>(worksites).AsReadOnly();
            Projectiles = projectiles == null ? null : new List<ProjectileSnapshot>(projectiles).AsReadOnly();
            Stats = stats;
        }
    }
}
