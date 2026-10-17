using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// 巢穴生成步骤的只读输入适配，组合R1指定洞室用途的冻结差分与正式Core保护格。
    /// 私有数组不被生成步骤写入；矿脉、地形和保护身份在创建后保持不变。
    /// </summary>
    public sealed class MiningInput
    {
        private readonly byte[] materials;
        private readonly byte[] shapes;
        private readonly bool[] protection;
        private readonly HashSet<int> mineralCells;
        public string MapId { get; }
        public string Seed { get; }
        public IReadOnlyList<VeinInput> Veins { get; }

        public MiningInput(JsonElement source, bool[] protection, string roomId, string role)
        {
            MapId = source.GetProperty("mapId").GetString(); Seed = source.GetProperty("seed").GetString();
            materials = Decode(source.GetProperty("materials").GetString());
            shapes = Decode(source.GetProperty("shapes").GetString());
            this.protection = (bool[])protection.Clone();
            var veins = source.GetProperty("veins").EnumerateArray().Select(v => new VeinInput(v)).ToList();
            if (role != "ordinary")
            {
                var variant = source.GetProperty("variants").EnumerateArray().Single(v =>
                    v.GetProperty("roomId").GetString() == roomId && v.GetProperty("role").GetString() == role);
                var removed = new HashSet<string>(variant.GetProperty("removed").EnumerateArray().Select(v => v.GetString()));
                veins.RemoveAll(v => removed.Contains(v.Id));
                veins.AddRange(variant.GetProperty("added").EnumerateArray().Select(v => new VeinInput(v)));
            }
            Veins = veins.AsReadOnly(); mineralCells = new HashSet<int>(veins.SelectMany(v => v.Cells));
            if (this.protection.Length != materials.Length || mineralCells.Count != veins.Sum(v => v.Cells.Count))
                throw new ArgumentException("R1保护格尺寸或矿格互斥失效。");
        }

        public bool Carvable(int index) => index >= 0 && index < materials.Length &&
            materials[index] == 2 && !protection[index] && !mineralCells.Contains(index);
        public bool NaturalAir(int index) => index >= 0 && index < materials.Length && materials[index] == 0;
        public bool Mineral(int index) => mineralCells.Contains(index);
        public bool Protected(int index) => protection[index];
        public byte[] CopyMaterials() => (byte[])materials.Clone();
        public byte[] CopyShapes() => (byte[])shapes.Clone();
        public bool[] CopyProtection() => (bool[])protection.Clone();

        private static byte[] Decode(string runs)
        {
            var result = new byte[320 * 192]; int index = 0;
            foreach (string run in runs.Split(','))
            {
                string[] pair = run.Split(':'); byte value = byte.Parse(pair[0]); int count = int.Parse(pair[1]);
                if (count < 1 || count > result.Length - index) throw new ArgumentException("R1栅格连续段无效。");
                Array.Fill(result, value, index, count); index += count;
            }
            if (index != result.Length) throw new ArgumentException("R1栅格尺寸不完整。");
            return result;
        }
    }
}
