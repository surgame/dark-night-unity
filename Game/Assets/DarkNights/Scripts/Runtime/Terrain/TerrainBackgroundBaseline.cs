using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>单连接背景基线接收器；有界组装并校验后一次发布，换代丢弃旧块，当前地图绝不补造初始参考。</summary>
    public sealed class TerrainBackgroundBaseline
    {
        public const int ChunkBytes = 8192;
        private TerrainEpochSignal identity;
        private byte[] buffer;
        private bool[] received;
        private int receivedCount;
        public BackgroundBakeDescriptor Reference { get; private set; }
        public bool Ready { get; private set; }
        public string WorldId => identity.WorldId;
        public ulong MapEpoch => identity.MapEpoch;
        public int Epoch => identity.Epoch;
        public void Begin(TerrainEpochSignal signal)
        {
            if (signal.Epoch < 1 || signal.MapEpoch < 1 || !Guid.TryParseExact(signal.WorldId, "N", out _) ||
                signal.BackgroundBytes < 0 || signal.BackgroundBytes > BackgroundReferenceCodec.MaximumBytes ||
                string.IsNullOrWhiteSpace(signal.Seed) || signal.Seed.Length > 80)
                throw new FormatException("地图基线身份或背景预算无效。");
            identity = signal; Reference = null; Ready = signal.BackgroundBytes == 0;
            buffer = new byte[signal.BackgroundBytes];
            received = new bool[(buffer.Length + ChunkBytes - 1) / ChunkBytes]; receivedCount = 0;
        }
        public bool Accept(TerrainBackgroundChunk chunk)
        {
            if (chunk.Epoch != identity.Epoch || chunk.MapEpoch != identity.MapEpoch || chunk.WorldId != identity.WorldId) return false;
            if (Ready) return false;
            if (buffer == null || chunk.Index < 0 || chunk.Index >= received.Length || chunk.Bytes == null ||
                chunk.Bytes.Length != Math.Min(ChunkBytes, buffer.Length - chunk.Index * ChunkBytes))
                throw new FormatException("背景基线块长度或序号无效。");
            if (received[chunk.Index]) return false;
            Buffer.BlockCopy(chunk.Bytes, 0, buffer, chunk.Index * ChunkBytes, chunk.Bytes.Length);
            received[chunk.Index] = true;
            if (++receivedCount != received.Length) return false;
            var candidate = BackgroundReferenceCodec.Decode(buffer);
            if (candidate.WorldId != identity.WorldId || candidate.LayoutSeed != identity.Seed)
                throw new FormatException("背景参考与地图基线不匹配。");
            Reference = candidate; Ready = true; buffer = null; received = null;
            return true;
        }
        public void Reset() { identity = default; buffer = null; received = null; Reference = null; Ready = false; receivedCount = 0; }
    }
}
