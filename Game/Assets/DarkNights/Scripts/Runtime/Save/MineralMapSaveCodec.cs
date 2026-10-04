using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using DarkNights.Core.Config.Terrain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkNights.Runtime.Save
{
    /// <summary>矿层 v19 紧凑保存编码；固定行序明确保存空格，解压有界、拒绝尾随字节，静态矿床标记不挤占实体预算。</summary>
    internal static class MineralMapSaveCodec
    {
        private const int Cells = TerrainGenerationSettings.Width * TerrainGenerationSettings.Height;
        internal static JToken Write(MineralMapSnapshot data)
        {
            if (data == null) return JValue.CreateNull();
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
            {
                writer.Write(0x314D4E44); writer.Write(data.RulesFingerprint); writer.Write(data.Count);
                for (int i = 0; i < data.Count; i++) { writer.Write(data.Kind(i)); writer.Write(data.Durability(i)); writer.Write(data.Reserves(i)); }
            }
            return new JValue(Pack(bytes.ToArray()));
        }
        internal static MineralMapSnapshot Read(JToken token)
        {
            if (token?.Type == JTokenType.Null) return null;
            if (token?.Type != JTokenType.String) throw new FormatException("矿层保存不是紧凑字符串。");
            byte[] bytes = Unpack((string)token, Cells * 9 + 128);
            using var input = new MemoryStream(bytes); using var reader = new BinaryReader(input, Encoding.UTF8);
            if (reader.ReadInt32() != 0x314D4E44) throw new FormatException("矿层保存编码不受支持。");
            string fingerprint = reader.ReadString();
            if (reader.ReadInt32() != Cells) throw new FormatException("矿层保存尺寸不匹配。");
            var kinds = new byte[Cells]; var hp = new int[Cells]; var reserves = new int[Cells];
            for (int i = 0; i < Cells; i++) { kinds[i] = reader.ReadByte(); hp[i] = reader.ReadInt32(); reserves[i] = reader.ReadInt32(); }
            if (input.Position != input.Length) throw new FormatException("矿层保存包含尾随数据。");
            return new MineralMapSnapshot(kinds, hp, reserves, fingerprint);
        }
        internal static string WriteMetadata(JArray data) => Pack(Encoding.UTF8.GetBytes(data.ToString(Formatting.None)));
        internal static JArray ReadMetadata(JToken token)
        {
            if (token?.Type != JTokenType.String) throw new FormatException("矿层标记缺少紧凑编码。");
            return JArray.Parse(new UTF8Encoding(false, true).GetString(Unpack((string)token, 16 * 1024 * 1024)));
        }
        private static string Pack(byte[] source)
        {
            using var output = new MemoryStream();
            using (var zip = new GZipStream(output, CompressionLevel.Optimal, true)) zip.Write(source, 0, source.Length);
            return Convert.ToBase64String(output.ToArray());
        }
        private static byte[] Unpack(string value, int maximum)
        {
            if (value.Length > 3000000) throw new FormatException("矿层编码超过预算。");
            using var input = new MemoryStream(Convert.FromBase64String(value));
            using var zip = new GZipStream(input, CompressionMode.Decompress); using var output = new MemoryStream();
            var buffer = new byte[8192]; int read;
            while ((read = zip.Read(buffer, 0, buffer.Length)) != 0)
            {
                if (output.Length + read > maximum) throw new FormatException("矿层解压超过预算。");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
    }
}
