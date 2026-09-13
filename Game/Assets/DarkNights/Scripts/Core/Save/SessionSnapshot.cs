using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 一局完整的schema v1传输对象，仅描述可保存状态。它与实体分离，读取时先验证整个快照，再替换当前会话。
    /// </summary>
    public sealed class SessionSnapshot
    {
        public int SchemaVersion { get; }
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
        public double CameraX { get; }
        public double CameraZoom { get; }
        public IReadOnlyList<int> SelectedIds { get; }
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
            double cameraX,
            double cameraZoom,
            IReadOnlyList<int> selectedIds,
            SessionMode mode = SessionMode.Playing,
            IReadOnlyList<EntityIdentityData> identities = null)
        {
            SchemaVersion = schemaVersion;
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
            CameraX = cameraX;
            CameraZoom = cameraZoom;
            SelectedIds = selectedIds == null ? null : new List<int>(selectedIds).AsReadOnly();
        }
    }
}
