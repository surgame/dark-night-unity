using System;
using System.IO;
using System.Text;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>保存和可靠基线共用的有界初始参考封套；成对 RLE 编码材料与坡形，完整校验版本、样式及哈希后才交出只读合同。</summary>
    public static class BackgroundReferenceCodec
    {
        public const int MaximumBytes = 250000;
        public static byte[] Encode(BackgroundBakeDescriptor source)
        {
            if (source == null) return Array.Empty<byte>();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(BackgroundBakeDescriptor.Version);
            writer.Write(source.WorldId); writer.Write(source.LayoutSeed);
            writer.Write(BackgroundBakeDescriptor.StyleContentHash); writer.Write(source.ReferenceHash);
            byte[] cells = source.CopyMaterials(), shapes = source.CopyShapes();
            for (int i = 0; i < cells.Length;)
            {
                int end = i + 1;
                while (end < cells.Length && end - i < ushort.MaxValue && cells[end] == cells[i] && shapes[end] == shapes[i]) end++;
                writer.Write((ushort)(end - i)); writer.Write(cells[i]); writer.Write(shapes[i]); i = end;
            }
            writer.Flush();
            if (stream.Length > MaximumBytes) throw new FormatException("背景参考超出传输预算。");
            return stream.ToArray();
        }
        public static BackgroundBakeDescriptor Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 4 || bytes.Length > MaximumBytes) throw new FormatException("背景参考包长度无效。");
            try
            {
                using var stream = new MemoryStream(bytes, false);
                using var reader = new BinaryReader(stream, new UTF8Encoding(false, true));
                if (reader.ReadInt32() != BackgroundBakeDescriptor.Version) throw new FormatException("未知背景参考版本。");
                string world = reader.ReadString(), seed = reader.ReadString(), style = reader.ReadString(), hash = reader.ReadString();
                if (style != BackgroundBakeDescriptor.StyleContentHash || hash.Length != 64) throw new FormatException("未知背景样式或校验值。");
                var cells = new byte[TerrainGenerationSettings.Width * TerrainGenerationSettings.Height];
                var shapes = new byte[cells.Length];
                for (int i = 0; i < cells.Length;)
                {
                    int count = reader.ReadUInt16(); byte material = reader.ReadByte(), shape = reader.ReadByte();
                    if (count == 0 || count > cells.Length - i) throw new FormatException("背景参考 RLE 越界。");
                    for (int n = 0; n < count; n++, i++) { cells[i] = material; shapes[i] = shape; }
                }
                if (stream.Position != stream.Length) throw new FormatException("背景参考含尾随数据。");
                return new BackgroundBakeDescriptor(world, seed, cells, shapes, hash);
            }
            catch (Exception error) when (error is ArgumentException || error is IOException)
            { throw new FormatException("初始背景参考损坏。", error); }
        }
    }
}
