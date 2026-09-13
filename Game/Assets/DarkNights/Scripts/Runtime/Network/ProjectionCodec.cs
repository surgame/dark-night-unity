using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 用已锁定 MemoryPack 具体类型编码完整投影，再装入有界压缩封套；接收时解封、校验并冻结。
    /// YYGC 状态只持有编码后的只写一次字节数组，替换及归池不会清空客户端已拥有的展示副本。
    /// </summary>
    public sealed class ProjectionCodec
    {
        public const int MaximumBytes = 524288;
        private readonly GameCatalog catalog;
        private readonly LevelLayout layout;

        public ProjectionCodec(GameCatalog catalog, LevelLayout layout)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public byte[] Encode(SessionViewData frame)
        {
            byte[] bytes = MemoryPackSerializer.Serialize(SessionWire.From(frame));
            return ProjectionPacket.Pack(bytes);
        }

        public SessionViewData Decode(byte[] bytes)
        {
            byte[] raw = ProjectionPacket.Unpack(bytes);
            SessionWire wire = null;
            int consumed = MemoryPackSerializer.Deserialize<SessionWire>(raw.AsSpan(), ref wire);
            if (consumed != raw.Length || wire == null) throw new FormatException("Invalid projection envelope.");
            ProjectionValidation.Validate(wire, catalog, layout);
            return wire.Freeze();
        }
    }
}
