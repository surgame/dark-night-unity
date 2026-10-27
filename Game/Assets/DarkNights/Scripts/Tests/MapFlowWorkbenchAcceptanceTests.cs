using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DarkNights.Core.Config.Terrain;
using DarkNights.Editor;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>
    /// 生成流程与分层工作台的有限验收；通过真实控件、Undo和保存入口验证同次候选及历史读取。
    /// 只写本批独立资产，保留夹具和失败证据；不会覆盖用户窗口、正式装配或玩家存档。
    /// </summary>
    [Explicit("仅按本轮授权的有限验收过滤器执行，保留全部隔离夹具。")]
    public sealed class MapFlowWorkbenchAcceptanceTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static string Root => SessionState.GetString("DarkNights.MapFlow.AssetRoot", "Assets/__MapFlowAcceptance20261028");
        private MapWorkbenchWindow window;
        private ObjectDefinition source;
        private UnityEngine.Object[] selection;
        private TerrainGenerationPreview Model => (TerrainGenerationPreview)Read(window, "generation");
        private object Commands => Read(window, "commands");
        private static object Read(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
        private static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        private static object Static(string type, string name, params object[] args)
        {
            try { return typeof(MapWorkbenchWindow).Assembly.GetType("DarkNights.Editor.Terrain." + type).GetMethod(name, Flags).Invoke(null, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        private void Edit(Action action)
        {
            Undo.IncrementCurrentGroup();
            Call(Commands, "Edit", "生成流程有限验收", action);
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
        }
        private IEnumerator Ready()
        {
            double deadline = EditorApplication.timeSinceStartup + 90;
            var preview = Read(window, "preview");
            while (!double.IsPositiveInfinity((double)Read(window, "generateAfter")) || !Model.CandidateFresh ||
                !(bool)preview.GetType().GetProperty("SaveReady", Flags).GetValue(preview))
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline),
                    "预览未就绪：" + typeof(MapWorkbenchWindow).GetProperty("Notice", Flags).GetValue(window));
                yield return null;
            }
        }
        private static string Plan(TerrainGenerationPreview model) => string.Join(";", model.RulePlan.Rooms.Select(r =>
            r.Kind + "," + r.X + "," + r.Y + "," + r.Width + "," + r.Height));
        [SetUp]
        public void OpenIsolated()
        {
            selection = Selection.objects;
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", Path.GetFileName(Root));
            source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset"));
            using (var data = new SerializedObject(source))
            {
                data.FindProperty("definitionGuid").stringValue = Guid.NewGuid().ToString("N");
                data.FindProperty("key").stringValue = "validation.map.flow." + Guid.NewGuid().ToString("N");
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(source, AssetDatabase.GenerateUniqueAssetPath(Root + "/Source.asset"));
            AssetDatabase.SaveAssetIfDirty(source);
            window = ScriptableObject.CreateInstance<MapWorkbenchWindow>(); Call(window.Draft, "Load", source);
            window.ShowUtility(); typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
            Static("MapWorkbenchFiles", "New", window);
        }
        [TearDown]
        public void CloseIsolated()
        {
            if (source != null) { Undo.ClearUndo(source); AssetDatabase.SaveAssetIfDirty(source); }
            if (window != null) { typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false); window.Close(); }
            Selection.objects = selection;
        }
        [UnityTest]
        public IEnumerator NativeParametersUndoAndOverridePreservation()
        {
            yield return Ready();
            Model.CaptureStages = true; Call(Commands, "Generate"); yield return Ready();
            string plan = Plan(Model), payload = ReviewedMineMap.Encode(Model.LastCandidate, "compare");
            string seed = Model.LastCandidate.Seed;
            Call(window, "SelectPage", "entrance");
            var entry = window.rootVisualElement.Query<DropdownField>().ToList().Single(x => x.label == "入口形状 · 9种");
            Undo.IncrementCurrentGroup(); entry.value = "山脚横洞"; Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            yield return Ready();
            Assert.That(Plan(Model), Is.EqualTo(plan)); Assert.That(Model.LastCandidate.Seed, Is.EqualTo(seed));
            Assert.That(ReviewedMineMap.Encode(Model.LastCandidate, "compare"), Is.Not.EqualTo(payload));
            Undo.PerformUndo(); yield return Ready();
            Assert.That(ReviewedMineMap.Encode(Model.LastCandidate, "compare"), Is.EqualTo(payload));
            Undo.PerformRedo(); yield return Ready(); Assert.That(Plan(Model), Is.EqualTo(plan));
            Undo.PerformUndo(); yield return Ready();
            Call(window, "SelectPage", "surface");
            var surface = window.rootVisualElement.Query<DropdownField>().ToList().Single(x => x.label == "地表算法");
            Undo.IncrementCurrentGroup(); surface.value = "基础噪声"; Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            yield return Ready(); Assert.That(Plan(Model), Is.EqualTo(plan));
            Undo.PerformUndo(); yield return Ready();
            Assert.That(ReviewedMineMap.Encode(Model.LastCandidate, "compare"), Is.EqualTo(payload));
            Edit(() => window.Draft.Config.CaveMap.Structure.RoadRadius = 5); yield return Ready();
            Assert.That(Plan(Model), Is.EqualTo(plan));
            Undo.PerformUndo(); yield return Ready();
            Static("TerrainRegionCommands", "Edit", window, (Action<CaveRegionOverride>)(x => x.VeinCount = 2));
            yield return Ready();
            var before = Model.LastCandidate.CopyMaterials();
            Assert.That(window.Draft.Config.CaveMap.RegionOverrides.Length, Is.EqualTo(1));
            Static("TerrainRegionCommands", "Reroll", window); yield return Ready();
            CollectionAssert.AreEqual(before, Model.LastCandidate.CopyMaterials());
            Edit(() => window.Draft.Config.CaveMap.Structure.SpaceRevision++); yield return Ready();
            Assert.That(window.Draft.Config.CaveMap.RegionOverrides.Length, Is.EqualTo(1));
            Assert.That(Model.ContentDiagnostics.Any(x => x.Contains("已过期并保留")), Is.True);
            Undo.PerformUndo(); yield return Ready();
            var candidate = Model.LastCandidate; string identity = Model.Identity;
            Edit(() =>
            {
                window.Draft.Config.CaveMap.SeedSelection = new TerrainSeedSettings
                { Mode = TerrainSeedSettings.Pool, Seeds = new[] { seed, "FLOW-POOL-B" } };
                Model.Selected.DockColumn++;
            });
            yield return Ready();
            Assert.That(Model.Identity, Is.EqualTo(identity)); Assert.That(Model.LastCandidate, Is.SameAs(candidate));
            Call(Commands, "Reseed"); yield return Ready();
            Assert.That(window.Draft.Config.CaveMap.SeedSelection.Seeds.Contains(Model.LastCandidate.Seed), Is.True);
            Assert.That(window.Draft.Config.CaveMap.RegionOverrides.Length, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator ModuleLayersAndProductionStepsUseCurrentCandidate()
        {
            yield return Ready();
            Model.CaptureStages = true; Call(Commands, "Generate"); yield return Ready();
            string before = window.Draft.Config.CanonicalIdentity();
            Assert.That(window.rootVisualElement.Query<Button>(className: "map-module").ToList().Count, Is.EqualTo(15));
            Assert.That(window.rootVisualElement.Q("mapLibrary").parent, Is.SameAs(window.rootVisualElement.Q("moduleNavigation").parent));
            Assert.That(window.rootVisualElement.Q("mapLibrary").Q("taskNavigation"), Is.Null);
            Assert.That(window.rootVisualElement.Q("workbenchMore"), Is.Null);
            Assert.That(window.rootVisualElement.Query<Button>(className: "map-tab").ToList(), Is.Empty);
            var originalCandidate = Model.LastCandidate;
            foreach (string page in new[] { "recipe", "surface", "rooms", "roads", "entrance", "minerals", "environment", "extras", "appearance", "release" })
            {
                Call(window, "SelectPage", page);
                Assert.That(window.rootVisualElement.Query<VisualElement>(className: "map-config-basic").ToList().Count, Is.GreaterThan(0), page);
                Assert.That(window.rootVisualElement.Query<HelpBox>().ToList().Any(x => x.text.StartsWith("任务属性待修正")), Is.False, page);
                foreach (var fold in window.rootVisualElement.Query<Foldout>(className: "map-config-advanced").ToList())
                    Assert.That(fold.value, Is.False, page);
                Assert.That(window.Draft.Config.CanonicalIdentity(), Is.EqualTo(before), page);
            }
            Assert.That(Model.Result.Plan, Is.SameAs(Model.RulePlan));
            Assert.That(Model.Result.Steps.Count, Is.EqualTo(8));
            for (int i = 0; i < 8; i++)
            {
                Call(window.rootVisualElement.Q<Button>("productionStage" + i).clickable, "SimulateSingleClick", null, 0);
                Assert.That(window.rootVisualElement.Q<Image>("productionImage").ClassListContains("map-hidden"), Is.EqualTo(i == 7));
                Assert.That(Model.LastCandidate, Is.SameAs(originalCandidate));
                Assert.That(window.Draft.Config.CanonicalIdentity(), Is.EqualTo(before));
            }
            CollectionAssert.AreEqual(Model.LastCandidate.CopyMaterials(), Model.Result.Steps[7].CopyMaterials());
            Call(window, "SelectPage", "minerals");
            Assert.That(window.rootVisualElement.Q<DropdownField>("mineralAlgorithm").choices, Does.Not.Contain("基础矿床"));
            Call(window, "SelectPage", "recipe");
            var layout = window.rootVisualElement.Q<DropdownField>("layoutAlgorithm");
            layout.value = "天然洞穴";
            Assert.That(window.Draft.Config.CaveMap.Minerals.Algorithm, Is.EqualTo(TerrainMineralSettings.RegionalVeins));
            var mineral = window.rootVisualElement.Q<DropdownField>("mineralAlgorithm");
            Assert.That(mineral.value, Is.EqualTo("当前组合不支持，请选择"));
            mineral.value = "基础矿床"; yield return Ready();
            Assert.That(Model.RulePlan, Is.Null); Assert.That(Model.LastCandidate.Deposits.Count, Is.GreaterThan(0));
        }
        [UnityTest]
        public IEnumerator SaveReopenAndInstallWithFrozenPlan()
        {
            yield return Ready();
            Edit(() => window.Draft.Config.CaveMap.SeedSelection = new TerrainSeedSettings
            { Mode = TerrainSeedSettings.Pool, Seeds = new[] { window.Draft.Config.CaveMap.Seed, "FLOW-POOL-B" } });
            yield return Ready();
            var expected = Model.LastCandidate; string expectedPlan = Plan(Model);
            var style = (DarkNights.View.Terrain.CaveTerrainStyle)Read(window, "style");
            float originalOutline = style.OutlineAmplitude;
            ((TerrainStyleDrafts)Read(window, "drafts")).Draft(style).OutlineAmplitude = originalOutline + 1;
            string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Map.asset");
            var document = (TerrainMapDocument)Static("TerrainDocumentStore", "SaveTo", window, path);
            Assert.That(document.Map.SourceIdentity, Does.StartWith("terrain-source-v4:"));
            Assert.That(document.GenerationMetadata, Is.Not.Empty); string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(document.Style.OutlineAmplitude, Is.EqualTo(originalOutline + 1));
            Assert.That(style.OutlineAmplitude, Is.EqualTo(originalOutline));
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false); window.Close();
            window = ScriptableObject.CreateInstance<MapWorkbenchWindow>(); Call(window.Draft, "Load", source); window.ShowUtility();
            Static("TerrainDocumentStore", "Open", window, document); yield return Ready();
            Assert.That(Model.CandidateFromSaved, Is.True); Assert.That(Plan(Model), Is.EqualTo(expectedPlan));
            Assert.That(Model.Result.Version, Is.EqualTo(17));
            Assert.That(ReviewedMineMap.Encode(Model.LastCandidate, "compare"), Is.EqualTo(ReviewedMineMap.Encode(expected, "compare")));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(Model.LandingConflict, Is.Not.Empty, "此夹具保留自然起伏，不为船体制造平台。");
            Static("TerrainDocumentStore", "Install", window);
            Assert.That(source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single().ReviewedMap, Is.SameAs(document.Map));
            SessionState.SetString("DarkNights.MapFlow.Document", path);
            SessionState.SetString("DarkNights.MapFlow.Source", AssetDatabase.GetAssetPath(source));
            var copy = (TerrainMapDocument)Static("TerrainDocumentStore", "SaveTo", window,
                AssetDatabase.GenerateUniqueAssetPath(Root + "/Map-copy.asset"));
            Assert.That(copy.Map.Freeze(expected.WorldId).CopyMaterials(), Is.EqualTo(expected.CopyMaterials()));
            TestContext.WriteLine("Saved new fixture: " + path);
        }
        [UnityTest]
        public IEnumerator ExtendedToolsAndConflictGuardsRemainIsolated()
        {
            yield return Ready();
            var candidate = Model.LastCandidate;
            var state = Read(window, "state");
            foreach (string extra in new[] { "nest", "collapse", "scenery" })
            {
                state.GetType().GetField("Extra").SetValue(state, extra); Call(window, "SelectPage", "extras");
                Assert.That(window.rootVisualElement.Query<HelpBox>().ToList().Any(x => x.text.StartsWith("任务属性待修正")), Is.False, extra);
            }
            foreach (string tool in new[] { "planet", "paint", "study" })
            {
                state.GetType().GetField("AdvancedPage").SetValue(state, tool); Call(window, "SelectPage", "advanced");
                Assert.That(window.rootVisualElement.Query<VisualElement>(className: "map-config-basic").ToList().Count, Is.GreaterThan(0), tool);
                Assert.That(window.rootVisualElement.Query<Foldout>(className: "map-config-advanced").ToList().Count, Is.GreaterThan(0), tool);
                Assert.That(window.rootVisualElement.Query<HelpBox>().ToList().Any(x => x.text.StartsWith("任务属性待修正")), Is.False, tool);
            }
            var preview = Read(window, "preview");
            var paint = (TerrainMapDraft)preview.GetType().GetProperty("Map", Flags).GetValue(preview);
            Assert.That(paint.Paint(20, 100, false, 2), Is.True);
            Assert.Throws<InvalidOperationException>(() => Call(Commands, "Guard", true));
            Assert.That(paint.Undo(), Is.True); Assert.That(paint.Redo(), Is.True); paint.Cancel();
            Assert.That(Model.LastCandidate, Is.SameAs(candidate));
            Edit(() => window.Draft.Config.Geology.Enabled = true); yield return Ready();
            Assert.That(Model.GeologyConflict, Is.Not.Empty);
            Assert.Throws<InvalidOperationException>(() => Static("TerrainDocumentStore", "SaveTo", window, Root + "/MustNotSave.asset"));
            Assert.That(File.Exists(Root + "/MustNotSave.asset"), Is.False);
            Undo.PerformUndo(); yield return Ready(); Assert.That(Model.GeologyConflict, Is.Empty);
        }
        [UnityTest]
        public IEnumerator HistoricalDocumentsReadActualResultsWithoutReplanning()
        {
            yield return Ready();
            string[] roots = { "Assets/DarkNights/Res/Terrain/StrataCave/MapDocuments", "Assets/__MapContractAcceptance20261027" };
            var documents = AssetDatabase.FindAssets("t:TerrainMapDocument", roots).Select(g =>
                AssetDatabase.LoadAssetAtPath<TerrainMapDocument>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            Assert.That(documents.Length, Is.GreaterThan(0));
            foreach (var doc in documents)
            {
                doc.Map.VerifySource(doc.Input);
                Static("MapWorkbenchImport", "Read", doc);
            }
            var old = documents.First(x => string.IsNullOrEmpty(x.GenerationMetadata));
            var original = old.Map.Freeze("00000000000000000000000000000003");
            Static("TerrainDocumentStore", "Open", window, old); yield return Ready();
            Assert.That(Model.CandidateFromSaved, Is.True); Assert.That(Model.RulePlan, Is.Null);
            CollectionAssert.AreEqual(original.CopyMaterials(), Model.LastCandidate.CopyMaterials());
            Assert.That(window.rootVisualElement.Q<Label>("productionDetail").text, Does.Contain("未保存制作步骤"));
            TestContext.WriteLine("Historical documents validated: " + documents.Length);
        }
    }
}
