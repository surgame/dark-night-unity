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
        public string WorldId { get; }
        public string Seed { get; }
        public int Count => materials.Length;
        public IReadOnlyList<TerrainRoom> Rooms { get; }
        public IReadOnlyList<TerrainDepositBlueprint> Deposits { get; }

        public PlayableTerrain(string worldId, string seed, byte[] cells, bool[] protectedCells)
            : this(worldId, seed, cells, protectedCells,
                new bool[cells == null ? 0 : cells.Length], Array.Empty<TerrainRoom>(), Array.Empty<TerrainDepositBlueprint>())
        {
        }

        public PlayableTerrain(string worldId, string seed, byte[] cells, bool[] protectedCells,
            bool[] softRock, TerrainRoom[] rooms, TerrainDepositBlueprint[] deposits)
        {
            if (!Guid.TryParseExact(worldId, "N", out _) || string.IsNullOrWhiteSpace(seed) || seed.Length > 80 ||
                cells == null || cells.Length != TerrainGenerationSettings.Width * TerrainGenerationSettings.Height ||
                protectedCells == null || protectedCells.Length != cells.Length || softRock == null ||
                softRock.Length != cells.Length || rooms == null || deposits == null)
                throw new ArgumentException("随机地图身份或尺寸无效。");
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] > 8 || (protectedCells[i] && cells[i] == 0) ||
                    (softRock[i] && (cells[i] == 0 || cells[i] == 8 || protectedCells[i])))
                    throw new ArgumentException("随机地图材料、保护位或软岩标记无效。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TerrainDepositBlueprint deposit in deposits)
                if (deposit == null || deposit.X < 0 || deposit.X >= TerrainGenerationSettings.Width ||
                    deposit.Y < 0 || deposit.Y >= TerrainGenerationSettings.Height || !ids.Add(deposit.Id))
                    throw new ArgumentException("随机地图矿床标记重复或越界。");
            for (int x = 0; x < TerrainGenerationSettings.Width; x++)
                if (cells[(TerrainGenerationSettings.Height - 1) * TerrainGenerationSettings.Width + x] != 8)
                    throw new ArgumentException("随机地图底边必须保留基岩。");
            for (int x = 0; x < CampColumns; x++)
                for (int y = 0; y < CampRow + 4; y++)
                    if (y < CampRow ? cells[y * TerrainGenerationSettings.Width + x] != 0 :
                        cells[y * TerrainGenerationSettings.Width + x] == 0 || !protectedCells[y * TerrainGenerationSettings.Width + x])
                        throw new ArgumentException("营地保护区域不完整。");
            WorldId = worldId; Seed = seed;
            materials = (byte[])cells.Clone(); protection = (bool[])protectedCells.Clone(); this.softRock = (bool[])softRock.Clone();
            Rooms = Array.AsReadOnly((TerrainRoom[])rooms.Clone());
            Deposits = Array.AsReadOnly((TerrainDepositBlueprint[])deposits.Clone());
        }

        public byte Material(int x, int y) => materials[y * TerrainGenerationSettings.Width + x];
        public byte[] CopyMaterials() => (byte[])materials.Clone();
        public bool[] CopyProtection() => (bool[])protection.Clone();
        public bool[] CopySoftRock() => (bool[])softRock.Clone();
        public TerrainBlueprint Blueprint() => new TerrainBlueprint(new TerrainGenerationSettings { Seed = Seed },
            materials, protection, new int[TerrainGenerationSettings.Width],
            new List<TerrainRoom>(Rooms).ToArray(), softRock, new List<TerrainDepositBlueprint>(Deposits).ToArray());
    }
}
