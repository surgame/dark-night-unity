using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 完整投影的有界传输封套，记录格式、编码方式与未压缩长度；大帧仅在压缩更小时使用 GZip。
    /// 解码先限制分配，再验证压缩终点、校验和与精确长度；返回独立数组，不持有池化状态。
    /// </summary>
    public static class ProjectionPacket
    {
        public const int HeaderBytes = 9;
        // 装备及矿床字段增加后的完整容量帧；解压预算与线上封包预算分别限制。
        public const int MaximumBodyBytes = 786432;

        public static byte[] Pack(byte[] raw)
        {
            if (raw == null || raw.Length == 0 || raw.Length > MaximumBodyBytes)
                throw new InvalidOperationException("Full projection exceeds decoded byte limit: " + (raw?.Length ?? 0));
            byte mode = 0;
            byte[] body = raw;
            if (raw.Length >= 1024)
            {
                byte[] compressed = Compress(raw);
                if (compressed.Length < raw.Length)
                {
                    body = compressed;
                    mode = 1;
                }
            }
            if (body.Length > ProjectionCodec.MaximumBytes - HeaderBytes)
                throw new InvalidOperationException("Encoded projection exceeds transport byte limit.");
            var packet = new byte[HeaderBytes + body.Length];
            packet[0] = (byte)'D';
            packet[1] = (byte)'N';
            packet[2] = (byte)'P';
            packet[3] = 1;
            packet[4] = mode;
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(5, 4), raw.Length);
            Buffer.BlockCopy(body, 0, packet, HeaderBytes, body.Length);
            return packet;
        }

        public static byte[] Unpack(byte[] packet)
        {
            if (packet == null || packet.Length <= HeaderBytes || packet.Length > ProjectionCodec.MaximumBytes ||
                packet[0] != 'D' || packet[1] != 'N' || packet[2] != 'P' || packet[3] != 1 || packet[4] > 1)
                throw new FormatException("Invalid projection packet header.");
            int length = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(5, 4));
            if (length <= 0 || length > MaximumBodyBytes)
                throw new FormatException("Invalid uncompressed projection size.");
            int encoded = packet.Length - HeaderBytes;
            if (packet[4] == 0)
            {
                if (encoded != length) throw new FormatException("Invalid raw projection size.");
                var raw = new byte[length];
                Buffer.BlockCopy(packet, HeaderBytes, raw, 0, length);
                return raw;
            }
            // GZip 至少含 10 字节头和 8 字节尾；ISIZE 必须与封套相同，截断尾部不能被流视为正常 EOF。
            if (encoded < 18 || encoded >= length ||
                BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(packet.Length - 4, 4)) != (uint)length)
                throw new FormatException("Invalid compressed projection size.");
            return Expand(packet, length);
        }

        private static byte[] Compress(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Fastest, true))
                    gzip.Write(raw, 0, raw.Length);
                return output.ToArray();
            }
        }

        private static byte[] Expand(byte[] packet, int length)
        {
            try
            {
                using (var source = new ProjectionInputStream(packet))
                using (var gzip = new GZipStream(source, CompressionMode.Decompress, true))
                {
                    var raw = new byte[length];
                    int offset = 0;
                    while (offset < length)
                    {
                        int read = gzip.Read(raw, offset, length - offset);
                        if (read == 0) throw new FormatException("Truncated compressed projection.");
                        offset += read;
                    }
                    if (gzip.ReadByte() != -1 || source.Position != source.Length)
                        throw new FormatException("Trailing compressed projection bytes.");
                    return raw;
                }
            }
            catch (IOException error) { throw new FormatException("Invalid compressed projection.", error); }
        }
    }
}
