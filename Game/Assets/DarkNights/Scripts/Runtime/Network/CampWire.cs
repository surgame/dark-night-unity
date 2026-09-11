using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// CampViewData 的 MemoryPack 具体 wire 类型，仅负责传输字段；发送后不修改，接收立即冻结后交给展示层。
    /// 可变实例不属于客户端世界，不能跨状态池回调保留；字段顺序变更必须升级握手协议。
    /// </summary>
    [MemoryPackable]
    public partial class CampWire
    {
        public ResourcesWire Stock { get; set; }
        public int Population { get; set; }
        public int Capacity { get; set; }
        public double RecruitCooldown { get; set; }
        public int WaveIndex { get; set; }
        public string WavePhase { get; set; }
        public double DayRemaining { get; set; }
        public int EnemyCount { get; set; }
        public string Mode { get; set; }
        public int Kills { get; set; }
        public int Lost { get; set; }
        public ResourcesWire Gathered { get; set; }
        public int NextSpawn { get; set; }

        public static CampWire From(CampViewData value) => new CampWire
        {
            Stock = ResourcesWire.From(value.Stock),
            Population = value.Population,
            Capacity = value.Capacity,
            RecruitCooldown = value.RecruitCooldown,
            WaveIndex = value.WaveIndex,
            WavePhase = value.WavePhase,
            DayRemaining = value.DayRemaining,
            EnemyCount = value.EnemyCount,
            Mode = value.Mode,
            Kills = value.Kills,
            Lost = value.Lost,
            Gathered = ResourcesWire.From(value.Gathered),
            NextSpawn = value.NextSpawn,
        };

        public CampViewData Freeze() => new CampViewData(
            Stock?.Freeze(),
            Population,
            Capacity,
            RecruitCooldown,
            WaveIndex,
            WavePhase,
            DayRemaining,
            EnemyCount,
            Mode,
            Kills,
            Lost,
            Gathered?.Freeze(), NextSpawn);
    }
}
