using System;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Runner.DI;
using System.Linq;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Objects
{
    /// <summary>YYGC 多格矿床的唯一状态所有者；格枯竭只清除矿层占用，前景不受影响，容量和阶段从格集合派生。</summary>
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
        public int Capacity => Current?.Cells.Sum(cell => cell.Capacity) ?? 0;
        public int Remaining => Current?.Cells.Sum(cell => cell.Remaining) ?? 0;
        public MineralDepositStage Stage => Remaining == 0 ? MineralDepositStage.Depleted : MineralDepositStage.Available;
        public string ResourceId => RoomKind == "boss" || Rarity == "rare" ? rareResource : commonResource;
        public MineralCellState PrimaryCell => Current?.Cells.FirstOrDefault(cell => cell.Remaining > 0) ?? default;
        public int Durability => PrimaryCell.Durability;
        public int MaximumDurability => harvestDurability;
        public int HarvestAmount => Math.Min(PrimaryCell.Remaining, unitsPerHarvest);
        public int UnitsPerHarvest => unitsPerHarvest;
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
                Rarity = blueprint.Rarity, Cells = blueprint.Cells.Select(cell => new MineralCellState
                { U = cell.U, V = cell.V, Capacity = cell.Capacity, Remaining = cell.Capacity,
                    Durability = harvestDurability, ContentVersion = 1 }).ToArray()
            });
        }

        internal bool Extract(int amount)
        {
            if (amount <= 0 || Remaining <= 0) return false;
            MineralDepositState state = Edit();
            for (int i = 0; i < state.Cells.Length && amount > 0; i++)
            {
                var cell = state.Cells[i]; int taken = Math.Min(cell.Remaining, amount);
                if (taken == 0) continue;
                cell.Remaining -= taken; amount -= taken;
                cell.Durability = cell.Remaining == 0 ? 0 : harvestDurability;
                if (cell.Remaining == 0) cell.ContentVersion = checked(cell.ContentVersion + 1);
                state.Cells[i] = cell;
            }
            return true;
        }

        internal bool HitByTool(MiningToolRules tool, out int harvested)
            => HitByTool(tool, PrimaryCell.U, PrimaryCell.V, PrimaryCell.ContentVersion, out harvested);

        internal bool HitByTool(MiningToolRules tool, int u, int v, ulong version, out int harvested)
        {
            harvested = 0;
            if (tool == null || tool.BlockReason(Core.ViewData.HeroMiningTargetKind.MineralDeposit,
                ResourceId, RequiredMiningLevel, DefinitionGuid).Length > 0) return false;
            if (!TryGetCell(u, v, out var cell) || cell.ContentVersion != version) return false;
            return HitCell(tool.Damage, u, v, out harvested);
        }

        internal bool HitByHand(int damage, out int harvested)
            => HitCell(damage, PrimaryCell.U, PrimaryCell.V, out harvested);

        internal bool TryGetCell(int u, int v, out MineralCellState cell)
        {
            foreach (var value in Current.Cells) if (value.U == u && value.V == v) { cell = value; return true; }
            cell = default; return false;
        }

        internal MineralDepositViewData Freeze() => new MineralDepositViewData(Id, X, Y, RoomKind, Rarity,
            ResourceId, MaximumDurability, unitsPerHarvest, RequiredMiningLevel, Current.Cells.Select(cell =>
                cell.Freeze(World?.Terrain?.Map?.ContentVersion(new AnyRules.Next.CellCoord(cell.U, cell.V)) ?? 0)).ToArray());

        private bool HitCell(int damage, int u, int v, out int harvested)
        {
            harvested = 0;
            if (damage < 1 || !TryGetCell(u, v, out var cell) || cell.Remaining <= 0 || cell.Durability <= 0) return false;
            var state = Edit(); int index = Array.FindIndex(state.Cells, value => value.U == u && value.V == v);
            cell.Durability = Math.Max(0, cell.Durability - damage);
            if (cell.Durability == 0)
            {
                harvested = Math.Min(cell.Remaining, unitsPerHarvest); cell.Remaining -= harvested;
                cell.Durability = cell.Remaining == 0 ? 0 : harvestDurability;
                if (cell.Remaining == 0) cell.ContentVersion = checked(cell.ContentVersion + 1);
            }
            state.Cells[index] = cell; return true;
        }
        internal bool ExtractByHand() => HitByHand(harvestDurability, out _);
    }
}
