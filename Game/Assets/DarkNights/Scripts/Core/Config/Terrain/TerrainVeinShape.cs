namespace DarkNights.Core.Config.Terrain
{
    /// <summary>
    /// 开发端近壁矿脉的形态配方；只决定背景矿层占格，不改变前景墙、巢腔或生物行为。
    /// 序号用于工具预设，新增值只在末尾追加，不能据此推断矿量或必然伴巢。
    /// </summary>
    public enum TerrainVeinShape
    {
        ThickMass,
        Branching,
        AdjacentLobes,
        LayeredBands
    }
}
