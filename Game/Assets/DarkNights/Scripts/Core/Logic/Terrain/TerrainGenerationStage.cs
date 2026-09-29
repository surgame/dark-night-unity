namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>权威格子生成的稳定插入点；顺序由生成器固定，步骤只在其声明的阶段运行。</summary>
    public enum TerrainGenerationStage
    {
        AfterCave = 0,
        AfterSky = 1,
        AfterDock = 2,
        BeforeGeometry = 3
    }
}
