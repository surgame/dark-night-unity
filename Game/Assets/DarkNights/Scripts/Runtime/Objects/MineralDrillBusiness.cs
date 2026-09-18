namespace DarkNights.Runtime.Objects
{
    /// <summary>钻机业务入口；连接和权限由上层命令验证，本类只在一次会话事务中改变指定矿床。</summary>
    public static class MineralDrillBusiness
    {
        public static bool Start(MineralDepositBehaviour deposit, int drillId) => deposit != null && deposit.AttachDrill(drillId);
        public static bool Stop(MineralDepositBehaviour deposit, int drillId) => deposit != null && deposit.DetachDrill(drillId);
        public static int Tick(MineralDepositBehaviour deposit, double seconds) => deposit == null ? 0 : deposit.AdvanceDrill(seconds);
    }
}
