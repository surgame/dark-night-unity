namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>单个生成步骤的格子差异摘要；工作台据此显示阶段、范围和变更量，不进入运行时权威状态。</summary>
    public sealed class TerrainModifierDiagnostic
    {
        public string StableId { get; }
        public TerrainGenerationStage Stage { get; }
        public int ChangedCells { get; }
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }

        public TerrainModifierDiagnostic(string stableId, TerrainGenerationStage stage, int changedCells,
            int minX, int minY, int maxX, int maxY)
        {
            StableId = stableId; Stage = stage; ChangedCells = changedCells;
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }
    }
}
