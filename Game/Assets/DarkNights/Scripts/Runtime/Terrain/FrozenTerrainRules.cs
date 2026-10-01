using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AnyRules.Next;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>一次会话的不可变采集目录；最大耐久唯一来自 AnyRuleD，材质 Key 编译成当前调色板索引，指纹不使用 Unity 实例编号。</summary>
    public sealed class FrozenTerrainRules
    {
        private readonly Dictionary<uint, (string Resource, int Yield, int Efficiency)> rules =
            new Dictionary<uint, (string, int, int)>();
        private readonly uint bedrock;
        public GridBusinessCatalog Business { get; }
        public string Fingerprint { get; }

        public FrozenTerrainRules(ServerGameplayCatalog catalog, GameplayDefinitionData[] definitions,
            IEnumerable<TerrainMaterialMiningRule> source)
        {
            Business = new GridBusinessCatalog(catalog, definitions);
            bedrock = catalog.Tiles.ByKey("bedrock");
            var identity = new StringBuilder(Business.ContentDigest);
            if (source == null) throw new InvalidOperationException("地形缺少采集规则。");
            foreach (var rule in source.OrderBy(value => value?.MaterialKey, StringComparer.Ordinal))
            {
                if (rule == null || rule.PickaxeEfficiencyPercent < 1 || rule.PickaxeEfficiencyPercent > 10000 ||
                    rule.Yield < 0 || rule.Yield > 1000 || rule.ResourceId != "" && rule.ResourceId != "iron" && rule.ResourceId != "gold" ||
                    rule.Yield > 0 && string.IsNullOrEmpty(rule.ResourceId))
                    throw new InvalidOperationException("材质采集效率、产出或资源不合法；当前货物只支持 iron/gold。");
                uint tile = catalog.Tiles.ByKey(rule.MaterialKey);
                if (rules.ContainsKey(tile) || tile == bedrock && rule.Yield != 0)
                    throw new InvalidOperationException("材质规则重复或基岩配置了产出。");
                rules.Add(tile, (rule.ResourceId ?? "", rule.Yield, rule.PickaxeEfficiencyPercent));
                identity.Append('|').Append(rule.MaterialKey).Append('|').Append(rule.ResourceId).Append('|')
                    .Append(rule.Yield.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(rule.PickaxeEfficiencyPercent.ToString(CultureInfo.InvariantCulture));
            }
            foreach (var definition in catalog.Definitions)
                if (!rules.ContainsKey(definition.Identity.TileId)) throw new InvalidOperationException("编译目录中存在没有采集规则的材质。");
            using var hash = SHA256.Create();
            Fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        public bool CanDamage(uint tile) => tile != 0 && tile != bedrock && rules.ContainsKey(tile);
        public int PickaxeDamage(uint tile, int damage)
        {
            if (damage < 1 || !CanDamage(tile)) return 0;
            long scaled = (long)damage * rules[tile].Efficiency;
            return (int)Math.Max(1, Math.Min(int.MaxValue, scaled / (100L * Math.Max(1, Business.Get(tile).Hardness))));
        }
        public (string Resource, int Amount) Drop(uint tile)
        {
            var rule = rules[tile]; return (rule.Resource, rule.Yield);
        }
    }
}
