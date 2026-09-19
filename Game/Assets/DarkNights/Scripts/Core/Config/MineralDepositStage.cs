namespace DarkNights.Core.Config
{
    /// <summary>矿床运行阶段合同；运行时对象只写此值，地形网格和客户端副本不拥有采集进度。</summary>
    public enum MineralDepositStage
    {
        Available = 0,
        Depleted = 1
    }
}
