using DarkNights.Core.Logic.State;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 当前三夜夜袭的唯一阶段、倒计时与生成游标，属于会话对象而非展示副本。
    /// 所有字段按值冻结保存，载入后从同一敌人游标和间隔继续。
    /// </summary>
    [MemoryPackable, StateData]
    public partial record WaveState
    {
        public uint Sequence { get; set; }
        public int Index { get; internal set; }
        public WavePhase Phase { get; internal set; }
        public double DayRemaining { get; internal set; }
        public double SpawnElapsed { get; internal set; }
        public int NextSpawn { get; internal set; }
    }
}
