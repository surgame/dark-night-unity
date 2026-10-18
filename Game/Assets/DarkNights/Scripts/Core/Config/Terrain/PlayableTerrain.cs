using System;
using System.Collections.Generic;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>灰松谷随机场景的冻结格子合同与坐标约定；仅用于生成、传输验收和保存，不拥有运行写权限。</summary>
    public sealed class PlayableTerrain
    {
        public const int CellPixels = 16;
        public const int CampRow = 40;
        public const int CampColumns = 72;
        public const float MinimumHeight = (CampRow - TerrainGenerationSettings.Height + 1) * CellPixels;
        public const float OriginY = (CampRow - .5f) * CellPixels;
        private readonly byte[] materials;
        private readonly bool[] protection;
        private readonly bool[] softRock;
        private readonly byte[] shapes;
        public bool Expedition { get; }
        public string WorldId { get; }
        public string Seed { get; }
        public BackgroundBakeDescriptor Background { get; }
        public int Count => materials.Length;
        public IReadOnlyList<TerrainRoom> Rooms { get; }
        public IReadOnlyList<TerrainDepositBlueprint> Deposits { get; }
        public IReadOnlyList<TerrainDamageRecord> Damage { get; }
        public string MiningRulesFingerprint { get; }
        public MineralMapSnapshot Minerals { get; }
        public IReadOnlyList<TerrainNest> Nests { get; }
        public IReadOnlyList<TerrainNestProgress> NestProgress { get; }
        public IReadOnlyList<TerrainGeologySite> GeologySites { get; }
        public IReadOnlyList<TerrainGeologyProgress> GeologyProgress { get; }

        public PlayableTerrain(string worldId, string seed, byte[] cells, bool[] protectedCells)
            : this(worldId, seed, cells, protectedCells,
                new bool[cells == null ? 0 : cells.Length], Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>())
        {
        }

        public PlayableTerrain(string worldId, string seed, byte[] cells, bool[] protectedCells,
            bool[] softRock, TerrainRoom[] rooms, TerrainDepositBlueprint[] deposits, byte[] shapes = null, bool expedition = false,
            BackgroundBakeDescriptor background = null, TerrainDamageRecord[] damage = null, string miningRulesFingerprint = "", MineralMapSnapshot minerals = null,
            TerrainNest[] nests = null, TerrainNestProgress[] nestProgress = null,
            TerrainGeologySite[] geologySites = null, TerrainGeologyProgress[] geologyProgress = null)
        {
            if (!Guid.TryParseExact(worldId, "N", out _) || string.IsNullOrWhiteSpace(seed) || seed.Length > 80 ||
                cells == null || cells.Length != TerrainGenerationSettings.Width * TerrainGenerationSettings.Height ||
                protectedCells == null || protectedCells.Length != cells.Length || softRock == null ||
                softRock.Length != cells.Length || rooms == null || deposits == null)
                throw new ArgumentException("随机地图身份或尺寸无效。");
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] > 8 || (protectedCells[i] && cells[i] == 0) ||
                    (softRock[i] && (cells[i] == 8 || protectedCells[i])))
                    throw new ArgumentException("随机地图材料、保护位或软岩标记无效。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var mineralCoordinates = new HashSet<int>();
            foreach (TerrainDepositBlueprint deposit in deposits)
            {
                if (deposit == null || deposit.X < 0 || deposit.X >= TerrainGenerationSettings.Width ||
                    deposit.Y < 0 || deposit.Y >= TerrainGenerationSettings.Height || !ids.Add(deposit.Id))
                    throw new ArgumentException("随机地图矿床标记重复或越界。");
                foreach (var cell in deposit.Cells)
                    if (!mineralCoordinates.Add(-cell.V * TerrainGenerationSettings.Width + cell.U) ||
                        protectedCells[-cell.V * TerrainGenerationSettings.Width + cell.U])
                        throw new ArgumentException("初始矿格重叠或占用保护格。");
            }
            if (mineralCoordinates.Count > cells.Length) throw new ArgumentException("初始矿格超过地图预算。");
            for (int x = 0; x < TerrainGenerationSettings.Width; x++)
                if (cells[(TerrainGenerationSettings.Height - 1) * TerrainGenerationSettings.Width + x] != 8)
                    throw new ArgumentException("随机地图底边必须保留基岩。");
            this.shapes = shapes == null ? new byte[cells.Length] : (byte[])shapes.Clone();
            if (this.shapes.Length != cells.Length) throw new ArgumentException("坡形尺寸无效。");
            for (int i = 0; i < cells.Length; i++)
                if (this.shapes[i] > 12 || (this.shapes[i] != 0 && (cells[i] == 0 || protectedCells[i])))
                    throw new ArgumentException("坡形内容无效。");
            Expedition = expedition;
            damage = damage ?? Array.Empty<TerrainDamageRecord>();
            var coordinates = new HashSet<int>();
            foreach (var record in damage)
                if (record == null || !coordinates.Add(-record.V * TerrainGenerationSettings.Width + record.U) ||
                    cells[-record.V * TerrainGenerationSettings.Width + record.U] == 0)
                    throw new ArgumentException("格业务保存重复或对应空格。");
            if (miningRulesFingerprint == null || miningRulesFingerprint.Length != 0 && miningRulesFingerprint.Length != 64)
                throw new ArgumentException("采集规则指纹无效。");
            Damage = Array.AsReadOnly((TerrainDamageRecord[])damage.Clone()); MiningRulesFingerprint = miningRulesFingerprint;
            Minerals = minerals;
            nests = nests ?? Array.Empty<TerrainNest>(); nestProgress = nestProgress ?? Array.Empty<TerrainNestProgress>();
            var nestIds = new HashSet<int>(); var nestCells = new HashSet<int>();
            if (nests.Length > TerrainNest.MaximumCount || nestProgress.Length != 0 && nestProgress.Length != nests.Length)
                throw new ArgumentException("巢穴数量或保存覆盖范围无效。");
            foreach (var nest in nests)
            {
                if (nest == null || !nestIds.Add(nest.Id)) throw new ArgumentException("巢穴编号重复。");
                for (int y = nest.Row - 2; y <= nest.Row; y++) for (int x = nest.U; x <= nest.U + 4; x++)
                    if (protectedCells[y * TerrainGenerationSettings.Width + x] || cells[y * TerrainGenerationSettings.Width + x] == 8 ||
                        !nestCells.Add(y * TerrainGenerationSettings.Width + x)) throw new ArgumentException("巢位重叠或侵入保护区域。");
                if (nestProgress.Length == 0)
                {
                    if (cells[nest.Row * TerrainGenerationSettings.Width + nest.U] == 0 ||
                        cells[(nest.Row - 1) * TerrainGenerationSettings.Width + nest.U] == 0)
                        throw new ArgumentException("未运行巢穴必须保留完整封壳。");
                    for (int y = nest.Row - 2; y <= nest.Row; y++) for (int x = nest.U + 1; x <= nest.U + 4; x++)
                        if (cells[y * TerrainGenerationSettings.Width + x] != 0) throw new ArgumentException("未运行巢穴必须有冻结空腔。");
                }
            }
            var progressIds = new HashSet<int>();
            foreach (var progress in nestProgress)
                if (progress == null || !nestIds.Contains(progress.Id) || !progressIds.Add(progress.Id) ||
                    System.Linq.Enumerable.Single(nests, n => n.Id == progress.Id).HitPoints < progress.Hp)
                    throw new ArgumentException("巢穴保存身份不匹配。");
            Nests = Array.AsReadOnly((TerrainNest[])nests.Clone()); NestProgress = Array.AsReadOnly((TerrainNestProgress[])nestProgress.Clone());
            geologySites = geologySites ?? Array.Empty<TerrainGeologySite>();
            geologyProgress = geologyProgress ?? Array.Empty<TerrainGeologyProgress>();
            if (geologySites.Length > 1 || geologySites.Length != 0 && !expedition || System.Linq.Enumerable.Any(geologySites, s => s == null) ||
                geologyProgress.Length != 0 && (geologyProgress.Length != geologySites.Length || geologyProgress[0] == null ||
                    geologyProgress[0].SiteId != geologySites[0].SiteId))
                throw new ArgumentException("首片塌方站点或状态覆盖范围无效。");
            GeologySites = Array.AsReadOnly((TerrainGeologySite[])geologySites.Clone());
            GeologyProgress = Array.AsReadOnly((TerrainGeologyProgress[])geologyProgress.Clone());
            WorldId = worldId; Seed = seed;
            if (background != null && (background.WorldId != worldId || background.LayoutSeed != seed))
                throw new ArgumentException("背景参考与地图身份不一致。");
            Background = background;
            materials = (byte[])cells.Clone(); protection = (bool[])protectedCells.Clone(); this.softRock = (bool[])softRock.Clone();
            Rooms = Array.AsReadOnly((TerrainRoom[])rooms.Clone());
            Deposits = Array.AsReadOnly((TerrainDepositBlueprint[])deposits.Clone());
        }

        public byte Material(int x, int y) => materials[y * TerrainGenerationSettings.Width + x];
        public byte[] CopyMaterials() => (byte[])materials.Clone();
        public bool[] CopyProtection() => (bool[])protection.Clone();
        public bool[] CopySoftRock() => (bool[])softRock.Clone();
        public byte[] CopyShapes() => (byte[])shapes.Clone();
        public TerrainBlueprint Blueprint() => new TerrainBlueprint(new TerrainGenerationSettings { Seed = Seed,
            ResourceProfile = Expedition ? TerrainGenerationSettings.CaveExplorationProfile : TerrainGenerationSettings.GameplayResourceProfile },
            materials, protection, new int[TerrainGenerationSettings.Width],
            new List<TerrainRoom>(Rooms).ToArray(), softRock, new List<TerrainDepositBlueprint>(Deposits).ToArray(), shapes: shapes);
    }
}
