using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// R2读取的冻结矿脉投影，身份、初始储量和占格来自保留的R1结果。
    /// 只参与开发端巢位选择，不结算矿物、不修改原矿脉或拥有运行进度。
    /// </summary>
    public sealed class VeinInput
    {
        public string Id { get; }
        public int Head { get; }
        public int Tier { get; }
        public int Capacity { get; }
        public int Room { get; }
        public bool Interior { get; }
        public IReadOnlyList<int> Cells { get; }

        public VeinInput(JsonElement value)
        {
            Id = value.GetProperty("id").GetString(); Head = value.GetProperty("head").GetInt32();
            Tier = value.GetProperty("tier").GetInt32(); Capacity = value.GetProperty("capacity").GetInt32();
            Room = value.GetProperty("room").GetInt32(); Interior = value.GetProperty("interior").GetBoolean();
            Cells = Array.AsReadOnly(value.GetProperty("cells").EnumerateArray().Select(v => v.GetInt32()).ToArray());
            if (Cells.Count < 1 || Cells.Count > 64 || Cells[0] != Head || Tier < 0 || Tier > 2)
                throw new ArgumentException("R1矿脉投影无效。");
        }
    }
}
