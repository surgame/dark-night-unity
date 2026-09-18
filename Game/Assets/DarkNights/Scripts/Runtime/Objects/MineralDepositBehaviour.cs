using System;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Runner.DI;

namespace DarkNights.Runtime.Objects
{
    /// <summary>YYGC 矿床对象的状态所有者；手采和钻机只改变此状态，不持续清除地图格。</summary>
    [RequireConfig(typeof(MineralDepositRuleConfig))]
    public sealed partial class MineralDepositBehaviour : SessionStateBehaviour<MineralDepositState>, IMineralDepositCapability
    {
        [Inject] private MineralDepositRuleConfig config;
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
        public int DrillId => Current?.DrillId ?? 0;
        public double DrillProgress => Current?.DrillProgress ?? 0;
        public string ResourceId => RoomKind == "boss" || Rarity == "rare" ? "gold" : "iron";

        protected override void OnReset()
        {
            base.OnReset();
            if (config == null || config.RuleKey != MineralDepositRuleConfig.Rule)
                throw new InvalidOperationException("MineralDeposit requires its fixed RuleKey.");
        }

        internal void Prepare(int id, float x, string placement, TerrainDepositBlueprint blueprint)
        {
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));
            PrepareState(new MineralDepositState
            {
                Id = id, PlacementKey = placement, X = x, RoomKind = blueprint.RoomKind,
                Y = blueprint.Y,
                Rarity = blueprint.Rarity, Capacity = blueprint.Capacity, Remaining = blueprint.Capacity,
                Stage = MineralDepositStage.Available
            });
        }

        internal bool Extract(int amount)
        {
            if (amount <= 0 || Remaining <= 0) return false;
            MineralDepositState state = Edit();
            state.Remaining = Math.Max(0, state.Remaining - amount);
            state.Stage = state.Remaining == 0 ? MineralDepositStage.Depleted :
                state.DrillId == 0 ? MineralDepositStage.Available : MineralDepositStage.Drilling;
            return true;
        }

        internal bool ExtractByHand() => Extract(1);

        internal bool AttachDrill(int drillId)
        {
            if (drillId <= 0 || Remaining <= 0 || (DrillId != 0 && DrillId != drillId)) return false;
            MineralDepositState state = Edit(); state.DrillId = drillId; state.Stage = MineralDepositStage.Drilling; return true;
        }

        internal bool DetachDrill(int drillId)
        {
            if (drillId <= 0 || DrillId != drillId) return false;
            MineralDepositState state = Edit(); state.DrillId = 0; state.DrillProgress = 0;
            state.Stage = Remaining == 0 ? MineralDepositStage.Depleted : MineralDepositStage.Available;
            return true;
        }

        internal int AdvanceDrill(double seconds)
        {
            if (seconds <= 0 || DrillId == 0 || Remaining <= 0) return 0;
            MineralDepositState state = Edit(); state.DrillProgress += seconds;
            int extracted = 0;
            while (state.DrillProgress >= 1 && state.Remaining > 0)
            {
                state.DrillProgress -= 1; state.Remaining--; extracted++;
            }
            if (state.Remaining == 0) state.Stage = MineralDepositStage.Depleted;
            return extracted;
        }
    }
}
