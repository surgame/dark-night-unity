using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AnyRules.Next;
using AnyRules.Next.Compiler;

namespace DarkNights.Tools.OreRuleReview
{
    /// <summary>
    /// 只读引用锁定 AnyRules 源码的方案审查探针；实际编译和求解矿层候选规则，
    /// 不创建业务对象或 Unity 资源。资源身份只是测试标记，不能证明真实贴图或 GPU 画面正确。
    /// </summary>
    internal static class Program
    {
        private static readonly string[] Minerals = { "iron", "gold", "copper", "silver", "diamond" };
        private static readonly StableGuid WorldId = Id(900000);
        private static readonly RuleQueryContext Context = new RuleQueryContext(42, WorldId, 1, new VisualCoord(4, -3));

        private static int Main(string[] args)
        {
            var checks = new Dictionary<string, int>();
            try
            {
                RuleCompilation compiled = Compile(Source());
                Require(compiled.Diagnostics.Count == 0, "Candidate has compiler diagnostics.");
                checks["actual_compilation"] = 1;
                checks["four_variant_rule_sets"] = 75;
                var runtime = compiled.Runtime;
                var gameplay = RuleCatalogCodec.ReadGameplay(RuleCatalogCodec.WriteGameplay(compiled.Gameplay));
                var restored = RuleCatalogCodec.ReadVisual(RuleCatalogCodec.WriteVisual(runtime), gameplay);
                int selections = 0, recipes = 0, roundtrips = 0;
                for (int code = 0; code < 1296; code++)
                {
                    uint[] values = Corners(code);
                    CornerSamples samples = Samples(values);
                    for (uint target = 1; target <= Minerals.Length; target++)
                    {
                        int expected = Mask(values, target);
                        var status = runtime.TryResolve(target, TerrainKey.Parse("ore"), samples, Context, out var slot);
                        Require(expected == 0 ? status == RuleQueryStatus.Unmatched :
                            status == RuleQueryStatus.Matched && slot.RuleGuid == RuleId(target, expected), "Wrong mineral mask.");
                        if (expected != 0) selections++;
                    }
                    RenderRecipe recipe = Solve(runtime, samples);
                    bool empty = values.All(value => value == 0);
                    Require(recipe.Status == (empty ? RecipeStatus.Empty : RecipeStatus.Ready), "Invalid recipe.");
                    Require(recipe.Parts.All(part => part.Kind == RecipePartKind.Fill || part.Kind == RecipePartKind.TerrainSurface), "Unexpected transition or exact-cell part.");
                    foreach (var part in recipe.Parts.Where(part => part.Kind == RecipePartKind.Fill))
                        Require(part.ResourceId == Id(1002), "Unexpected base fill resource.");
                    var surfaces = recipe.Parts.Where(part => part.Kind == RecipePartKind.TerrainSurface).ToArray();
                    Require(surfaces.Length == values.Where(value => value != 0).Distinct().Count(), "Missing/duplicate mineral output.");
                    foreach (var part in surfaces)
                    {
                        uint target = runtime.Gameplay.Tiles.ByGuid(part.Key.TerrainGuid);
                        Require(part.Key.RuleGuid == RuleId(target, Mask(values, target)), "Wrong solved rule.");
                        Require(part.SortingDomain == VisualSortingDomain.Ground, "Mineral layer unexpectedly needs footpoint sorting.");
                    }
                    Require(recipe.ContentEquals(Solve(restored, samples)), "Catalog roundtrip changed output.");
                    Require(recipe.ContentEquals(Solve(runtime, samples)), "Repeated solve changed output.");
                    recipes++; roundtrips++;
                }
                checks["four_corner_recipes"] = recipes;
                checks["nonempty_mineral_selections"] = selections;
                checks["catalog_roundtrips"] = roundtrips;
                for (int corner = 0; corner < 4; corner++)
                {
                    GridSample[] samples = Enumerable.Repeat(GridSample.FromCell(new GridCell(1)), 4).ToArray();
                    samples[corner] = GridSample.Unknown;
                    Require(Solve(runtime, new CornerSamples(samples[0], samples[1], samples[2], samples[3])).Status == RecipeStatus.Pending, "Unknown input rendered as known.");
                }
                checks["unknown_corner_pending"] = 4;
                RuleCompilationException invalid = null;
                try { Compile(Source("ore", RuleSlotCoverage.ExactCell, true)); }
                catch (RuleCompilationException error) { invalid = error; }
                Require(invalid != null && invalid.Diagnostics.Any(item => item.Code == RuleDiagnosticCode.InvalidCoverageContract), "Old exact-cell contract was accepted.");
                checks["old_exact_notthis_rejected"] = 1;
                var notThis = Compile(Source("ore", RuleSlotCoverage.TerrainSurface, true, requireCoverage: false)).Runtime;
                Require(notThis.TryResolve(1, TerrainKey.Parse("ore"), Samples(new uint[] { 1, 0, 0, 0 }), Context, out _) == RuleQueryStatus.Unmatched, "NotThis unexpectedly contains Empty.");
                checks["notthis_excludes_empty"] = 1;
                var surfaceOnly = Compile(Source("surface")).Runtime;
                Require(Solve(surfaceOnly, Samples(new uint[] { 1, 2, 1, 2 })).Parts.All(part => part.Kind == RecipePartKind.Fill), "Special surface unexpectedly preserved mixed-material masks.");
                checks["surface_channel_mixed_fallback"] = 1;
                var noFill = Compile(Source(fill: false)).Runtime;
                Require(Solve(noFill, Samples(new uint[] { 1, 2, 0, 0 })).Diagnostic == RecipeDiagnostic.MissingFill, "Missing generic fill was not detected.");
                checks["missing_fill_rejected"] = 1;
                Write(args, true, checks, "");
                Console.WriteLine(JsonSerializer.Serialize(new { passed = true, checks }));
                return 0;
            }
            catch (Exception error)
            {
                Write(args, false, checks, error.ToString());
                Console.Error.WriteLine(error);
                return 1;
            }
        }

