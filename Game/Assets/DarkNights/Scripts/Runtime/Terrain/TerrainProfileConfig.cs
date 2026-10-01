using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using GameCore.Objects.Behaviours.Interfaces;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>正式 WorldSession 的地形共享配置；绑定作者资源及采集规则，启动前冻结，运行中不读取可变资产。</summary>
    [Serializable]
    public sealed class TerrainProfileConfig : IConfigData
    {
        public string Name => "网格地形与采集";
        public ARDMapDefinition Definition;
        public ARDMapDefinition ContourDefinition;
        public UnityEngine.Object CaveStyle;
        public UnityEngine.Object BackgroundStyle;
        public List<TerrainMaterialMiningRule> Materials = new List<TerrainMaterialMiningRule>
        {
            new TerrainMaterialMiningRule { MaterialKey = "loam" },
            new TerrainMaterialMiningRule { MaterialKey = "slate" },
            new TerrainMaterialMiningRule { MaterialKey = "basalt" },
            new TerrainMaterialMiningRule { MaterialKey = "copper", ResourceId = "iron", Yield = 1 },
            new TerrainMaterialMiningRule { MaterialKey = "iron", ResourceId = "iron", Yield = 1 },
            new TerrainMaterialMiningRule { MaterialKey = "gold", ResourceId = "gold", Yield = 1 },
            new TerrainMaterialMiningRule { MaterialKey = "moss" },
            new TerrainMaterialMiningRule { MaterialKey = "bedrock" }
        };

        public static TerrainProfileConfig Resolve()
        {
            var definition = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch");
            return definition.SharedConfigs.OfType<TerrainProfileConfig>().Single();
        }

        public FrozenTerrainRules Freeze(ARDMapDefinition definition = null)
        {
            definition = definition ?? Definition;
            if (definition == null) throw new InvalidOperationException("地形 Profile 缺少 AnyRuleD 编译目录。");
            return new FrozenTerrainRules(definition.LoadGameplayCatalog(), definition.ExportBusinessDefinitions(), Materials);
        }
    }
}
