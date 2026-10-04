using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Save;
using GameCore.Objects.Definition;

namespace DarkNights.Runtime.Network
{
    /// <summary>正式连接的内容握手摘要；统一规则、布局、定义、装备和航程配置身份，不拥有运行会话状态。</summary>
    internal static class SessionContentFingerprint
    {
        internal static string Create(GameCatalog catalog, LevelLayout layout, ObjectSessionResources resources,
            IReadOnlyList<ObjectPlacement> placements)
        {
            var fingerprint = new SaveContentFingerprint(catalog, layout);
            string identity = new ObjectWorldSaveJson(catalog, layout,
                resources.Definitions.ToDictionary(ObjectSessionResources.Rule, d => d.Guid.ToString()),
                placements.ToDictionary(p => p.PlacementKey, p => ObjectSessionResources.Rule(p.Definition))).IdentitySha256;
            var shared = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("session.pinewatch").SharedConfigs;
            var equipment = shared.OfType<HandheldConfig>().Single();
            var flow = shared.OfType<ExpeditionFlowConfig>().Single();
            string deposits = string.Join(";", resources.Definitions.OrderBy(value => value.Guid.ToString(), System.StringComparer.Ordinal)
                .SelectMany(value => value.SharedConfigs.OfType<MineralDepositRuleConfig>()
                    .Select(config => value.Guid + ":" + config.Fingerprint())));
            var database = ObjectDefinitionDatabase.Instance;
            var services = database.GetDefinitionByKey("expedition.ship").SharedConfigs.OfType<ShipServicesConfig>().Single();
            services.Validate();
            string serviceIdentity = string.Join(":", new[] { ("sale", services.Sale), ("shop", services.Shop) }
                .Select(pair =>
                {
                    var definition = pair.Item2.Resolve(database);
                    var config = definition.SharedConfigs.OfType<ShipServiceConfig>().Single();
                    config.Validate();
                    return definition.Guid + ":" + config.Service + ":" + config.Radius;
                }));
            string itemIdentity = resources.Equipment.Fingerprint;
            return "dark-nights-session-v" + Session.SessionAuthority.ProtocolVersion + ":" + fingerprint.RulesSha256 +
                ":" + fingerprint.LayoutSha256 + ":" + identity + ":" + equipment.Fingerprint() +
                ":" + BackgroundBakeDescriptor.StyleContentHash + ":" + flow.Fingerprint() +
                ":" + serviceIdentity + ":" + services.SaleEnabled + ":" + services.ShopEnabled + ":" + itemIdentity +
                ":" + Terrain.TerrainProfileConfig.Resolve().Freeze().Fingerprint + ":" + deposits + ":" +
                Core.Logic.Terrain.TerrainDepositFootprints.Version + ":" + Core.Logic.Terrain.TerrainDepositFootprints.NominalCells + ":" +
                Core.Logic.Terrain.TerrainDepositFootprints.MaximumCellsPerDeposit + ":" + Core.Logic.Terrain.TerrainDepositFootprints.MaximumWorldCells;
        }
    }
}