        private static RuleCatalogSource Source(string channel = "ore", RuleSlotCoverage coverage = RuleSlotCoverage.TerrainSurface,
            bool notThis = false, bool fill = true, bool requireCoverage = true)
        {
            var visuals = new List<TileVisualSetData>
            {
                new TileVisualSetData(Id(1000), new[] { new VisualVariantData(Id(1001), Id(1002), 1) }, tileableFill: true)
            };
            var terrains = new List<TerrainDefinitionData>();
            var sets = new List<RuleSetData>();
            for (uint target = 1; target <= Minerals.Length; target++)
            {
                terrains.Add(new TerrainDefinitionData(Id(10 + target), TerrainKey.Parse("ore." + Minerals[target - 1]),
                    Minerals[target - 1], solid: false, fillVisualSetGuid: fill ? Id(1000) : default));
                var rules = new List<RuleDefinitionData>();
                for (int mask = 1; mask < 16; mask++)
                {
                    uint offset = target * 100 + (uint)mask;
                    var visual = new TileVisualSetData(Id(10000 + offset),
                        Enumerable.Range(0, 4).Select(variant => new VisualVariantData(Id(40000 + offset * 10 + (uint)variant),
                            Id(50000 + offset * 10 + (uint)variant), 1)), RuleTransformMask.Identity);
                    visuals.Add(visual);
                    var corners = Enumerable.Range(0, 4).Select(corner => (mask & (1 << corner)) != 0 ?
                        CornerPredicate.This : notThis ? CornerPredicate.NotThis : CornerPredicate.AnyKnown).ToArray();
                    var output = new RuleSlotData(Id(30000 + offset), TerrainKey.Parse(channel), visual.AssetGuid,
                        coverage: coverage, boundaryContract: coverage == RuleSlotCoverage.ExactCell ? CellBoundaryContract.CanonicalMaterialEdgesV1 : CellBoundaryContract.None);
                    rules.Add(new RuleDefinitionData(RuleId(target, mask), PopCount(mask), corners, new[] { output }, RuleTransformMask.Identity));
                }
                sets.Add(new RuleSetData(Id(60000 + target), Id(10 + target), Minerals[target - 1], rules));
            }
            return new RuleCatalogSource(Id(800000), 1, terrains, visuals, sets, requireCoverage);
        }

        private static RuleCompilation Compile(RuleCatalogSource source) => RuleCompiler.Compile(source, RuleCompiler.ComputeSourceDigest(source));
        private static RenderRecipe Solve(RuntimeAnyRuleDDatabase runtime, CornerSamples samples) =>
            new MultiTerrainSolver(runtime).Solve(new WorldIdentity(WorldId, 1), Context, samples, 1);
        private static CornerSamples Samples(uint[] values) => new CornerSamples(GridSample.FromCell(new GridCell(values[0])),
            GridSample.FromCell(new GridCell(values[1])), GridSample.FromCell(new GridCell(values[2])), GridSample.FromCell(new GridCell(values[3])));
        private static uint[] Corners(int code)
        {
            var values = new uint[4];
            for (int corner = 0; corner < 4; corner++) { values[corner] = (uint)(code % 6); code /= 6; }
            return values;
        }
        private static int Mask(uint[] corners, uint target)
        {
            int mask = 0;
            for (int corner = 0; corner < 4; corner++) if (corners[corner] == target) mask |= 1 << corner;
            return mask;
        }
        private static int PopCount(int mask)
        {
            int result = 0;
            for (int corner = 0; corner < 4; corner++) result += (mask >> corner) & 1;
            return result;
        }
        private static StableGuid RuleId(uint target, int mask) => Id(20000 + target * 100 + (uint)mask);
        private static StableGuid Id(uint value) => StableGuid.Parse(value.ToString("x32"));
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Write(string[] args, bool passed, Dictionary<string, int> checks, string error)
        {
            if (args.Length == 0) return;
            File.WriteAllText(args[0], JsonSerializer.Serialize(new
            {
                schema = 1, passed, checks, error,
                scope = "Actual unchanged AnyRules compiler, solver and codec; synthetic resource IDs; no Unity, raster, business or network validation."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
