using System;
using System.Linq;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Objects
{
    /// <summary>船货入舱和安全交货的短事务步骤；货物归原角色／船体State，信用点归既有Economy。</summary>
    internal sealed class ShipCargoService
    {
        private readonly ObjectSession world;
        internal ShipCargoService(ObjectSession world) { this.world = world; }

        internal int Deposit(ActorBehaviour hero)
        {
            if (world.Flow.Phase != JourneyPhase.Landed ||
                !world.Trade.Near(hero, world.Expedition.Ship.Id, "sale")) return 0;
            int count = ExpeditionCargo.Transfer(hero, world.Expedition.Ship, world.Catalog.Balance.Expedition.ShipCapacity);
            if (count > 0) world.Notify("已入舱 " + count + " 单位矿物；撤离矿区后自动换取信用点。");
            return count;
        }

        internal long Income()
        {
            var ship = world.Expedition.Ship.Read();
            int iron = ship.CargoIron, gold = ship.CargoGold;
            foreach (var actor in world.Index.Actors.Where(a => !a.Enemy && a.Read().Boarded))
            { iron += actor.Read().CargoIron; gold += actor.Read().CargoGold; }
            return world.Catalog.Balance.Expedition.Trade.SaleValue(iron, gold);
        }

        internal bool CanSettle => Income() + world.Economy.Read().Credits <= 10000000;

        internal void Settle()
        {
            var j = world.Journey.Edit();
            if (j.RunId.Length == 0 || j.SettledRunId == j.RunId) return;
            long income = Income();
            if (income + world.Economy.Read().Credits > 10000000)
                throw new InvalidOperationException("共享余额不足以完整接收货物收入。");
            var ship = world.Expedition.Ship.Edit(); ship.CargoIron = ship.CargoGold = 0;
            foreach (var actor in world.Index.Actors.Where(a => !a.Enemy && a.Read().Boarded))
            { var a = actor.Edit(); a.CargoIron = a.CargoGold = 0; }
            world.Economy.Edit().Credits += (int)income;
            j.SettledRunId = j.RunId; j.LastIncome = (int)income;
            world.Notify("矿业回收已完成，本次收入 ＋" + income + " 共享信用点。");
        }
    }
}
