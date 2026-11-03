using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 诊断片段中一次实际地形变化后的完整冻结地图；绑定完成该变化的物理步号与真实revision。
    /// 只在版本变化时捕获，片段限制16份，避免依赖已退休地图或无界保存全图。
    /// </summary>
    public sealed class PhysicsTerrainTrace
    {
        public long Tick { get; }
        public ulong Revision { get; }
        public PlayableTerrain Terrain { get; }
        public PhysicsTerrainTrace(long tick, ulong revision, PlayableTerrain terrain)
        { Tick = tick; Revision = revision; Terrain = terrain; }
    }
}
