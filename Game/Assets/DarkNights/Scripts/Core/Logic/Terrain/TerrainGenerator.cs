using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>现行洞穴规则的纯生成入口；按明确布局产出冻结蓝图，不包含旧营地或HTML参考生成分支。</summary>
    public static class TerrainGenerator
    {
        /// <summary>完整星球生成所用的洞穴阶段；调用方传入已解析种子，不负责制作抽样或正式地图加载。</summary>
        public static TerrainBlueprint GenerateCave(TerrainGenerationSettings input, string seed)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var settings = input.CopyValidated();
            settings.Seed = seed;
            return Generate(settings);
        }

        public static TerrainBlueprint Generate(TerrainGenerationSettings input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var settings = input.CopyValidated();
            if (settings.ResourceProfile != TerrainGenerationSettings.CaveExplorationProfile)
                throw new ArgumentException("旧营地及参考生成方案已退出正式生成入口。", nameof(input));
            switch (settings.Layout)
            {
                case TerrainGenerationSettings.ConnectedRooms: return CaveRuleGenerator.Generate(settings);
                case TerrainGenerationSettings.NaturalLayout: return CaveExplorationGenerator.Generate(settings);
                default: throw new ArgumentException("未知洞穴布局算法。", nameof(input));
            }
        }
    }
}
