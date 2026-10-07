using System;
namespace DarkNights.Runtime.Objects
{
    /// <summary>货物在所属角色与设备之间的事务转移；只扣实际接收量，满容量不会销毁来源。</summary>
    internal static class ExpeditionCargo
    {
        internal static bool CanMine(ActorBehaviour actor) => !actor.World.IsExpedition ||
            actor.World.Expedition.Active && !actor.Read().Boarded;
        internal static bool CanCollect(ActorBehaviour actor, int amount) => amount >= 0 && (!actor.World.IsExpedition ||
            actor.Read().CargoIron + actor.Read().CargoGold + amount <= actor.World.Catalog.Balance.Expedition.BagCapacity);
        internal static void Collect(ActorBehaviour actor, string resource, int amount = 1)
        {
            if (amount == 0) return;
            if (amount < 0 || resource != "iron" && resource != "gold" || !CanCollect(actor, amount))
                throw new InvalidOperationException("采集资源或接收容量不合法。");
            if (!actor.World.IsExpedition) { actor.World.Economy.AddResource(resource, amount); return; }
            var state = actor.Edit(); if (resource == "gold") state.CargoGold += amount; else state.CargoIron += amount;
        }
        internal static int Transfer(ActorBehaviour actor, BuildingBehaviour device, int capacity)
        {
            var a = actor.Edit(); var b = device.Edit();
            int room = Math.Max(0, capacity - b.CargoIron - b.CargoGold);
            int iron = Math.Min(room, a.CargoIron), gold = Math.Min(room - iron, a.CargoGold);
            a.CargoIron -= iron; a.CargoGold -= gold; b.CargoIron += iron; b.CargoGold += gold;
            return iron + gold;
        }
        internal static int Load(BuildingBehaviour device, ActorBehaviour actor, int capacity)
        {
            var a = actor.Edit(); var b = device.Edit();
            int room = Math.Max(0, capacity - a.CargoIron - a.CargoGold);
            int iron = Math.Min(room, b.CargoIron), gold = Math.Min(room - iron, b.CargoGold);
            b.CargoIron -= iron; b.CargoGold -= gold; a.CargoIron += iron; a.CargoGold += gold;
            return iron + gold;
        }
    }
}
