using System;
using DarkNights.Core.Config;
using DarkNights.Core.Logic;

namespace DarkNights.Tests
{
    /// <summary>
    /// 独立于引擎的数值和只读内容边界：冻结布局、网格边界、到达容差与资源值隔离。
    /// 不运行实体生命周期；完整规则、支付和恢复由真实 YYGC 集成回归覆盖。
    /// </summary>
    public static class PureRuleScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            check(catalog.Balance.Units.Count == 6 && catalog.Balance.Buildings.Count == 5 &&
                catalog.Balance.Worksites.Count == 4 && catalog.Level.Waves.Count == 3,
                "Frozen Pinewatch content has all 15 rule definitions and three nights");
            check(layout.Buildings.Count == 4 && layout.Worksites.Count == 5 && layout.Actors.Count == 7,
                "Frozen authored layout contains 16 placements before the derived farm worksite");
            check(SimulationMath.MoveToward(10, 11, 2) == 11 && SimulationMath.MoveToward(11, 10, 2) == 10,
                "Movement never overshoots in either direction");
            check(SimulationMath.MoveToward(10, 20, 0) == 10 && SimulationMath.MoveToward(20, 10, 3) == 17,
                "Zero elapsed time cannot move and reverse travel retains its direction");
            check(SimulationMath.Snapped(-2, 4) == 0 && SimulationMath.Snapped(2, 4) == 4 &&
                SimulationMath.Snapped(2.5, 0) == 2.5, "Signed half-grid boundaries keep the original rounding rule");
            check(PlacementGeometry.Snap(181.999f) == 180 && PlacementGeometry.Snap(182) == 184,
                "Build preview and authority cross the same four-pixel grid boundary");
            check(PlacementGeometry.Within(20, 40, 0, 100) && !PlacementGeometry.Within(19.999f, 40, 0, 100),
                "Footprints include exact legal edges and reject boundary overlap");
            check(!PlacementGeometry.BuildingOverlap(0, 40, 48, 40) && PlacementGeometry.BuildingOverlap(0, 40, 47.99f, 40),
                "Building clearance retains the eight-pixel gap");
            check(!PlacementGeometry.WorksiteOverlap(0, 40, 46, 40) && PlacementGeometry.WorksiteOverlap(0, 40, 45.99f, 40),
                "Worksite clearance retains the six-pixel gap");
            var original = new ResourceAmounts(20, 10, 5, 4, 3);
            var reduced = original.Subtract(new ResourceAmounts(food: 25, wood: 2));
            check(original.Food == 20 && original.Wood == 10 && reduced.Food == -5 && reduced.Wood == 8 && !reduced.IsValid(),
                "Resource arithmetic preserves the original and does not hide an invalid payment");
            check(!new ResourceAmounts(food: double.NaN).IsValid() && !new ResourceAmounts(gold: double.PositiveInfinity).IsValid(),
                "Nonfinite resources are rejected before publication or persistence");
        }
    }
}
