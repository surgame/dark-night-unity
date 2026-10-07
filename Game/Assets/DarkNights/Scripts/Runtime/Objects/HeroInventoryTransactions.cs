using System;
using System.Linq;
using DarkNights.Core.Config;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>商店与调试入口共用的库存短事务；只修改所属 ActorState，支付与开发者权限由调用方先行核验。</summary>
    internal static class HeroInventoryTransactions
    {
        internal static bool Give(ObjectSession world, ActorBehaviour hero, ObjectDefinition definition, int quantity = 1)
        {
            var item = definition?.SharedConfigs.OfType<EquipmentItemConfig>().SingleOrDefault();
            if (item == null || hero == null || quantity < 1 || quantity > 100) return false;
            string guid = definition.Guid.ToString();
            if (world.Resources.Equipment.Resolve(guid) != definition || definition.PrefabRef == null) return false;
            item.Validate(); var current = hero.Read();
            if (item.Jetpack)
            {
                if (current.JetpackOwned || quantity != 1) return false;
                var state = hero.Edit(); state.JetpackOwned = state.JetpackEquipped = true;
                state.JetpackFuel = world.Catalog.Balance.HeroControl.FuelSeconds;
                state.InventoryRevision = checked(state.InventoryRevision + 1); return true;
            }
            if (!world.Resources.Equipment.CanEquip(guid)) return false;
            bool bomb = item.Handheld == HeroEquipmentKind.Bomb;
            bool owned = Enumerable.Range(0, 4).Any(slot => HeroInventoryBehaviour.Slot(current, slot) == guid);
            if (!bomb && (owned || quantity != 1)) return false;
            if (!owned && Enumerable.Range(0, 4).All(slot => HeroInventoryBehaviour.Slot(current, slot) != "")) return false;
            if (bomb && current.ExplosiveCharges > 1000 - quantity) return false;
            var edited = hero.Edit();
            if (!owned && !HeroInventoryBehaviour.Give(edited, guid)) return false;
            if (owned) edited.InventoryRevision = checked(edited.InventoryRevision + 1);
            if (bomb) edited.ExplosiveCharges += quantity;
            return true;
        }
        internal static bool Remove(ObjectSession world, ActorBehaviour hero, ObjectDefinition definition)
        {
            var item = definition?.SharedConfigs.OfType<EquipmentItemConfig>().SingleOrDefault();
            if (item == null || hero == null) return false;
            string guid = definition.Guid.ToString(); var current = hero.Read();
            if (item.Jetpack)
            {
                if (!current.JetpackOwned) return false;
                var state = hero.Edit(); state.JetpackOwned = state.JetpackEquipped = false;
                state.JetpackFuel = 0; state.InventoryRevision = checked(state.InventoryRevision + 1); return true;
            }
            int slot = Enumerable.Range(0, 4).Where(value => HeroInventoryBehaviour.Slot(current, value) == guid).DefaultIfEmpty(-1).First();
            if (slot < 0) return false;
            var edited = hero.Edit();
            if (slot == 0) edited.Slot0 = ""; else if (slot == 1) edited.Slot1 = "";
            else if (slot == 2) edited.Slot2 = ""; else edited.Slot3 = "";
            if (item.Handheld == HeroEquipmentKind.Bomb) edited.ExplosiveCharges = 0;
            if (edited.SelectedItem == slot)
            {
                HeroEquipment.Cancel(edited); edited.EquipmentAction = 0;
                edited.SelectionRevision = checked(edited.SelectionRevision + 1);
                world.Mutations.AfterCommit(() => hero.Object.GetBehaviour<HeroInventoryBehaviour>().MiningTool());
            }
            edited.InventoryRevision = checked(edited.InventoryRevision + 1); return true;
        }
    }
}
