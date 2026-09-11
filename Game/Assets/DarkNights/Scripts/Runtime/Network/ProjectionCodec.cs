using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>
    /// 用已锁定 MemoryPack 具体类型编码完整投影，限制单帧字节并在接收回调内校验、冻结。
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
            if (bytes.Length > MaximumBytes) throw new InvalidOperationException("Full projection exceeds supported byte limit.");
            return bytes;
        }

        public SessionViewData Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes)
                throw new FormatException("Invalid projection payload size.");
            SessionWire wire = null;
            int consumed = MemoryPackSerializer.Deserialize<SessionWire>(bytes.AsSpan(), ref wire);
            if (consumed != bytes.Length || wire == null) throw new FormatException("Invalid projection envelope.");
            ProjectionValidation.Validate(wire, catalog, layout);
            return wire.Freeze();
        }
    }
}
