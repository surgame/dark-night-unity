using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>一局装备定义的冻结索引；只保存静态能力与定义引用，装备持有关系始终归 ActorState。</summary>
    public sealed class EquipmentDefinitionCatalog
    {
        private readonly Dictionary<string, ObjectDefinition> definitions = new Dictionary<string, ObjectDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, HeroEquipmentKind> kinds = new Dictionary<string, HeroEquipmentKind>(StringComparer.Ordinal);
        private readonly Dictionary<string, MiningToolRules> tools = new Dictionary<string, MiningToolRules>(StringComparer.Ordinal);
        private readonly Dictionary<string, FlashlightRules> lights = new Dictionary<string, FlashlightRules>(StringComparer.Ordinal);
        public string Fingerprint { get; }
        public IReadOnlyList<ObjectDefinition> MiningDefinitions { get; }
        public IReadOnlyList<ObjectDefinition> LightDefinitions { get; }
        public string StarterLight { get; private set; } = "";

        public EquipmentDefinitionCatalog(ObjectDefinitionDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            var mining = new List<ObjectDefinition>();
            var illumination = new List<ObjectDefinition>();
            var identity = new List<string>();
            foreach (var definition in database.Definitions.Where(value => value != null &&
                (value.SharedConfigs.OfType<EquipmentItemConfig>().Any() || value.SharedConfigs.OfType<FlashlightToolConfig>().Any())).OrderBy(value => value.Guid.ToString(), StringComparer.Ordinal))
            {
                var item = definition.SharedConfigs.OfType<EquipmentItemConfig>().SingleOrDefault();
                item?.Validate();
                string guid = definition.Guid.ToString();
                if (definition.Guid.IsEmpty || !definition.isLocal || !definitions.TryAdd(guid, definition))
                    throw new InvalidOperationException("装备定义身份或对象生命周期无效。");
                kinds.Add(guid, item?.Handheld ?? HeroEquipmentKind.Empty);
                var light = definition.SharedConfigs.OfType<FlashlightToolConfig>().SingleOrDefault();
                bool emits = definition.BehaviourTypes.Contains(typeof(FlashlightToolBehaviour).FullName);
                if ((light != null) != emits) throw new InvalidOperationException("手电必须同时声明配置和 Behaviour：" + definition.Key);
                if (light != null)
                {
                    lights.Add(guid, light.Freeze()); illumination.Add(definition);
                    if (light.Starter)
                    {
                        if (StarterLight != "") throw new InvalidOperationException("初始手电 Definition 必须唯一。");
                        StarterLight = guid;
                    }
                }
                var config = definition.SharedConfigs.OfType<MiningToolConfig>().SingleOrDefault();
                bool behaviour = definition.BehaviourTypes.Contains(typeof(MiningToolBehaviour).FullName);
                if ((config != null) != behaviour) throw new InvalidOperationException("采集工具必须同时声明配置和 Behaviour：" + definition.Key);
                if (config != null)
                {
                    tools.Add(guid, config.Freeze()); mining.Add(definition);
                    foreach (var target in config.Deposits)
                        if (target.Resolve(database)?.SharedConfigs.OfType<MineralDepositRuleConfig>().SingleOrDefault() == null)
                            throw new InvalidOperationException("工具引用了非矿床 Definition。");
                }
                identity.Add(guid + ":" + item?.RuleKey + ":" + item?.Handheld + ":" + item?.Jetpack + ":" + (config?.Fingerprint() ?? "") + ":" + light?.Starter + ":" + light?.Freeze().Fingerprint);
            }
            MiningDefinitions = mining.AsReadOnly();
            LightDefinitions = illumination.AsReadOnly();
            Fingerprint = string.Join(";", identity);
        }

        public ObjectDefinition Resolve(string guid) => !string.IsNullOrEmpty(guid) && definitions.TryGetValue(guid, out var value) ? value : null;
        public MiningToolRules Mining(string guid) => !string.IsNullOrEmpty(guid) && tools.TryGetValue(guid, out var value) ? value : null;
        public FlashlightRules Light(string guid) => !string.IsNullOrEmpty(guid) && lights.TryGetValue(guid, out var value) ? value : null;
        public HeroEquipmentKind Kind(string guid) => !string.IsNullOrEmpty(guid) && kinds.TryGetValue(guid, out var value) ? value : HeroEquipmentKind.Empty;
        public bool CanEquip(string guid) => Resolve(guid) != null && Kind(guid) != HeroEquipmentKind.Empty;
    }
}
