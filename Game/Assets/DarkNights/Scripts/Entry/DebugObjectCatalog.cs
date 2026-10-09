#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;

namespace DarkNights.Entry
{
    /// <summary>调试网格的只读定义目录；身份与能力来自现有定义，实例和装备持有关系只从冻结副本读取。</summary>
    internal static class DebugObjectCatalog
    {
        internal static ObjectDefinition[] Definitions() => ObjectDefinitionDatabase.Instance.Definitions
            .Where(IsObject).OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
        internal static bool IsObject(ObjectDefinition definition)
        {
            if (definition == null || definition.Guid.IsEmpty) return false;
            return definition.SharedConfigs.Any(value => value is EquipmentItemConfig || value is ActorRuleConfig ||
                value is BuildingRuleConfig || value is WorksiteRuleConfig || value is MineralDepositRuleConfig) ||
                IsProjectile(definition) || definition.Key == "effect.arrow";
        }
        internal static ObjectDefinition Equipment(string guid) => ObjectDefinitionDatabase.Instance.Definitions
            .FirstOrDefault(value => value != null && value.Guid.ToString() == guid);
        internal static EquipmentItemConfig Item(ObjectDefinition definition) => definition?.SharedConfigs.OfType<EquipmentItemConfig>().SingleOrDefault();
        internal static bool IsProjectile(ObjectDefinition definition) => definition != null && definition ==
            ObjectDefinitionDatabase.Instance.GetDefinitionByKey("effect.ballistic");
        internal static string Slot(ActorViewData actor, int slot) => actor == null ? "" : slot switch
        { 0 => actor.Slot0Definition, 1 => actor.Slot1Definition, 2 => actor.Slot2Definition, _ => actor.Slot3Definition };
        internal static string Status(ObjectDefinition definition)
        {
            if (Item(definition) != null) return "装备";
            if (IsProjectile(definition)) return "投射物";
            if (definition.Key == "effect.arrow") return "投射物 · 暂停";
            if (definition.BehaviourTypes.Contains(typeof(ActorBehaviour).FullName)) return "单位 · 受限";
            return "只读 / 暂停";
        }
        internal static List<int> Instances(WorldViewData world, ObjectDefinition definition)
        {
            if (world == null || definition == null) return new List<int>();
            if (IsProjectile(definition)) return world.Projectiles.Where(value => value.Kind != 0 && value.ViewId <= int.MaxValue)
                .Select(value => (int)value.ViewId).ToList();
            return world.Identities.Where(value => value.DefinitionGuid == definition.Guid.ToString()).Select(value => value.Id).ToList();
        }
    }
}
#endif
