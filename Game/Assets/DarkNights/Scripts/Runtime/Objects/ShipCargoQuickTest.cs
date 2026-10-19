namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 显式开发快速局的三人货物初始条件，使用正式审核地图和正常对象工厂。
    /// 仅初始化一次初始货物／站位，后续转移、购买、航程、同步和保存均走真实命令与事务，不证明采矿路线通过。
    /// </summary>
    internal static class ShipCargoQuickTest
    {
        internal static void Apply(ObjectSession world)
        {
            var ship = world.Expedition.Ship.Edit();
            ship.CargoIron = world.Catalog.Balance.Expedition.ShipCapacity - 2;
            for (int slot = 0; slot < 3; slot++)
            {
                var actor = GroundSessionSetup.SpawnHero(world);
                var state = actor.Edit(); state.OwnerSlot = slot; state.ManualControl = true;
                if (slot < 2) world.Ship.Cabin.Place(actor, slot == 0 ? 96 : -40);
                if (slot == 1) { state.CargoIron = 3; state.CargoGold = 2; }
                actor.Object.GetBehaviour<HeroLightBehaviour>()?.GiveStarter();
            }
            world.Notify("交货快速测试：正式地图，三人初始站位；船货近满、队员携矿为显式测试初值。");
        }
    }
}
