using System;
using AnyRules.Next.Networking;
using FishNet.Serializing;

namespace DarkNights.Runtime.Network
{
    /// <summary>矿层消息的有界 FishNet 编解码；只封装 AMP1 字节，不复制地图协议或写入业务状态。</summary>
    public static class MineralMapWireSerializers
    {
        public static void WriteMineralMapWireMessage(this Writer writer, MineralMapWireMessage value)
        {
            if (value.Bytes == null || value.Bytes.Length > ProtocolLimits.MaximumPacketBytes) throw new FormatException("矿层消息超限。");
            writer.WriteInt32(value.Bytes.Length); writer.WriteUInt8Array(value.Bytes, 0, value.Bytes.Length);
        }
        public static MineralMapWireMessage ReadMineralMapWireMessage(this Reader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > ProtocolLimits.MaximumPacketBytes || length > reader.Remaining)
            { reader.Skip(reader.Remaining); return default; }
            var segment = reader.ReadArraySegment(length);
            var bytes = new byte[length]; Buffer.BlockCopy(segment.Array, segment.Offset, bytes, 0, length);
            return new MineralMapWireMessage { Bytes = bytes };
        }
    }
}
