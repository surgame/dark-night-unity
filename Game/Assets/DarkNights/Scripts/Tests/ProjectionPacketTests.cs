using System;
using System.Buffers.Binary;
using System.Linq;
using DarkNights.Runtime.Network;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 覆盖压缩封套的有界分配、原始回退、完整性和严格终点，防止大包优化削弱网络输入校验。
    /// 使用独立字节数据检验封套；具体实体与规则的坏帧断言继续由 ProjectionWireTests 负责。
    /// </summary>
    public sealed class ProjectionPacketTests
    {
        [Test]
        public void RepetitivePayloadCompressesAndReturnsIndependentBytes()
        {
            var raw = Enumerable.Repeat((byte)65, 16384).ToArray();
            byte[] packet = ProjectionPacket.Pack(raw);
            Assert.That(packet[4], Is.EqualTo(1));
            Assert.That(packet.Length, Is.LessThan(raw.Length / 4));
            byte[] restored = ProjectionPacket.Unpack(packet);
            Assert.That(restored, Is.EqualTo(raw));
            raw[0] = 0; Array.Clear(packet, 0, packet.Length);
            Assert.That(restored[0], Is.EqualTo(65));
        }

        [Test]
        public void IncompressibleAndSmallPayloadsUseExactRawFrames()
        {
            var raw = new byte[4096];
            new Random(29101).NextBytes(raw);
            foreach (byte[] input in new[] { raw, new byte[] { 1, 2, 3 } })
            {
                byte[] packet = ProjectionPacket.Pack(input);
                Assert.That(packet[4], Is.Zero);
                Assert.That(packet.Length, Is.EqualTo(input.Length + ProjectionPacket.HeaderBytes));
                Assert.That(ProjectionPacket.Unpack(packet), Is.EqualTo(input));
                Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(packet.Concat(new byte[] { 0 }).ToArray()));
            }
        }

        [Test]
        public void HeaderAndAllocationBoundsAreCheckedBeforeExpansion()
        {
            byte[] valid = ProjectionPacket.Pack(Enumerable.Repeat((byte)17, 16384).ToArray());
            foreach (int offset in new[] { 0, 3, 4 })
            {
                byte[] changed = (byte[])valid.Clone(); changed[offset] = 255;
                Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(changed));
            }
            foreach (int length in new[] { 0, -1, ProjectionPacket.MaximumBodyBytes + 1, int.MaxValue })
            {
                byte[] changed = (byte[])valid.Clone();
                BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(5, 4), length);
                Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(changed));
            }
            Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(null));
            Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(new byte[ProjectionCodec.MaximumBytes + 1]));
            Assert.Throws<InvalidOperationException>(() => ProjectionPacket.Pack(new byte[ProjectionPacket.MaximumBodyBytes + 1]));
        }

        [Test]
        public void TruncatedAndCorruptedGzipCannotBecomeAValidProjection()
        {
            byte[] valid = ProjectionPacket.Pack(Enumerable.Repeat((byte)41, 16384).ToArray());
            for (int removed = 1; removed < valid.Length; removed++)
                Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(valid.Take(valid.Length - removed).ToArray()),
                    "Missing suffix bytes: " + removed);
            byte[] corrupted = (byte[])valid.Clone();
            corrupted[corrupted.Length - 8] ^= 1;
            Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(corrupted), "CRC corruption must fail.");
        }

        [Test]
        public void TrailingBytesAndConcatenatedGzipMembersAreRejected()
        {
            byte[] valid = ProjectionPacket.Pack(Enumerable.Repeat((byte)51, 16384).ToArray());
            byte[] footer = valid.Skip(valid.Length - 8).ToArray();
            Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(valid.Concat(footer).ToArray()));
            Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(valid.Concat(valid.Skip(ProjectionPacket.HeaderBytes)).ToArray()));
        }

        [Test]
        public void DeclaredExpansionMustMatchExactlyAndCannotHideExtraOutput()
        {
            byte[] valid = ProjectionPacket.Pack(Enumerable.Repeat((byte)61, 16384).ToArray());
            foreach (int length in new[] { 1, 16383, 16385, ProjectionPacket.MaximumBodyBytes })
            {
                byte[] changed = (byte[])valid.Clone();
                BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(5, 4), length);
                BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(changed.Length - 4, 4), length);
                Assert.Throws<FormatException>(() => ProjectionPacket.Unpack(changed));
            }
        }
    }
}
