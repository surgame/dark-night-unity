using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;

namespace DarkNights.Editor
{
    /// <summary>采集辅助区的只读装配校验；验证能力、配置和矿床白名单，不补齐配置、不保存资产。</summary>
    internal static class MiningDefinitionValidation
    {
        internal static void Validate(ObjectDefinition definition, bool tool)
        {
            if (definition == null || definition.Guid.IsEmpty || !definition.isLocal)
                throw new InvalidOperationException("请选择具有稳定身份的本地 Definition。");
            if (definition.SharedConfigs == null || definition.BehaviourTypes == null)
                throw new InvalidOperationException("Definition 装配列表尚未初始化。");
            if (tool)
            {
                var items = definition.SharedConfigs.OfType<EquipmentItemConfig>().ToArray();
                var configs = definition.SharedConfigs.OfType<MiningToolConfig>().ToArray();
                if (items.Length != 1 || configs.Length != 1 ||
                    !definition.BehaviourTypes.Contains(typeof(MiningToolBehaviour).FullName))
                    throw new InvalidOperationException("采集工具必须装配唯一的装备配置、采集配置和 MiningToolBehaviour。");
                items[0].Validate();
                if (items[0].Jetpack) throw new InvalidOperationException("喷气背包不能作为手持采集工具保存。");
                configs[0].Freeze();
                foreach (var reference in configs[0].Deposits)
                {
                    var target = reference.Resolve(ObjectDefinitionDatabase.Instance);
                    if (target == null) throw new InvalidOperationException("矿床白名单引用尚未注册或不存在的 Definition。");
                    Validate(target, false);
                }
            }
            else
            {
                var configs = definition.SharedConfigs.OfType<MineralDepositRuleConfig>().ToArray();
                if (configs.Length != 1 || !definition.BehaviourTypes.Contains(typeof(MineralDepositBehaviour).FullName))
                    throw new InvalidOperationException("矿床必须装配唯一的矿床配置和 MineralDepositBehaviour。");
                configs[0].Validate();
            }
        }
    }
}
