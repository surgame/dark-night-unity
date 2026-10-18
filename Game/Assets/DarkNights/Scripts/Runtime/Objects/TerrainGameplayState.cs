using System;
using GameCore.Objects.NetworkStates;
using MemoryPack;

namespace DarkNights.Runtime.Objects
{
    /// <summary>地形能力拥有的有界巢穴状态；不保存地形格子或矿量。复制值数组隔离事务、池与冻结投影。</summary>
    [MemoryPackable, StateData]
    public partial record TerrainGameplayState
    {
        public uint Sequence { get; set; }
        public NestCrawlerState[] Nests { get; internal set; } = Array.Empty<NestCrawlerState>();
        public void CopyFrom(IStateData source)
        {
            if (!(source is TerrainGameplayState state)) throw new ArgumentException("Expected terrain gameplay state.");
            Sequence = state.Sequence; Nests = (NestCrawlerState[])state.Nests.Clone();
        }
    }
}
