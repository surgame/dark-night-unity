using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.State;
using DarkNights.Core.ViewData;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>出售与装备购买的权威短事务；模块身份由船上子物体定义，库存只写角色和经济 Behaviour。</summary>
    internal sealed class ShipTradeService
    {
        private const int MaximumCredits = 10000000;
        private readonly ObjectSession world;
        internal ShipTradeService(ObjectSession world) { this.world = world; }

        internal bool Near(ActorBehaviour hero, int shipId, string service)
        {
            if (!world.IsExpedition || hero == null || hero.Hp <= 0 || !hero.Read().Boarded ||
                world.Paused || world.Flow?.Phase is not (JourneyPhase.Orbit or JourneyPhase.Landed)) return false;
            var ship = world.Expedition.Ship;
            if (ship == null || ship.Id != shipId || ship.Read().PilotId != 0 ||
                ship.Read().ShipPhase is not (0 or 3) || ship.Read().ShipDoorClock > 0) return false;
            var anchors = ship.Object.GetComponentsInChildren<ShipServiceAnchor>(true);
            if (anchors.Length != 2) return false;
            var services = ship.Object.Definition.SharedConfigs.OfType<ShipServicesConfig>().SingleOrDefault();
            if (services == null) return false;
            services.Validate();
            DefinitionReference selected = service == "sale" ? services.Sale : services.Shop;
            if (service == "sale" ? !services.SaleEnabled : !services.ShopEnabled) return false;
            foreach (var anchor in anchors)
            {
                if (!anchor.gameObject.activeSelf || anchor.Definition.IsEmpty) continue;
                if (anchor.Definition.GuidString != selected.GuidString) continue;
                var config = anchor.Config;
                config?.Validate();
                if (config?.Service != service) continue;
                float localX = anchor.transform.localPosition.x * 100;
                float localY = anchor.transform.localPosition.y * 100;
                var actor = hero.Read(); var shipState = ship.Read();
                return Math.Abs(actor.X - ship.X - localX) <= config.Radius &&
                    Math.Abs(actor.Height - shipState.Height - localY) <= config.Radius;
            }
            return false;
        }

        internal int Sell(ActorBehaviour hero, int shipId, int expectedIron, int expectedGold)
        {
            return 0;
        }

        internal int Buy(ActorBehaviour hero, int shipId, string itemKey, int expectedRevision)
        {
            if (!Near(hero, shipId, "shop") || hero.Read().InventoryRevision != expectedRevision) return 0;
            ObjectDefinition definition = ObjectDefinitionDatabase.Instance?.GetDefinitionByKey("item." + itemKey);
            EquipmentItemConfig item = definition?.SharedConfigs.OfType<EquipmentItemConfig>().SingleOrDefault();
            if (item == null) return 0;
            item.Validate();
            if (definition.Guid.IsEmpty || definition.PrefabRef == null || world.Resources.Equipment.Resolve(definition.Guid.ToString()) != definition) return 0;
            var trade = world.Catalog.Balance.Expedition.Trade;
            int price = item.RuleKey switch
            {
                "pistol" => trade.PistolPrice,
                "pickaxe" => trade.PickaxePrice,
                "jetpack" => trade.JetpackPrice,
                _ => -1
            };
            int balance = world.Economy.Read().Credits;
            if (price < 0 || balance < price) return 0;
            var state = hero.Read();
            if (item.Jetpack ? state.JetpackOwned :
                Enumerable.Range(0, 4).Any(slot => HeroInventoryBehaviour.Slot(state, slot) == definition.Guid.ToString())) return 0;
            if (!item.Jetpack && state.Slot0 != "" && state.Slot1 != "" && state.Slot2 != "" && state.Slot3 != "") return 0;
            if (!HeroInventoryTransactions.Give(world, hero, definition))
                throw new InvalidOperationException("已验证的装备写入失败。");
            world.Economy.Edit().Credits = balance - price;
            world.Notify("已购买 " + definition.Name + "。");
            return 1;
        }
    }
}
