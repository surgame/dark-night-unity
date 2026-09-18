namespace DarkNights.Runtime.Objects
{
    /// <summary>钻机业务入口；钻机必须是索引中的真实 Worksite 对象，产出先进入该对象的缓冲区。</summary>
    public static class MineralDrillBusiness
    {
        public static bool Start(MineralDepositBehaviour deposit, WorksiteBehaviour drill) =>
            deposit != null && drill != null && drill.IsMineralDrill && deposit.AttachDrill(drill.Id);

        public static bool Stop(MineralDepositBehaviour deposit, WorksiteBehaviour drill) =>
            deposit != null && drill != null && drill.IsMineralDrill && deposit.DetachDrill(drill.Id);

        public static int Tick(MineralDepositBehaviour deposit, WorksiteBehaviour drill, double seconds)
        {
            if (deposit == null || drill == null || !drill.IsMineralDrill || deposit.DrillId != drill.Id) return 0;
            int extracted = deposit.AdvanceDrill(seconds);
            if (extracted > 0) drill.BufferOutput(extracted);
            return extracted;
        }

        // Kept as a hard-fail compatibility boundary so arbitrary integers cannot become drills.
        public static bool Start(MineralDepositBehaviour deposit, int drillId) => false;
        public static bool Stop(MineralDepositBehaviour deposit, int drillId) => false;
    }
}
