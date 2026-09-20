using System;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话对象独占的在飞箭矢集合和表现身份序号，客户端不从它推进命中。
    /// 每次状态复制克隆值数组；冻结、草稿与池归还互不影响。
    /// </summary>
    [MemoryPackable, StateData]
    public partial record ProjectileState
    {
        public uint Sequence { get; set; }
        public long NextViewId { get; internal set; } = 1;
        public ProjectileFlight[] Shots { get; internal set; } = Array.Empty<ProjectileFlight>();

        public BallisticFlight[] Ballistics { get; internal set; } = new BallisticFlight[HandheldConfig.PoolCapacity];

        public void CopyFrom(IStateData source)
        {
            if (!(source is ProjectileState value)) throw new ArgumentException("Expected projectile state.", nameof(source));
            Sequence = value.Sequence;
            if (Ballistics == null || Ballistics.Length != HandheldConfig.PoolCapacity) Ballistics = new BallisticFlight[HandheldConfig.PoolCapacity];
            Array.Copy(value.Ballistics, Ballistics, HandheldConfig.PoolCapacity);
            NextViewId = value.NextViewId;
            Shots = value.Shots == null ? Array.Empty<ProjectileFlight>() : (ProjectileFlight[])value.Shots.Clone();
        }
    }
}
