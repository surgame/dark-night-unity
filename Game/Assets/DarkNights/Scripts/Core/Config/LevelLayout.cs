using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 从唯一场景来源提取的冻结布局，与波次 JSON 分开。构造复制摆放集合，创建世界前校验内容和坐标，禁止用缺省布局启动模拟。
    /// </summary>
    public sealed class LevelLayout
    {
        public bool RandomTerrain { get; }
        public float WorldWidth { get; }
        public float GroundY { get; }
        public float BuildMinX { get; }
        public float BuildMaxX { get; }
        public float SpawnX { get; }
        public float CameraX { get; }
        public IReadOnlyList<PlacementDefinition> Buildings { get; }
        public IReadOnlyList<PlacementDefinition> Worksites { get; }
        public IReadOnlyList<PlacementDefinition> Actors { get; }
        public IReadOnlyList<PlatformDefinition> Platforms { get; }

        public LevelLayout(float worldWidth, float groundY, float buildMinX, float buildMaxX,
            float spawnX, float cameraX, IReadOnlyList<PlacementDefinition> buildings,
            IReadOnlyList<PlacementDefinition> worksites, IReadOnlyList<PlacementDefinition> actors,
            IReadOnlyList<PlatformDefinition> platforms = null, bool randomTerrain = false)
        {
            Platforms = new List<PlatformDefinition>(platforms ?? Array.Empty<PlatformDefinition>()).AsReadOnly();
            WorldWidth = worldWidth;
            RandomTerrain = randomTerrain;
            GroundY = groundY;
            BuildMinX = buildMinX;
            BuildMaxX = buildMaxX;
            SpawnX = spawnX;
            CameraX = cameraX;
            Buildings = new List<PlacementDefinition>(buildings ?? throw new ArgumentNullException(nameof(buildings))).AsReadOnly();
            Worksites = new List<PlacementDefinition>(worksites ?? throw new ArgumentNullException(nameof(worksites))).AsReadOnly();
            Actors = new List<PlacementDefinition>(actors ?? throw new ArgumentNullException(nameof(actors))).AsReadOnly();
        }

        public void Validate(GameCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (!Finite(WorldWidth) || WorldWidth <= 32 || !Finite(GroundY) ||
                !Coordinate(BuildMinX) || !Coordinate(BuildMaxX) || BuildMinX >= BuildMaxX ||
                !Coordinate(SpawnX) || !Coordinate(CameraX))
                throw new ArgumentException("Invalid level bounds.");
            if (Buildings.Count + Worksites.Count + Actors.Count > 255 || Buildings.Count == 0 || Actors.Count == 0)
                throw new ArgumentException("Invalid initial entity count.");
            foreach (var entry in Buildings.Concat(Worksites).Concat(Actors))
                if (entry == null || !Coordinate(entry.X) || entry.Variant < 0 || entry.Variant > 3 || entry.Name.Length > 80)
                    throw new ArgumentException("Invalid placement.");
            if (Buildings.Count(b => b.Kind == "tavern") != 1 ||
                Buildings.Any(b => b.Variant != 0 || !catalog.Balance.Buildings.ContainsKey(b.Kind)) ||
                Worksites.Any(w => w.Kind == "food" || !catalog.Balance.Worksites.ContainsKey(w.Kind)) ||
                Actors.Any(a => a.Variant != 0 || (a.Kind != "worker" && a.Kind != "spearman" && a.Kind != "archer")) ||
                Actors.Any(a => !catalog.Balance.Units.ContainsKey(a.Kind)) ||
                Buildings.Count + Worksites.Count + Actors.Count + Buildings.Count(b => b.Kind == "farm") > 256)
                throw new ArgumentException("Invalid initial content or farm count.");
            if (Platforms.Count > 128 || Platforms.Select(p => p?.Id).Distinct().Count() != Platforms.Count ||
                Platforms.Any(p => p == null || p.Id <= 0 || !Coordinate(p.MinX) || !Coordinate(p.MaxX) ||
                    p.MinX >= p.MaxX || !Finite(p.Height) || p.Height <= 0 ||
                    p.Height > (catalog.Balance.HeroControl?.MaximumHeight ?? 0)))
                throw new ArgumentException("Invalid one-way platform geometry.");
            ValidateOccupancy(catalog);
        }

        private void ValidateOccupancy(GameCatalog catalog)
        {
            for (int index = 0; index < Buildings.Count; index++)
            {
                PlacementDefinition building = Buildings[index];
                float width = catalog.Balance.Buildings[building.Kind].Width;
                if (building.X - width * 0.5f < BuildMinX || building.X + width * 0.5f > BuildMaxX)
                    throw new ArgumentException("Initial building is outside build bounds: " + building.Kind);
                for (int otherIndex = index + 1; otherIndex < Buildings.Count; otherIndex++)
                {
                    PlacementDefinition other = Buildings[otherIndex];
                    float otherWidth = catalog.Balance.Buildings[other.Kind].Width;
                    if (Math.Abs(building.X - other.X) < (width + otherWidth) * 0.5f + 8)
                        throw new ArgumentException("Initial buildings overlap: " + building.Kind + "/" + other.Kind);
                }
                foreach (PlacementDefinition worksite in Worksites)
                {
                    float worksiteWidth = catalog.Balance.Worksites[worksite.Kind].Width;
                    if (Math.Abs(building.X - worksite.X) < (width + worksiteWidth) * 0.5f + 6)
                        throw new ArgumentException("Initial building covers a resource point: " + building.Kind);
                }
            }
        }

        private bool Coordinate(float value) => Finite(value) && value >= 0 && value <= WorldWidth;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
