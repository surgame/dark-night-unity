using System;
using System.IO;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 防止解压器预读尾随数据后掩盖多余字节，每次只提供一个编码字节以保留确切消费位置。
    /// 输入受投影封套的 512 KiB 上限保护；只在同步解码期间引用数组，结束即释放。
    /// </summary>
    internal sealed class ProjectionInputStream : MemoryStream
    {
        internal ProjectionInputStream(byte[] packet) : base(packet, ProjectionPacket.HeaderBytes,
            packet.Length - ProjectionPacket.HeaderBytes, false)
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));
        public override int Read(Span<byte> buffer) => base.Read(buffer.Slice(0, Math.Min(buffer.Length, 1)));
    }
}
