using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>多格矿床的纯生成、值复制和原生装配合同；实际业务事务、Player 与联机由后续独立验收覆盖。</summary>
    public sealed class EmbeddedMineralContractTests
    {
        [Test]
        public void NativeContentBindsOneMineralLayerAndReservesSortingRange()
        {
            MineralLayerContentSetup.Install();
            var style = AssetDatabase.LoadAssetAtPath<DarkNights.View.Terrain.CaveTerrainStyle>("Assets/DarkNights/Res/Terrain/StrataCave/Style.asset");
            Assert.That(style.MineralDefinition, Is.SameAs(MineralLayerAssets.Ensure()));
            Assert.That(style.Background.FarOrder, Is.LessThan(DarkNights.View.Terrain.MineralLayerView.DefaultSortingOrder));
            Assert.That(style.Background.NearOrder, Is.LessThan(DarkNights.View.Terrain.MineralLayerView.DefaultSortingOrder));
            Assert.That(DarkNights.View.Terrain.MineralLayerView.DefaultSortingOrder + 3, Is.LessThan(0));
            Assert.That(style.Background.MiddleOrder, Is.GreaterThan(style.Background.DeepOrder));
            Assert.That(style.Background.NearOrder, Is.GreaterThan(style.Background.MiddleOrder));
        }

        [TestCase(1)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(80)]
        public void FinalFootprintsAreDeterministicAndPreserveCapacity(int capacity)
        {
            var materials = new byte[320 * 192]; var protection = new bool[materials.Length];
            protection[70 * 320 + 80] = true; materials[71 * 320 + 80] = 8;
            var anchors = new[] { new TerrainDepositBlueprint("b", "mine", 80, 70, "common", capacity),
                new TerrainDepositBlueprint("a", "boss", 80, 70, "rare", capacity) };
            var result = TerrainDepositFootprints.Build(anchors, materials, protection, "stable", 40);
            var repeated = TerrainDepositFootprints.Build(anchors.Reverse(), materials, protection, "stable", 40);
            Assert.That(result.SelectMany(value => value.Cells), Is.EqualTo(repeated.SelectMany(value => value.Cells)));
            var cells = result.SelectMany(value => value.Cells).ToArray();
            Assert.That(cells.Select(cell => (cell.U, cell.V)).Distinct().Count(), Is.EqualTo(cells.Length));
            Assert.That(cells.All(cell => cell.V <= -40 && !protection[-cell.V * 320 + cell.U] && materials[-cell.V * 320 + cell.U] != 8), Is.True);
            foreach (var deposit in result)
                Assert.That(deposit.Cells.Sum(cell => cell.Capacity), Is.EqualTo(capacity));
            Assert.That(materials.Count(value => value != 0), Is.EqualTo(1), "分配不能修改前景");
        }

        [Test]
        public void ProtectedOnlyCandidateFailsWithoutDroppingAnObject()
        {
            var cells = new byte[320 * 192]; var protection = Enumerable.Repeat(true, cells.Length).ToArray();
            Assert.Throws<InvalidOperationException>(() => TerrainDepositFootprints.Build(
                new[] { new TerrainDepositBlueprint("a", "mine", 80, 70, "common", 80) }, cells, protection, "stable"));
        }

        [Test]
        public void StateCopyOwnsItsArrayAndFrozenCellsRejectIllegalTombstones()
        {
            var original = new MineralDepositState();
            typeof(MineralDepositState).GetProperty(nameof(MineralDepositState.Cells)).SetValue(original, new[] { Cell(10, 40, 1) });
            var copy = new MineralDepositState(); copy.CopyFrom(original);
            copy.Cells[0] = Cell(0, 0, 2);
            Assert.That(original.Cells[0].Remaining, Is.EqualTo(10));
            Assert.That(MineralCellValidation.Validate(copy.Cells.Select(Frozen).ToArray(), 40, new HashSet<int>()), Is.True);
            Assert.That(MineralCellValidation.Validate(new[] { new MineralCellViewData(80, -70, 10, 0, 0, 1) }, 40, new HashSet<int>()), Is.False);
            Assert.That(MineralCellValidation.Validate(new[] { Frozen(original.Cells[0]), Frozen(original.Cells[0]) }, 40, new HashSet<int>()), Is.False);
        }

        private static MineralCellViewData Frozen(MineralCellState cell) => new MineralCellViewData(cell.U, cell.V,
            cell.Capacity, cell.Remaining, cell.Durability, cell.ContentVersion);

        private static MineralCellState Cell(int remaining, int durability, ulong version)
        {
            object boxed = new MineralCellState();
            var values = new Dictionary<string, object> { ["U"] = 80, ["V"] = -70, ["Capacity"] = 10,
                ["Remaining"] = remaining, ["Durability"] = durability, ["ContentVersion"] = version };
            foreach (var pair in values) typeof(MineralCellState).GetProperty(pair.Key).SetValue(boxed, pair.Value);
            return (MineralCellState)boxed;
        }
    }
}
