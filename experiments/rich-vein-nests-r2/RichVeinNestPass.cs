using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Experiments.RichVeinNests
{
    /// <summary>
    /// 独立富矿伴巢步骤；只读接收R1矿脉和地形，按深浅抽样及模板约束返回新候选补丁。
    /// 不消费R1随机流、不改矿量；无效位置有界拒绝，开关关闭严格还原输入的地形／坡形。
    /// </summary>
    public sealed class RichVeinNestPass
    {
        public NestPassResult Apply(MiningInput input, NestPlacementSettings settings)
        {
            if (input == null || settings == null) throw new ArgumentNullException();
            var placements = new List<NestPlacement>(); var decisions = new List<NestDecision>();
            var reserved = new HashSet<int>(); var planner = new NestPocketPlanner(input, settings);
            var veins = input.Veins.Where(settings.Eligible).OrderByDescending(v => v.Tier)
                .ThenBy(v => Hash(input.Seed + "|nest-order-v1|" + v.Id));
            foreach (var vein in veins)
            {
                double probability = settings.Probability(vein.Tier);
                double roll = Hash(input.Seed + "|rich-nest-v1|" + vein.Id) / 4294967296.0;
                string reason = "disabled";
                if (settings.Enabled)
                {
                    if (roll >= probability) reason = "roll-empty";
                    else if (placements.Count >= settings.MaximumNests) reason = "budget";
                    else
                    {
                        var result = planner.TryPlan(vein, placements, reserved);
                        reason = result == null ? "space-or-spacing" : "placed";
                        if (result != null)
                        {
                            placements.Add(result);
                            foreach (int cell in result.Pocket.Concat(result.Gate).Concat(result.Floor))
                            {
                                reserved.Add(cell);
                                foreach (int neighbor in NestPocketPlanner.Neighbors(cell)) reserved.Add(neighbor);
                            }
                        }
                    }
                }
                decisions.Add(new NestDecision(vein, probability, roll, reason));
            }
            int[] carve = placements.SelectMany(p => p.Pocket).Distinct().OrderBy(i => i).ToArray();
            byte[] materials = input.CopyMaterials();
            foreach (int cell in carve)
            {
                if (!input.Carvable(cell)) throw new InvalidOperationException("巢腔占用了矿格、保护格或非围岩。");
                materials[cell] = 0;
            }
            byte[] shapes = settings.Enabled ? TerrainShapeGeometry.Build(materials, input.CopyProtection(), 320, 192) : input.CopyShapes();
            return new NestPassResult(settings.Enabled, placements, decisions, carve, shapes);
        }

        public static uint Hash(string value)
        {
            uint hash = 2166136261;
            foreach (char item in value) hash = unchecked((hash ^ item) * 16777619);
            // 混合相近身份的低位，独立于矿脉随机流。
            hash ^= hash >> 16; hash = unchecked(hash * 0x7feb352d);
            hash ^= hash >> 15; hash = unchecked(hash * 0x846ca68b);
            return hash ^ (hash >> 16);
        }
    }
}
