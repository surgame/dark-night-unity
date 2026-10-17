using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Experiments.MiningArchetypes
{
    /// <summary>
    /// 同一真实露头的离线对比输入；冻结前景、坡形、保护格和其他矿脉占格。
    /// 只允许候选替换指定矿脉，不修改原图、其他矿层、运行世界或输入数组。
    /// </summary>
    public sealed class ArchetypeSite
    {
        private readonly byte[] materials;
        private readonly byte[] shapes;
        private readonly bool[] protection;
        private readonly HashSet<int> otherMinerals;
        public const int Width = 320;
        public const int Height = 192;
        public int Head { get; }
        public int Dx { get; }
        public int Dy { get; }
        public string ReplacedVein => "r1-vein-45395";

        public ArchetypeSite(JsonElement mining, PlayableTerrain terrain)
        {
            materials = terrain.CopyMaterials(); shapes = terrain.CopyShapes(); protection = terrain.CopyProtection();
            var vein = mining.GetProperty("veins").EnumerateArray().Single(v => v.GetProperty("id").GetString() == ReplacedVein);
            Head = vein.GetProperty("head").GetInt32();
            int first = vein.GetProperty("cells")[1].GetInt32();
            Dx = first % Width - Head % Width; Dy = first / Width - Head / Width;
            if (Math.Abs(Dx) + Math.Abs(Dy) != 1) throw new ArgumentException("真实露头法线无效。");
            otherMinerals = new HashSet<int>(mining.GetProperty("veins").EnumerateArray()
                .Where(v => v.GetProperty("id").GetString() != ReplacedVein)
                .SelectMany(v => v.GetProperty("cells").EnumerateArray().Select(c => c.GetInt32())));
        }

        public int Map(int u, int v)
        {
            // 共用弯折细脉进入露头左侧厚围岩，局部u朝下、v朝左；原露头不移动。
            int x = Head % Width - 14 - v, y = Head / Width + 2 + u;
            if (x < 1 || x >= Width - 1 || y < 33 || y >= Height - 1) return -1;
            return y * Width + x;
        }

        public bool Rock(int i) => i >= 0 && i < materials.Length && materials[i] == 2 &&
            !protection[i] && !otherMinerals.Contains(i);
        public bool Air(int i) => i >= 0 && i < materials.Length && materials[i] == 0;
        public bool OtherMineral(int i) => otherMinerals.Contains(i);
        public byte[] Materials() => (byte[])materials.Clone();
        public byte[] Shapes() => (byte[])shapes.Clone();
        public bool[] Protection() => (bool[])protection.Clone();

        public static IEnumerable<int> Neighbors(int i)
        {
            int x = i % Width, y = i / Width;
            if (x > 0) yield return i - 1;
            if (x < Width - 1) yield return i + 1;
            if (y > 0) yield return i - Width;
            if (y < Height - 1) yield return i + Width;
        }
    }
}
