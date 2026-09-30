using System;

namespace DarkNights.Core.ViewData
{
    /// <summary>单次主角输入冻结的采矿目标意图；地图身份和格内容只用于拒绝过期目标，不构成客户端授权。</summary>
    public readonly struct HeroMiningTarget : IEquatable<HeroMiningTarget>
    {
        public readonly string WorldId;
        public readonly ulong MapEpoch;
        public readonly uint TileId;
        public readonly int U, V;
        public readonly ushort Flags;
        public bool Present => !string.IsNullOrEmpty(WorldId);

        public HeroMiningTarget(string worldId, ulong mapEpoch, int u, int v, uint tileId, ushort flags)
        {
            WorldId = worldId; MapEpoch = mapEpoch; U = u; V = v; TileId = tileId; Flags = flags;
        }

        public bool Equals(HeroMiningTarget other) => WorldId == other.WorldId && MapEpoch == other.MapEpoch &&
            U == other.U && V == other.V && TileId == other.TileId && Flags == other.Flags;
        public override bool Equals(object value) => value is HeroMiningTarget other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(WorldId, MapEpoch, U, V, TileId, Flags);
    }
}
