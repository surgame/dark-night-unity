using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>
    /// 区域矿物的有限预分配；抽样只依赖根种子、内容域及区域号，不依赖前区几何尝试是否成功。
    /// 请求槽位在雕刻前分配，失败不向其他区域转移预算，槽位也提供本图稳定的侧巢编号。
    /// </summary>
    internal sealed class CaveRegionAllocation
    {
        internal int[] Order { get; }
        internal bool[] Empty { get; }
        internal bool[] Rich { get; }
        internal List<int>[] Slots { get; }
        internal TerrainRandom[] Random { get; }

        internal CaveRegionAllocation(TerrainGenerationSettings settings, CaveRulePlan plan)
        {
            int count = plan.Rooms.Count;
            var rules = settings.Minerals;
            string root = settings.Seed + "|" + rules.ContentKey;
            Order = Enumerable.Range(0, count).ToArray();
            var order = new TerrainRandom(root + "|allocation-v2");
            for (int i = count - 1; i > 0; i--)
            { int j = (int)(order.Next() * (i + 1)), value = Order[i]; Order[i] = Order[j]; Order[j] = value; }
            Empty = new bool[count]; Rich = new bool[count];
            Slots = new List<int>[count]; Random = new TerrainRandom[count];
            for (int i = 0; i < count; i++)
            {
                var classification = new TerrainRandom(root + "|classification-v2|" + i);
                Empty[i] = i != 0 && classification.Next() < rules.EmptyShare;
                Rich[i] = i != 0 && classification.Next() < rules.RichShare;
                Slots[i] = new List<int>();
                Random[i] = new TerrainRandom(root + "|region-v2|" + i);
            }
            var eligible = Order.Where(i => !Empty[i]).ToArray();
            for (int i = 0; i < rules.VeinCount; i++) Slots[eligible[i % eligible.Length]].Add(i);
        }
    }
}
