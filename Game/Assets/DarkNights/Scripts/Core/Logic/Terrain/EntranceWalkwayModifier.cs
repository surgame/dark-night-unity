namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 已退场入口步道的无操作标记；保留旧调用的类型及步骤身份，所有布局均不再挖路、填岩或修复洞穴。
    /// 参数仅兼容旧调用，新的生成流水线不包含此步骤。
    /// </summary>
    public sealed class EntranceWalkwayModifier : ITerrainGenerationModifier
    {
        public string StableId => "entrance-walkway-v2";
        public TerrainGenerationStage Stage => TerrainGenerationStage.AfterDock;

        public EntranceWalkwayModifier(int headroom = 5, int maxCandidates = 64) { }

        public void Apply(TerrainGenerationContext context) { }
    }
}
