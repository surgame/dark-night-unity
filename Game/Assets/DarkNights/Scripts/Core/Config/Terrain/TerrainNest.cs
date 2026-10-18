using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 开发端冻结的单个侧壁巢位；编号只在本地图内唯一，不是实体ID。
    /// 封壳为上下两格，右侧四乘三格巢腔预先挖空；不拥有触发或生物运行状态。
    /// </summary>
    public sealed class TerrainNest
    {
        public const int MaximumCount = 32;
        public int Id { get; }
        public int U { get; }
        public int Row { get; }
        public double WarningSeconds { get; }
        public float CrawlSpeed { get; }
        public int HitPoints { get; }
        public TerrainNest(int id, int u, int row, double warningSeconds = .65, float crawlSpeed = 24, int hitPoints = 20)
        {
            if (id < 1 || id > MaximumCount || u < 4 || u + 4 >= TerrainGenerationSettings.Width ||
                row < 4 || row + 1 >= TerrainGenerationSettings.Height ||
                double.IsNaN(warningSeconds) || warningSeconds < .2 || warningSeconds > 10 ||
                float.IsNaN(crawlSpeed) || crawlSpeed < 1 || crawlSpeed > 128 || hitPoints < 1 || hitPoints > 10000)
                throw new ArgumentException("巢位身份、封壳或试参无效。");
            Id = id; U = u; Row = row; WarningSeconds = warningSeconds; CrawlSpeed = crawlSpeed; HitPoints = hitPoints;
        }
        public float SpawnX => (U + 2) * PlayableTerrain.CellPixels;
        public float SpawnHeight => PlayableTerrain.OriginY - (Row + .5f) * PlayableTerrain.CellPixels;
    }
}
