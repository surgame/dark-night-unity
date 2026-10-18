using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Experiments.NearWallMining
{
    /// <summary>
    /// 近壁样板的只读真实地形边界；将旧R1空格露头左邻的实岩作为新露头。
    /// 局部u向左入岩、v向下沿壁，禁止迁移主体或拉长连接来迁就空间不足。
    /// </summary>
    public sealed class NearWallSite
    {
        private readonly byte[] materials;
        private readonly byte[] shapes;
        private readonly bool[] protection;
        private readonly HashSet<int> otherMinerals;
        public const int Width = 320;
        public const int Height = 192;
        public string ReplacedVein => "r1-vein-45395";
        public int Head { get; }

        public NearWallSite(JsonElement mining, PlayableTerrain terrain)
        {
            materials = terrain.CopyMaterials(); shapes = terrain.CopyShapes(); protection = terrain.CopyProtection();
            var old = mining.GetProperty("veins").EnumerateArray().Single(v => v.GetProperty("id").GetString() == ReplacedVein);
            Head = old.GetProperty("head").GetInt32() - 1;
            otherMinerals = new HashSet<int>(mining.GetProperty("veins").EnumerateArray()
                .Where(v => v.GetProperty("id").GetString() != ReplacedVein)
                .SelectMany(v => v.GetProperty("cells").EnumerateArray().Select(c => c.GetInt32())));
            if (!Rock(Head) || !Air(Head + 1)) throw new InvalidOperationException("近壁露头不再接天然空腔。");
        }

        public int Map(int u, int v)
        {
            int x = Head % Width - u, y = Head / Width + v;
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
            if (y < Height - 1) yield return i + Width;
            if (y > 0) yield return i - Width;
            if (x < Width - 1) yield return i + 1;
        }
    }
}
