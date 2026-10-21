using System;
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

namespace DarkNights.Tests
{
    /// <summary>
    /// 地图工作台候选、静态门控及版本装配的隔离回归；使用独立作者资产和保留夹具，不写正式新局引用。
    /// 只验编辑态生产链路，不把确定性或静态核对作为人物通行、运行事件或多人恢复验收。
    /// </summary>
    public sealed class TunerWorkbenchCandidateTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Folder = "Assets/__TunerCandidateValidation20261022";
        private ObjectDefinition source;
        private TerrainStylePreviewWindow window;
        private TerrainGenerationPreview model;
        private object commands;
        private static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
            catch (TargetInvocationException error)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }

        [SetUp]
        public void OpenIsolatedSource()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__TunerCandidateValidation20261022");
            source = ScriptableObject.CreateInstance<ObjectDefinition>();
            var flow = new ExpeditionFlowConfig();
            flow.CaveMap.Rules.Enabled = true;
            flow.Geology.Enabled = false;
            source.SharedConfigs.Add(flow);
            AssetDatabase.CreateAsset(source, AssetDatabase.GenerateUniqueAssetPath(Folder + "/Source.asset"));
            AssetDatabase.SaveAssetIfDirty(source);
            window = ScriptableObject.CreateInstance<TerrainStylePreviewWindow>();
            Call(window.Draft, "Load", source);
            model = (TerrainGenerationPreview)typeof(TerrainStylePreviewWindow).GetField("generation", Flags).GetValue(window);
            commands = typeof(TerrainStylePreviewWindow).GetField("commands", Flags).GetValue(window);
            model.JointDraft = true;
        }

        [TearDown]
        public void RetainFixtureAndReleaseWindow()
        {
            TestContext.WriteLine("Retained source: " + AssetDatabase.GetAssetPath(source));
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
            UnityEngine.Object.DestroyImmediate(window);
        }

        [TestCase("erosion")]
        [TestCase("switchback")]
        [TestCase("cluster")]
        [TestCase("rift")]
        [TestCase("roots")]
        public void RoadsRegenerateDeterministicallyAndContentOnlyKeepsPlan(string road)
        {
            window.Draft.Config.CaveMap.Rules.Road = road;
            var first = model.Generate();
            string before = ReviewedMineMap.Encode(first, "test");
            string plan = Newtonsoft.Json.JsonConvert.SerializeObject(new { model.RulePlan.Rooms, model.RulePlan.Connections });
            string settings = window.Draft.Config.CaveMap.Rules.SpaceKey(first.Seed);
            Assert.That(first.Rooms.Count, Is.GreaterThanOrEqualTo(9));
            Assert.That(model.Generate().Rooms.Count, Is.EqualTo(first.Rooms.Count));
            Assert.That(ReviewedMineMap.Encode(model.LastCandidate, "test"), Is.EqualTo(before));
            Call(commands, "ContentOnly");
            var content = model.Generate();
            Assert.That(window.Draft.Config.CaveMap.Rules.SpaceKey(content.Seed), Is.EqualTo(settings));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(new { model.RulePlan.Rooms, model.RulePlan.Connections }),
                Is.EqualTo(plan));
            Assert.That(ReviewedMineMap.Encode(content, "test"), Is.Not.EqualTo(before));
        }

        [Test]
        public void AllEntranceAndRoomMixChoicesGenerateWithoutChangingFormalSource()
        {
            string before = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single().CanonicalIdentity();
            foreach (string entrance in CaveRuleSettings.Entrances)
            {
                window.Draft.Config.CaveMap.Rules.Entrance = entrance;
                Assert.That(model.Generate().Rooms.Count, Is.GreaterThanOrEqualTo(9), entrance);
            }
            foreach (string mix in CaveRuleSettings.Mixes)
            {
                window.Draft.Config.CaveMap.Rules.RoomMix = mix;
                Assert.That(model.Generate().Rooms.Count, Is.GreaterThanOrEqualTo(9), mix);
            }
            Assert.That(source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single().CanonicalIdentity(), Is.EqualTo(before));
        }

        [Test]
        public void AuthorUndoAndStaleGateKeepOldCandidateUntilExplicitUpdate()
        {
            model.Generate();
            var first = model.LastCandidate;
            string original = window.Draft.Config.CaveMap.Rules.Road;
            Call(commands, "Edit", "验证道路撤销", (Action)(() => window.Draft.Config.CaveMap.Rules.Road = "roots"));
            Undo.FlushUndoRecordObjects();
            Assert.That(model.CandidateFresh, Is.False);
            Assert.That(model.LastCandidate, Is.SameAs(first));
            Assert.That(model.ExportBlockReason(), Does.Contain("未保存"));
            Undo.PerformUndo();
            Assert.That(window.Draft.Config.CaveMap.Rules.Road, Is.EqualTo(original));
            Assert.That(model.CandidateFresh, Is.True);
            Undo.PerformRedo();
            Assert.That(window.Draft.Config.CaveMap.Rules.Road, Is.EqualTo("roots"));
            Call(commands, "SaveDraft");
            Assert.That(model.ExportBlockReason(), Does.Contain("更新候选"));
            model.Generate();
            model.CheckCandidate();
        }

        [Test]
        public void FreezeCreatesReadOnlyVersionAndInstallValidatesTheSameSavedSource()
        {
            var terrain = model.Generate();
            model.CheckCandidate();
            Assert.That((string)Call(commands, "FreezeReason"), Does.Contain("核对"));
            Call(commands, "Check");
            Assert.That((string)Call(commands, "FreezeReason"), Is.Empty);
            var create = typeof(JointMineContentSetup).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(m => m.Name == "Create");
            var install = typeof(JointMineContentSetup).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Single(m => m.Name == "Install");
            var asset = (ReviewedMineMap)create.Invoke(null, new object[]
            { terrain, ReviewedMineMap.SourceFingerprint(window.Draft.Config), "test", false, source, Folder });
            var config = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            Assert.That(config.ReviewedMap, Is.Null, "创建不是装配");
            asset.VerifySource(config);
            Assert.That(ReviewedMineMap.Encode(asset.Freeze(TerrainGenerationPreview.WorldId), "test"),
                Is.EqualTo(ReviewedMineMap.Encode(terrain, "test")));
            install.Invoke(null, new object[] { asset, source });
            Assert.That(config.ReviewedMap, Is.SameAs(asset));
            config.CaveMap.Rules.ContentRevision++;
            Assert.Throws<TargetInvocationException>(() => install.Invoke(null, new object[] { asset, source }));
            TestContext.WriteLine("Retained version: " + AssetDatabase.GetAssetPath(asset));
        }

        [Test]
        public void OldGeologyConflictBlocksFreezeButDoesNotInventGeneratedSite()
        {
            window.Draft.Config.Geology.Enabled = true;
            Call(commands, "SaveDraft");
            var terrain = model.Generate();
            Assert.That(terrain.GeologySites, Is.Empty);
            Assert.That(model.GeologyConflict, Is.Not.Empty);
            Assert.That(model.ExportBlockReason(), Does.Contain("塌方"));
            window.Draft.Config.Geology.Enabled = false;
            Call(commands, "SaveDraft");
            model.Generate(); model.CheckCandidate();
            Assert.That(model.GeologyConflict, Is.Empty);
        }
    }
}
