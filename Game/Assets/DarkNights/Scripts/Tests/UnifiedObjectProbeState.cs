using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Objects.NetworkStates;

namespace DarkNights.Tests
{
    /// <summary>装配回归的实例状态；列表始终深复制，归池清空自身，冻结快照不依赖池的生命周期。</summary>
    public sealed class UnifiedObjectProbeState : IStateData, IEquatable<UnifiedObjectProbeState>
    {
        public uint Sequence { get; set; }
        public int Number { get; set; }
        public List<int> Items { get; } = new List<int>();

        public void CopyFrom(IStateData source)
        {
            var state = (UnifiedObjectProbeState)source;
            Sequence = state.Sequence;
            Number = state.Number;
            Items.Clear();
            Items.AddRange(state.Items);
        }

        public void OnReturnToPool() { Sequence = 0; Number = 0; Items.Clear(); }
        public bool Equals(UnifiedObjectProbeState other) => other != null && Sequence == other.Sequence &&
            Number == other.Number && Items.SequenceEqual(other.Items);
        public override bool Equals(object other) => Equals(other as UnifiedObjectProbeState);
        public override int GetHashCode() => Number;
    }
}
