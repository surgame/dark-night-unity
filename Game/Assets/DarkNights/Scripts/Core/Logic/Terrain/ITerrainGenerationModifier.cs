namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>后台可执行的纯数据地形步骤；构造时冻结作者参数，不持有 Unity 对象或运行时世界状态。</summary>
    public interface ITerrainGenerationModifier
    {
        string StableId { get; }
        TerrainGenerationStage Stage { get; }
        void Apply(TerrainGenerationContext context);
    }
}
