using System;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>YYGC 矿床对象的状态所有者；手采只改变对象存量，不持续清除地图格。</summary>
    [RequireConfig(typeof(MineralDepositRuleConfig))]
    public sealed partial class MineralDepositBehaviour : SessionStateBehaviour<MineralDepositState>, IMineralDepositCapability
    {
        [Inject] private MineralDepositRuleConfig config;
        private int harvestDurability, unitsPerHarvest;
        private string commonResource, rareResource;
        public int Id => Current?.Id ?? 0;
        public string RuleKey => config.RuleKey;
        public string DefinitionGuid => Object.Definition.Guid.ToString();
        public string PlacementKey => Current?.PlacementKey ?? "";
        public float X => Current?.X ?? 0;
        public int Y => Current?.Y ?? 0;
        public string RoomKind => Current?.RoomKind ?? "";
        public string Rarity => Current?.Rarity ?? "";
        public int Capacity => Current?.Capacity ?? 0;
        public int Remaining => Current?.Remaining ?? 0;
        public MineralDepositStage Stage => Current?.Stage ?? MineralDepositStage.Depleted;
        public string ResourceId => RoomKind == "boss" || Rarity == "rare" ? rareResource : commonResource;
        public int Durability => Current?.Durability ?? 0;
        public int MaximumDurability => harvestDurability;
        public int HarvestAmount => Math.Min(Remaining, unitsPerHarvest);
        public int RequiredMiningLevel { get; private set; }

        protected override void OnReset()
        {
            base.OnReset();
            if (config == null || config.RuleKey != MineralDepositRuleConfig.Rule)
                throw new InvalidOperationException("MineralDeposit requires its fixed RuleKey.");
            config.Validate(); harvestDurability = config.HarvestDurability; unitsPerHarvest = config.UnitsPerHarvest;
            RequiredMiningLevel = config.RequiredMiningLevel;
            commonResource = config.CommonResource; rareResource = config.RareResource;
        }

        internal void Prepare(int id, float x, string placement, TerrainDepositBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            PrepareState(new MineralDepositState
            {
                Id = id, PlacementKey = placement, X = x, RoomKind = blueprint.RoomKind,
                Y = blueprint.Y,
                Rarity = blueprint.Rarity, Capacity = blueprint.Capacity, Remaining = blueprint.Capacity,
                Stage = MineralDepositStage.Available, Durability = harvestDurability
            });
        }

        internal bool Extract(int amount)
        {
            if (amount <= 0 || Remaining <= 0) return false;
            MineralDepositState state = Edit();
            state.Remaining = Math.Max(0, state.Remaining - amount);
            state.Stage = state.Remaining == 0 ? MineralDepositStage.Depleted : MineralDepositStage.Available;
            state.Durability = state.Remaining == 0 ? 0 : harvestDurability;
            return true;
        }

        internal bool HitByTool(MiningToolRules tool, out int harvested)
        {
            harvested = 0;
            if (tool == null || tool.BlockReason(Core.ViewData.HeroMiningTargetKind.MineralDeposit,
                ResourceId, RequiredMiningLevel, DefinitionGuid).Length > 0) return false;
            return HitByHand(tool.Damage, out harvested);
        }

        internal bool HitByHand(int damage, out int harvested)
        {
            harvested = 0;
            if (damage < 1 || Remaining <= 0 || Durability <= 0) return false;
            var state = Edit(); state.Durability = Math.Max(0, state.Durability - damage);
            if (state.Durability > 0) return true;
            harvested = Math.Min(state.Remaining, unitsPerHarvest); state.Remaining -= harvested;
            state.Stage = state.Remaining == 0 ? MineralDepositStage.Depleted : MineralDepositStage.Available;
            state.Durability = state.Remaining == 0 ? 0 : harvestDurability; return true;
        }
        internal bool ExtractByHand() => HitByHand(harvestDurability, out _);
    }
}
