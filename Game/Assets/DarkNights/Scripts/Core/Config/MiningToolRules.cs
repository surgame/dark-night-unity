using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.ViewData;

namespace DarkNights.Core.Config
{
    /// <summary>工具 Definition 的冻结采集规则；匹配函数同时供只读选取和权威执行使用，集合不暴露可写引用。</summary>
    public sealed class MiningToolRules
    {
        private readonly HashSet<string> materials;
        private readonly HashSet<string> deposits;
        public MiningTargetKinds Targets { get; }
        public bool AllMaterials { get; }
        public int Level { get; }
        public int Damage { get; }
        public float Reach { get; }
        public float HandHeight { get; }
        public float Seconds { get; }
        public float ImpactFraction { get; }

        public MiningToolRules(MiningTargetKinds targets, bool allMaterials, IEnumerable<string> materials,
            IEnumerable<string> deposits, int level, int damage, float reach, float handHeight,
            float seconds, float impactFraction)
        {
            this.materials = new HashSet<string>(materials ?? Array.Empty<string>(), StringComparer.Ordinal);
            this.deposits = new HashSet<string>(deposits ?? Array.Empty<string>(), StringComparer.Ordinal);
            if ((targets & ~(MiningTargetKinds.Foreground | MiningTargetKinds.MineralDeposit)) != 0 ||
                level < 1 || level > 1000 || damage < 1 || damage > 1000 ||
                !Positive(reach, 128) || !Positive(handHeight, 64) || !Positive(seconds, 5) ||
                !Positive(impactFraction, .95f) || this.materials.Count > 128 || this.deposits.Count > 128 ||
                this.materials.Any(string.IsNullOrWhiteSpace) || this.deposits.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("工具采集配置无效。");
            Targets = targets; AllMaterials = allMaterials; Level = level; Damage = damage;
            Reach = reach; HandHeight = handHeight; Seconds = seconds; ImpactFraction = impactFraction;
        }

        public string BlockReason(HeroMiningTargetKind kind, string material, int requiredLevel = 1,
            string definitionGuid = "")
        {
            MiningTargetKinds target = kind == HeroMiningTargetKind.Foreground ? MiningTargetKinds.Foreground :
                kind == HeroMiningTargetKind.MineralDeposit ? MiningTargetKinds.MineralDeposit : MiningTargetKinds.None;
            if (target == MiningTargetKinds.None || (Targets & target) == 0) return "当前工具不支持此类目标";
            if (requiredLevel < 1 || Level < requiredLevel) return "工具采集等级不足";
            if (string.IsNullOrWhiteSpace(material) || !AllMaterials && !materials.Contains(material)) return "当前工具不支持此材料";
            if (target == MiningTargetKinds.MineralDeposit && deposits.Count > 0 && !deposits.Contains(definitionGuid))
                return "当前工具不支持此矿床定义";
            return "";
        }

        private static bool Positive(float value, float maximum) => !float.IsNaN(value) &&
            !float.IsInfinity(value) && value > 0 && value <= maximum;
    }
}
