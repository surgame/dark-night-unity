using System;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>单次候选的可写权威格子上下文；所有修改在坡形与背景捕获前完成，保护位和软岩同步写入。</summary>
    public sealed class TerrainGenerationContext
    {
        public const int Width = TerrainGenerationSettings.Width;
        public const int Height = TerrainGenerationSettings.Height;
        private readonly byte[] cells;
        private readonly bool[] protection;
        private readonly bool[] soft;
        public TerrainBlueprint Source { get; }
        public PlanetDefinition Planet { get; }
        public Func<bool> Cancelled { get; }

        public TerrainGenerationContext(TerrainBlueprint source, PlanetDefinition planet, byte[] cells,
            bool[] protection, bool[] soft, Func<bool> cancelled)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Planet = planet ?? throw new ArgumentNullException(nameof(planet));
            if (cells == null || protection == null || soft == null || cells.Length != Width * Height ||
                protection.Length != cells.Length || soft.Length != cells.Length)
                throw new ArgumentException("地形步骤输入尺寸无效。");
            this.cells = cells; this.protection = protection; this.soft = soft; Cancelled = cancelled;
        }

        public byte MaterialAt(int x, int y) => cells[Index(x, y)];
        public bool IsProtected(int x, int y) => protection[Index(x, y)];
        public byte[] CopyMaterials() => (byte[])cells.Clone();
        public bool[] CopyProtection() => (bool[])protection.Clone();
        public bool[] CopySoftRock() => (bool[])soft.Clone();

        public void SetCell(int x, int y, byte material, bool protect = false, bool softRock = false)
        {
            if (material > 8 || protect && material == 0 || softRock && (protect || material == 8))
                throw new ArgumentException("地形步骤写入的材料标记无效。");
            int index = Index(x, y);
            cells[index] = material; protection[index] = protect; soft[index] = softRock;
        }

        public void Replace(byte[] materials, bool[] protectedCells, bool[] softRock)
        {
            if (materials == null || protectedCells == null || softRock == null ||
                materials.Length != cells.Length || protectedCells.Length != cells.Length || softRock.Length != cells.Length)
                throw new ArgumentException("地形步骤替换尺寸无效。");
            Array.Copy(materials, cells, cells.Length);
            Array.Copy(protectedCells, protection, cells.Length);
            Array.Copy(softRock, soft, cells.Length);
        }

        public void CheckCancellation()
        {
            if (Cancelled != null && Cancelled()) throw new OperationCanceledException("星球候选生成已取消。");
        }

        private static int Index(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(x));
            return y * Width + x;
        }
    }
}
