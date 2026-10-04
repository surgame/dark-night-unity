using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>会话矿层的冻结类型目录与采集规则；身份沿用原作者资源，实例耐久与储量只由原生地图持有。</summary>
    public sealed class FrozenMineralRules
    {
        public static readonly string[] Keys = { "iron", "gold", "copper", "silver", "diamond" };
        public ARDMapDefinition Definition { get; }
        public GridBusinessCatalog Business { get; }
        public string Fingerprint { get; }
        public string DefinitionGuid { get; }
        public int MaximumDurability { get; }
        public int UnitsPerHarvest { get; }
        public int RequiredLevel { get; }
        public string CommonResource { get; }
        public string RareResource { get; }
        public FrozenMineralRules(ARDMapDefinition definition, MineralDepositRuleConfig config, string definitionGuid)
        {
            Definition = definition ?? throw new InvalidOperationException("矿层缺少原生地图定义。");
            config.Validate(); DefinitionGuid = definitionGuid; MaximumDurability = config.HarvestDurability;
            UnitsPerHarvest = config.UnitsPerHarvest; RequiredLevel = config.RequiredMiningLevel;
            CommonResource = config.CommonResource; RareResource = config.RareResource;
            Business = new GridBusinessCatalog(definition.LoadGameplayCatalog(), definition.ExportBusinessDefinitions());
            if (Business.Gameplay.Definitions.Any(value => value.MaximumDurability != MaximumDurability))
                throw new InvalidOperationException("矿层目录耐久与矿镐规则不一致；请执行矿层地图配置升级。");
            using var hash = SHA256.Create();
            Fingerprint = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Business.ContentDigest + "|" +
                config.Fingerprint() + "|" + definitionGuid))).Replace("-", "").ToLowerInvariant();
        }
        public static FrozenMineralRules Resolve()
        {
            var definition = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("scenery.mineral-deposit")
                ?? throw new InvalidOperationException("缺少矿层类型的静态 Definition。");
            return new FrozenMineralRules(TerrainProfileConfig.Resolve().MineralDefinition,
                definition.SharedConfigs.OfType<MineralDepositRuleConfig>().Single(), definition.Guid.ToString());
        }
        public string Kind(uint tile) => Business.Gameplay.Tiles.TryGet(tile, out var identity) ? identity.Key.ToString() : "";
        public string Resource(uint tile) => Kind(tile) == "gold" ? RareResource : CommonResource;
        public string BlockReason(MiningToolRules tool, uint tile) => tool == null ? "缺少矿镐" :
            tool.BlockReason(HeroMiningTargetKind.MineralDeposit, Kind(tile), RequiredLevel, DefinitionGuid);
    }
}
