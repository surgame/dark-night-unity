using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>Unity 作者配置的接口型地形步骤；由 SerializeReference 保存具体类型，进入后台前冻结为纯 Core 实现。</summary>
    public interface ITerrainGenerationModifierConfig
    {
        bool Enabled { get; }
        string CanonicalSettings { get; }
        ITerrainGenerationModifierConfig Copy();
        ITerrainGenerationModifier Freeze();
    }
}
