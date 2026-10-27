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
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>地图合同收口的有限编辑态验收；使用真实控件、Undo及保存装配入口，只写本批隔离资产，保留全部结果。</summary>
    [Explicit("本批保全验收使用独立资产目录；仅按本批用例过滤器显式执行。")]
    public sealed class MapContractEditorAcceptanceTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const string Root = "Assets/__MapContractAcceptance20261027";
        private MapWorkbenchWindow window;
        private ObjectDefinition source;
        private UnityEngine.Object[] selection;
        private TerrainGenerationPreview Model => (TerrainGenerationPreview)Read(window, "generation");
        private object Commands => Read(window, "commands");
        private static object Read(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
        private static object Call(object target, string method, params object[] args)
        {
            try { return target.GetType().GetMethod(method, Flags).Invoke(target, args); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static object Static(string type, string method, params object[] args)
        {
            try { return typeof(MapWorkbenchWindow).Assembly.GetType("DarkNights.Editor.Terrain." + type)
                .GetMethod(method, Flags).Invoke(null, args); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private IEnumerator Ready()
        {
            double deadline = EditorApplication.timeSinceStartup + 90;
            var preview = Read(window, "preview");
            while (!double.IsPositiveInfinity((double)Read(window, "generateAfter")) ||
                !Model.CandidateFresh || !(bool)preview.GetType().GetProperty("SaveReady", Flags).GetValue(preview))
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "地图预览未就绪：" +
                    typeof(MapWorkbenchWindow).GetProperty("Notice", Flags).GetValue(window));
                yield return null;
            }
        }
        [SetUp]
        public void OpenIsolatedWorkbench()
        {
            selection = Selection.objects;
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "__MapContractAcceptance20261027");
            source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ObjectDefinition>(
                "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset"));
            source.name = "Isolated map contract source";
            using (var data = new SerializedObject(source))
            {
                data.FindProperty("definitionGuid").stringValue = Guid.NewGuid().ToString("N");
                data.FindProperty("key").stringValue = "validation.map.contract." + Guid.NewGuid().ToString("N");
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.CreateAsset(source, AssetDatabase.GenerateUniqueAssetPath(Root + "/Source.asset"));
            AssetDatabase.SaveAssetIfDirty(source);
            window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            Call(window.Draft, "Load", source);
            window.ShowUtility();
            Static("MapWorkbenchFiles", "New", window);
        }
        [TearDown]
        public void KeepFixturesAndCloseWindow()
        {
            if (source != null) { Undo.ClearUndo(source); AssetDatabase.SaveAssetIfDirty(source); }
            if (window != null)
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                window.Close();
            }
            Selection.objects = selection;
        }
        [UnityTest]
        public IEnumerator SurfaceControlUndoAndSeedPolicyUseDistinctIdentities()
        {
            yield return Ready();
            var map = window.Draft.Config.CaveMap;
            string seed = map.Seed;
            byte[] landform = Model.LastCandidate.CopyMaterials();
            string sourceIdentity = ReviewedMineMap.SourceFingerprint(window.Draft.Config, Model.PlanetId);
            Undo.IncrementCurrentGroup();
            var picker = window.rootVisualElement.Query<DropdownField>().ToList().Single(x => x.label == "地表算法");
            picker.value = "基础噪声";
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            yield return Ready();
            Assert.That(window.Draft.Config.CaveMap.SurfaceAlgorithm, Is.EqualTo(TerrainGenerationSettings.NoiseSurface));
            Assert.That(window.rootVisualElement.Query<PropertyField>().ToList().Any(x => x.label == "起伏高度（格）"), Is.False);
            Assert.That(Model.LastCandidate.CopyMaterials(), Is.Not.EqualTo(landform));
            Undo.PerformUndo();
            yield return Ready();
            Assert.That(window.Draft.Config.CaveMap.SurfaceAlgorithm, Is.EqualTo(TerrainGenerationSettings.LandformSurface));
            CollectionAssert.AreEqual(landform, Model.LastCandidate.CopyMaterials());
            Assert.That(window.rootVisualElement.Query<PropertyField>().ToList().Any(x => x.label == "起伏高度（格）"), Is.True);
            Undo.PerformRedo();
            yield return Ready();
            Assert.That(window.Draft.Config.CaveMap.SurfaceAlgorithm, Is.EqualTo(TerrainGenerationSettings.NoiseSurface));
            Undo.PerformUndo();
            yield return Ready();
            string generation = Model.Identity;
            var candidate = Model.LastCandidate;
            Call(Commands, "Edit", "验证制作策略独立", (Action)(() =>
                window.Draft.Config.CaveMap.SeedSelection = new TerrainSeedSettings
                { Mode = TerrainSeedSettings.Pool, Seeds = new[] { seed, "MAP-CONTRACT-POOL-B" } }));
            Assert.That(Model.Identity, Is.EqualTo(generation));
            Assert.That(ReviewedMineMap.SourceFingerprint(window.Draft.Config, Model.PlanetId), Is.EqualTo(sourceIdentity));
            Assert.That(window.hasUnsavedChanges, Is.True);
            Call(Commands, "Edit", "验证航程独立", (Action)(() => window.Draft.Config.Planets[0].StarSpeed += 1));
            Assert.That(Model.Identity, Is.EqualTo(generation));
            yield return Ready();
            Assert.That(Model.LastCandidate, Is.SameAs(candidate));
            TestContext.WriteLine("Surface native DropdownField, hidden parameters, Undo/Redo and identities passed.");
        }
        [UnityTest]
        public IEnumerator SaveReopenAndInstallOnlyTouchIsolatedSource()
        {
            yield return Ready();
            Call(Commands, "Edit", "验证池配置保存", (Action)(() =>
            {
                var map = window.Draft.Config.CaveMap;
                map.SeedSelection = new TerrainSeedSettings
                { Mode = TerrainSeedSettings.Pool, Seeds = new[] { map.Seed, "MAP-CONTRACT-POOL-B" } };
                window.Draft.Config.Modifiers.Add(new EntranceWalkwayModifierConfig());
            }));
            yield return Ready();
            string beforeSource = File.ReadAllText(AssetDatabase.GetAssetPath(source));
            var expected = Model.LastCandidate;
            string path = AssetDatabase.GenerateUniqueAssetPath(Root + "/Map.asset");
            var document = (TerrainMapDocument)Static("TerrainDocumentStore", "SaveTo", window, path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(File.ReadAllText(AssetDatabase.GetAssetPath(source)), Is.EqualTo(beforeSource));
            Assert.That(document.Input.Modifiers, Is.Empty);
            Assert.That(document.Map.SourceIdentity, Does.StartWith("terrain-source-v3:"));
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
            window.Close(); window = null;
            document = AssetDatabase.LoadAssetAtPath<TerrainMapDocument>(path);
            window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            Call(window.Draft, "Load", source);
            window.ShowUtility();
            Static("TerrainDocumentStore", "Open", window, document);
            yield return Ready();
            Assert.That(window.hasUnsavedChanges, Is.False);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(window.Draft.Config.CaveMap.SeedSelection.Mode, Is.EqualTo(TerrainSeedSettings.Pool));
            Assert.That(Model.LastCandidate.Seed, Is.EqualTo(expected.Seed));
            Assert.That(ReviewedMineMap.Encode(Model.LastCandidate, "compare"),
                Is.EqualTo(ReviewedMineMap.Encode(expected, "compare")));
            Static("TerrainDocumentStore", "Install", window);
            var installed = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            Assert.That(installed.ReviewedMap, Is.SameAs(document.Map));
            installed.ReviewedMap.VerifySource(installed);
            CollectionAssert.AreEqual(expected.CopyMaterials(), installed.ReviewedMap.Freeze("00000000000000000000000000000002").CopyMaterials());
            Assert.That(source.SharedConfigs.OfType<TerrainProfileConfig>().Single().CaveStyle, Is.SameAs(document.Style));
            TestContext.WriteLine("Saved/reopened/installed fixture: " + path);
        }
        [Test]
        public void ExistingMapsKeepOriginalSourceAndPayload()
        {
            var formal = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset")
                .SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            Assert.That(formal.ReviewedMap.HasHistoricalSource, Is.True);
            formal.ReviewedMap.VerifySource(formal);
            var a = formal.ReviewedMap.Freeze("00000000000000000000000000000003");
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainMapDocument",
                new[] { "Assets/DarkNights/Res/Terrain/StrataCave/MapDocuments" }))
            {
                var doc = AssetDatabase.LoadAssetAtPath<TerrainMapDocument>(AssetDatabase.GUIDToAssetPath(guid));
                if (!doc.Map.HasHistoricalSource) continue;
                doc.Map.VerifySource(doc.Input);
                var input = (ExpeditionFlowConfig)Static("MapWorkbenchImport", "Read", doc);
                Assert.That(input.CaveMap.Rules, Is.Null);
                Assert.That(doc.Map.Freeze("00000000000000000000000000000004").Seed, Is.EqualTo(doc.Input.CaveMap.Seed));
                TestContext.WriteLine("Read historical map: " + AssetDatabase.GetAssetPath(doc));
            }
            CollectionAssert.AreEqual(a.CopyMaterials(), formal.ReviewedMap.Freeze("00000000000000000000000000000005").CopyMaterials());
        }
    }
}
