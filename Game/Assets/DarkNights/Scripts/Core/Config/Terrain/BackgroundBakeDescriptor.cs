using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>初次生成后的不可变视觉参考；归所属世界保存并同步，仅供背景重建，不参与碰撞、采矿或权威格子修改。</summary>
    public sealed class BackgroundBakeDescriptor
    {
        public const int Version = 1;
        public const int RasterPixelsPerCell = 8;
        public const string GeneratorVersion = "contour-follow-static-1-unity-1";
        public const string StyleId = "h5-v16.1-warm-rock";
        public const string StyleContract = "8ppc;stone=4;outline=hybridB,OUTLINE-0921,3,20,2;base=92,70,50;near=58,24,72;middle=72,40,52;deep=48,10,68;soft=2;detail=DECOR-0922;mask=1f245aa86d05751adc0e9e582d4a7e68487ca746f3b5f5622328b8cca9c46012;row-down;shape-v1;straight-srgb";
        public static string StyleContentHash { get; } = Hash(Encoding.UTF8.GetBytes(GeneratorVersion + ":" + StyleContract));
        private readonly byte[] materials;
        private readonly byte[] shapes;
        public string WorldId { get; }
        public string LayoutSeed { get; }
        public string ReferenceHash { get; }
        public int Width => TerrainGenerationSettings.Width;
        public int Height => TerrainGenerationSettings.Height;

        public BackgroundBakeDescriptor(string worldId, string seed, byte[] materials, byte[] shapes, string expectedHash = null)
        {
            int count = TerrainGenerationSettings.Width * TerrainGenerationSettings.Height;
            if (!Guid.TryParseExact(worldId, "N", out _) || string.IsNullOrWhiteSpace(seed) || seed.Length > 80 ||
                materials == null || materials.Length != count || shapes == null || shapes.Length != count)
                throw new ArgumentException("初始背景参考身份或长度无效。");
            for (int i = 0; i < count; i++)
                if (materials[i] > 8 || shapes[i] > 12 || (materials[i] == 0 && shapes[i] != 0))
                    throw new ArgumentException("初始背景参考材料或坡形无效。");
            WorldId = worldId; LayoutSeed = seed;
            this.materials = (byte[])materials.Clone(); this.shapes = (byte[])shapes.Clone();
            var bytes = new List<byte>();
            bytes.AddRange(Encoding.UTF8.GetBytes("DNBG1|320|192|row-down|shape-v1|" + worldId + "|" + seed + "|" + StyleContentHash + "|"));
            bytes.AddRange(this.materials); bytes.AddRange(this.shapes);
            ReferenceHash = Hash(bytes.ToArray());
            if (expectedHash != null && !string.Equals(expectedHash, ReferenceHash, StringComparison.Ordinal))
                throw new ArgumentException("初始背景参考 SHA-256 不匹配。");
        }

        public byte Material(int x, int row) => materials[row * Width + x];
        public byte Shape(int x, int row) => shapes[row * Width + x];
        public byte[] CopyMaterials() => (byte[])materials.Clone();
        public byte[] CopyShapes() => (byte[])shapes.Clone();
        private static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
