using System;
using System.Linq;
using System.Reflection;
using DarkNights.Editor;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GameCore.Objects.Definition;

namespace DarkNights.Tests
{
    /// <summary>星球作者面板的隔离验收；验证草稿、配置冻结与冲突，不写正式资产或创建运行会话。</summary>
    public sealed class JourneyEditorTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }

        [Test]
        public void ConfigFreezesRowsAndFingerprintTracksAuthorChanges()
        {
            var config = new ExpeditionFlowConfig();
            var frozen = config.FreezePlanets(); string before = config.Fingerprint();
            config.Planets[0].DisplayName = "不同星球";
            Assert.That(frozen[0].DisplayName, Is.Not.EqualTo(config.Planets[0].DisplayName));
            Assert.That(config.Fingerprint(), Is.Not.EqualTo(before));
            Assert.That(JsonUtility.FromJson<ExpeditionFlowConfig>(JsonUtility.ToJson(config)).Fingerprint(), Is.EqualTo(config.Fingerprint()));
        }

        [Test]
        public void CatalogRejectsDuplicateDisabledNullAndExcessRows()
        {
            var config = new ExpeditionFlowConfig();
            config.Planets.Add(new PlanetPreset()); Assert.Throws<InvalidOperationException>(() => config.Validate());
            config.Planets.RemoveAt(1); config.Planets[0].Enabled = false;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            config.Enabled = false; Assert.DoesNotThrow(() => config.Validate());
            config.Planets.Add(null); Assert.Throws<InvalidOperationException>(() => config.Validate());
            config.Planets.Clear(); config.Enabled = true; Assert.Throws<InvalidOperationException>(() => config.Validate());
            for (int i = 0; i < 33; i++) config.Planets.Add(new PlanetPreset { Id = "p" + i });
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(0)] [TestCase(181)]
        public void InvalidTimeoutCannotApply(float value)
        {
            var config = new ExpeditionFlowConfig { PreparationTimeoutSeconds = value };
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            config.PreparationTimeoutSeconds = 30; config.ArrivalTimeoutSeconds = value;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Test]
        public void DraftApplyIsExplicitAndPreservesUnrelatedConfig()
        {
            var source = ScriptableObject.CreateInstance<ObjectDefinition>();
            var draft = ScriptableObject.CreateInstance<ExpeditionFlowDraft>();
            try
            {
                var equipment = new HandheldConfig(); source.SharedConfigs.Add(equipment);
                Call(draft, "Load", source); draft.Config.Planets[0].DisplayName = "草稿星";
                Assert.That(source.SharedConfigs.OfType<ExpeditionFlowConfig>(), Is.Empty);
                Call(draft, "Apply"); Call(draft, "Apply");
                Assert.That(source.SharedConfigs.Count, Is.EqualTo(2));
                Assert.That(source.SharedConfigs[0], Is.SameAs(equipment));
                var actual = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
                Assert.That(actual.Planets[0].DisplayName, Is.EqualTo("草稿星"));
                draft.Config.Planets[0].DisplayName = "未应用";
                Assert.That(actual.Planets[0].DisplayName, Is.EqualTo("草稿星"));
                Call(draft, "Load", source); Assert.That(draft.Config.Planets[0].DisplayName, Is.EqualTo("草稿星"));
            }
            finally { Undo.ClearUndo(source); UnityEngine.Object.DestroyImmediate(draft); UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void ExternalEditConflictKeepsBothAuthorAndDraft()
        {
            var source = ScriptableObject.CreateInstance<ObjectDefinition>();
            var draft = ScriptableObject.CreateInstance<ExpeditionFlowDraft>();
            try
            {
                var actual = new ExpeditionFlowConfig(); source.SharedConfigs.Add(actual);
                Call(draft, "Load", source); draft.Config.Planets[0].DisplayName = "草稿";
                actual.Planets[0].DisplayName = "外部编辑";
                Assert.Throws<InvalidOperationException>(() => Call(draft, "Apply"));
                Assert.That(actual.Planets[0].DisplayName, Is.EqualTo("外部编辑"));
                Assert.That(draft.Config.Planets[0].DisplayName, Is.EqualTo("草稿"));
            }
            finally { UnityEngine.Object.DestroyImmediate(draft); UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void TunerRowCommandsAndUndoKeepStableIdsAndSerializedDraft()
        {
            using var model = new DarkNights.Editor.Terrain.TerrainGenerationPreview();
            using var controls = new DarkNights.Editor.Terrain.TerrainPlanetControls(model);
            var draft = model.Draft;
            string before = JsonUtility.ToJson(draft.Config);
            Undo.IncrementCurrentGroup(); controls.Add(false); Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup(); controls.Add(true); Undo.FlushUndoRecordObjects();
            Assert.That(draft.Config.Planets.Select(p => p.Id).Distinct().Count(), Is.EqualTo(draft.Config.Planets.Count));
            string selected = model.Selected.Id;
            Undo.IncrementCurrentGroup(); controls.Move(-1); Undo.FlushUndoRecordObjects();
            Assert.That(model.Selected.Id, Is.EqualTo(selected));
            string reordered = JsonUtility.ToJson(draft.Config);
            Undo.PerformUndo(); Assert.That(draft.Config.Planets.Count, Is.EqualTo(3));
            Undo.PerformRedo(); Assert.That(JsonUtility.ToJson(draft.Config), Is.EqualTo(reordered));
            model.Cancel(); Assert.That(JsonUtility.ToJson(draft.Config), Is.EqualTo(before));
            Undo.ClearUndo(draft);
        }
    }
}
