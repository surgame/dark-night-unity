using System;

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
        public string WorldId { get; }
        public string Seed { get; }
        public int Count => materials.Length;

        public PlayableTerrain(string worldId, string seed, byte[] cells, bool[] protectedCells)
        {
            if (!Guid.TryParseExact(worldId, "N", out _) || string.IsNullOrWhiteSpace(seed) || seed.Length > 80 ||
                cells == null || cells.Length != TerrainGenerationSettings.Width * TerrainGenerationSettings.Height ||
                protectedCells == null || protectedCells.Length != cells.Length)
                throw new ArgumentException("随机地图身份或尺寸无效。");
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] > 8 || (protectedCells[i] && cells[i] == 0)) throw new ArgumentException("随机地图材料或保护位无效。");
            for (int x = 0; x < TerrainGenerationSettings.Width; x++)
                if (cells[(TerrainGenerationSettings.Height - 1) * TerrainGenerationSettings.Width + x] != 8)
                    throw new ArgumentException("随机地图底边必须保留基岩。");
            for (int x = 0; x < CampColumns; x++)
                for (int y = 0; y < CampRow + 4; y++)
                    if (y < CampRow ? cells[y * TerrainGenerationSettings.Width + x] != 0 :
                        cells[y * TerrainGenerationSettings.Width + x] == 0 || !protectedCells[y * TerrainGenerationSettings.Width + x])
                        throw new ArgumentException("营地保护区域不完整。");
            WorldId = worldId; Seed = seed;
            materials = (byte[])cells.Clone(); protection = (bool[])protectedCells.Clone();
        }

        public byte Material(int x, int y) => materials[y * TerrainGenerationSettings.Width + x];
        public byte[] CopyMaterials() => (byte[])materials.Clone();
        public bool[] CopyProtection() => (bool[])protection.Clone();
        public TerrainBlueprint Blueprint() => new TerrainBlueprint(new TerrainGenerationSettings { Seed = Seed },
            materials, protection, new int[TerrainGenerationSettings.Width], Array.Empty<TerrainRoom>());
    }
}
