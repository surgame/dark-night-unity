using System;
using System.Collections.Generic;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using GameCore.Objects.Types;

namespace DarkNights.Runtime.Framework
{
    /// <summary>
    /// 从 YYGC 定义目录建立只读的规则查询，不保存另一张可编辑注册表。
    /// 实体家族和 RuleKey 来自显式 IConfigData，不解析定义 Key 的前缀或后缀。
    /// 索引只持有定义，不拥有实例、数值或世界状态；显示名或 Key 改名不改变规则身份。
    /// </summary>
    public sealed class DefinitionRuleIndex
    {
        private readonly Dictionary<string, ObjectDefinition> definitions =
            new Dictionary<string, ObjectDefinition>(StringComparer.Ordinal);

        public int Count => definitions.Count;

        public DefinitionRuleIndex(ObjectDefinitionDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            foreach (ObjectDefinition definition in database.Definitions)
            {
                if (definition == null) throw new InvalidOperationException("Null object definition.");
                if (!IsEntityType(definition.Type)) continue;
                string kind = RuleKey(definition);
                if (!definition.isLocal || definition.Guid.IsEmpty || definition.PrefabRef == null ||
                    !definition.PrefabRef.RuntimeKeyIsValid() || !definitions.TryAdd(kind, definition))
                    throw new InvalidOperationException("Invalid or duplicate rule definition: " + definition.Key);
            }
        }

        public ObjectDefinition GetRequired(string kind)
        {
            if (kind == null || !definitions.TryGetValue(kind, out ObjectDefinition definition))
                throw new KeyNotFoundException("Unknown rule definition: " + kind);
            return definition;
        }

        public void Validate(GameCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            foreach (string kind in catalog.Balance.Units.Keys) Require(kind, ObjectType.Unit);
            foreach (string kind in catalog.Balance.Buildings.Keys) Require(kind, ObjectType.Placeable_CompositeStructure);
            foreach (string kind in catalog.Balance.Worksites.Keys) Require(kind, ObjectType.Scenery_ResourceNode);
            if (Count != catalog.Balance.Units.Count + catalog.Balance.Buildings.Count + catalog.Balance.Worksites.Count)
                throw new InvalidOperationException("Definition directory contains unknown rule content.");
        }

        public static string RuleKey(ObjectDefinition definition)
        {
            if (definition == null) throw new InvalidOperationException("Placement definition is missing.");
            if (!IsEntityType(definition.Type)) throw new InvalidOperationException("Definition is not an entity.");
            string ruleKey = ObjectSessionResources.Rule(definition);
            bool family = definition.Type == ObjectType.Unit && definition.SharedConfigs.Exists(c => c is ActorRuleConfig) ||
                definition.Type == ObjectType.Placeable_CompositeStructure && definition.SharedConfigs.Exists(c => c is BuildingRuleConfig) ||
                definition.Type == ObjectType.Scenery_ResourceNode && definition.SharedConfigs.Exists(c => c is WorksiteRuleConfig);
            if (!family || string.IsNullOrWhiteSpace(ruleKey)) throw new InvalidOperationException("RuleKey family does not match definition.");
            return ruleKey;
        }

        public static bool IsEntityType(ObjectType type) => type == ObjectType.Unit ||
            type == ObjectType.Placeable_CompositeStructure || type == ObjectType.Scenery_ResourceNode;

        private void Require(string kind, ObjectType type)
        {
            if (GetRequired(kind).Type != type)
                throw new InvalidOperationException("Rule category mismatch: " + kind);
        }
    }
}
