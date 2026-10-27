using System;
using System.IO;
using System.Security.Cryptography;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 同版地图的可重建白模属性包；仅存材料、坡形、保护和矿种索引，不参与生成、编辑或业务装载。
    /// 来源与载荷摘要同时校验，损坏或旧版本回退完整候选；只有显式保存地图才写入新派生目录。
    /// </summary>
    internal static class TerrainDesignCache
    {
        internal const int Width = TerrainGenerationSettings.Width;
        internal const int Height = TerrainGenerationSettings.Height;
        private const int Magic = 0x31444E44;
        private const int Version = 1;
        internal const int ByteCount = Width * Height * 4;

        internal static byte[] Build(PlayableTerrain map)
        {
            var cells = map.CopyMaterials(); var shapes = map.CopyShapes();
            var protection = map.CopyProtection(); var soft = map.CopySoftRock();
            var result = new byte[ByteCount];
            for (int i = 0; i < cells.Length; i++)
            {
                result[i * 4] = cells[i]; result[i * 4 + 1] = shapes[i];
                result[i * 4 + 2] = (byte)((protection[i] ? 1 : 0) | (soft[i] ? 2 : 0));
                if (map.Minerals != null) result[i * 4 + 3] = map.Minerals.Kind(i);
            }
            if (map.Minerals == null)
                foreach (var deposit in map.Deposits)
                {
                    byte kind = (byte)(Array.IndexOf(FrozenMineralRules.Keys, deposit.MineralKind) + 1);
                    foreach (var cell in deposit.Cells) result[(-cell.V * Width + cell.U) * 4 + 3] = kind;
                }
            return result;
        }

        internal static byte[] Read(TextAsset asset, string sourceHash)
        {
            if (asset == null || string.IsNullOrEmpty(sourceHash)) return null;
            try
            {
                var bytes = asset.bytes;
                if (bytes.Length > ByteCount + 256) return null;
                using (var stream = new MemoryStream(bytes))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version ||
                        reader.ReadInt32() != Width || reader.ReadInt32() != Height ||
                        reader.ReadInt32() != PlayableTerrain.CellPixels || reader.ReadSingle() != PlayableTerrain.OriginY ||
                        reader.ReadString() != sourceHash) return null;
                    byte[] expected = reader.ReadBytes(32), pixels = reader.ReadBytes(ByteCount);
                    if (pixels.Length != ByteCount || stream.Position != stream.Length) return null;
                    using (var hash = SHA256.Create())
                        if (!Equal(expected, hash.ComputeHash(pixels))) return null;
                    for (int i = 0; i < pixels.Length; i += 4)
                        if (pixels[i] > 8 || pixels[i + 1] > 12 || pixels[i + 2] > 3 || pixels[i + 3] > 5 ||
                            pixels[i] == 0 && pixels[i + 1] != 0) return null;
                    return pixels;
                }
            }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }
            catch (FormatException) { return null; }
        }

        internal static TextAsset Save(string folder, PlayableTerrain map, string sourceHash)
        {
            byte[] pixels = Build(map);
            string path = folder + "/design-preview.bytes";
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            using (var hash = SHA256.Create())
            {
                writer.Write(Magic); writer.Write(Version); writer.Write(Width); writer.Write(Height);
                writer.Write(PlayableTerrain.CellPixels); writer.Write(PlayableTerrain.OriginY);
                writer.Write(sourceHash); writer.Write(hash.ComputeHash(pixels)); writer.Write(pixels);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<TextAsset>(path) ??
                throw new IOException("设计预览包导入失败，未更新地图文件。");
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            int difference = 0;
            for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }
}
