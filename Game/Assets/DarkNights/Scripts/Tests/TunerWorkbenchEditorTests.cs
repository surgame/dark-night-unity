using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkNights.Editor;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.View.Expedition;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>
    /// 统一星球工作台的作者合同；覆盖真实属性可编辑性、身份失效范围、保存重读及旧草稿转移。
    /// 作者资产只读；持久化使用独立验证目录，结束后保留夹具供统一归档。
    /// </summary>
    public sealed class TunerWorkbenchEditorTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static void Load(ExpeditionFlowDraft draft, ObjectDefinition source) =>
            typeof(ExpeditionFlowDraft).GetMethod("Load", Flags).Invoke(draft, new object[] { source });

        [Test]
        public void ActualSerializedPropertiesAreEditableAndAuthorIsIsolated()
        {
            using var model = new TerrainGenerationPreview();
            using var data = new SerializedObject(model.Draft);
            Assert.That(model.Draft.hideFlags & HideFlags.NotEditable, Is.EqualTo(HideFlags.None));
            var name = data.FindProperty("Config.Planets").GetArrayElementAtIndex(0).FindPropertyRelative("DisplayName");
            Assert.That(name.editable, Is.True);
            string before = model.Selected.DisplayName;
            name.stringValue = "可编辑的星球"; data.ApplyModifiedProperties();
            Assert.That(model.Selected.DisplayName, Is.EqualTo("可编辑的星球"));
            model.Cancel(); Assert.That(model.Selected.DisplayName, Is.EqualTo(before));
        }

        [Test]
        public void MetadataAndJourneyDoNotRebuildGeometryButDockAndStepsDo()
        {
            using var model = new TerrainGenerationPreview();
            string geometry = model.Identity;
            model.Selected.DisplayName += " 草稿"; model.Selected.TransitSeconds = 1.2f;
            model.Selected.StarSpeed = 333; model.Selected.ArrivalHeight = 160;
            model.Selected.Seed = "FIXED-ONLY"; model.Draft.Config.PreparationTimeoutSeconds = 45;
            Assert.That(model.HasChanges, Is.True); Assert.That(model.Identity, Is.EqualTo(geometry));
            model.Selected.DockRow++; Assert.That(model.Identity, Is.Not.EqualTo(geometry));
            model.Cancel(); geometry = model.Identity;
            ((EntranceWalkwayModifierConfig)model.Draft.Config.Modifiers[0]).Enabled = false;
            Assert.That(model.Identity, Is.Not.EqualTo(geometry));
        }

        [Test]
        public void FixedSeedIsExplicitAndPreviewDoesNotChangeIt()
        {
            using var model = new TerrainGenerationPreview();
            model.Selected.Seed = "FORMAL-FIXED";
            model.Draft.Config.CaveMap.Seed = "STRATA-0922";
            string before = model.Draft.Config.CanonicalIdentity();
            var map = model.Generate();
            Assert.That(map.Seed, Is.EqualTo("STRATA-0922"));
            Assert.That(model.Selected.Seed, Is.EqualTo("FORMAL-FIXED"));
            Assert.That(model.Draft.Config.CanonicalIdentity(), Is.EqualTo(before));
        }

        [Test]
        public void LegacyTransferIsDeepAndConflictingDraftsAreRetained()
        {
            using var model = new TerrainGenerationPreview();
            using var old = new TerrainGenerationPreview();
            old.Selected.DisplayName = "旧航程草稿";
            model.Import(old.Draft);
            Assert.That(model.Selected.DisplayName, Is.EqualTo("旧航程草稿"));
            Assert.That(model.Draft.Config.Modifiers[0], Is.Not.SameAs(old.Draft.Config.Modifiers[0]));
            old.Selected.DisplayName = "另一份旧草稿";
            Assert.Throws<InvalidOperationException>(() => model.Import(old.Draft));
            Assert.That(model.Selected.DisplayName, Is.EqualTo("旧航程草稿"));
            Assert.That(old.Selected.DisplayName, Is.EqualTo("另一份旧草稿"));
        }

        [Test]
        public void ExplicitSaveReloadKeepsUnrelatedConfigAndChecksExternalConflict()
        {
            const string folder = "Assets/__TunerJourneyValidation20261004";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "__TunerJourneyValidation20261004");
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/Session.asset");
            var source = ScriptableObject.CreateInstance<ObjectDefinition>();
            source.SharedConfigs.Add(new HandheldConfig()); source.SharedConfigs.Add(new ExpeditionFlowConfig());
            AssetDatabase.CreateAsset(source, path);
            var draft = ScriptableObject.CreateInstance<ExpeditionFlowDraft>(); Load(draft, source);
            using (var model = new TerrainGenerationPreview(draft))
            {
                string unrelated = JsonUtility.ToJson(source.SharedConfigs.OfType<HandheldConfig>().Single());
                model.Selected.DisplayName = "保存后的星球"; model.Selected.TransitSeconds = 1.4f;
                ((EntranceWalkwayModifierConfig)model.Draft.Config.Modifiers[0]).Enabled = false;
                model.Apply();
                Assert.That(JsonUtility.ToJson(source.SharedConfigs.OfType<HandheldConfig>().Single()), Is.EqualTo(unrelated));
                var actual = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
                model.Selected.DisplayName = "未保存"; actual.Planets[0].DisplayName = "外部修改";
                Assert.Throws<InvalidOperationException>(() => model.Apply());
                Assert.That(actual.Planets[0].DisplayName, Is.EqualTo("外部修改"));
            }
            Resources.UnloadAsset(source);
            source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(path);
            var reloaded = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            Assert.That(reloaded.Planets[0].DisplayName, Is.EqualTo("保存后的星球"));
            Assert.That(reloaded.Planets[0].TransitSeconds, Is.EqualTo(1.4f));
            Assert.That(((EntranceWalkwayModifierConfig)reloaded.Modifiers[0]).Enabled, Is.False);
            TestContext.WriteLine("Retained validation fixture: " + Path.GetFullPath(path));
        }

        [Test]
        public void StyleSaveReloadKeepsUnrelatedExternalFields()
        {
            const string folder = "Assets/__TunerJourneyValidation20261004";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "__TunerJourneyValidation20261004");
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/Style.asset");
            var source = ScriptableObject.CreateInstance<DarkNights.View.Terrain.CaveTerrainStyle>();
            AssetDatabase.CreateAsset(source, path);
            using (var styles = new TerrainStyleDrafts())
            {
                styles.Draft(source).OutlineAmplitude = 4;
                source.StoneSize = 5; EditorUtility.SetDirty(source);
                Assert.That(styles.Apply(), Is.EqualTo(1));
                Assert.That(source.StoneSize, Is.EqualTo(5));
            }
            Resources.UnloadAsset(source);
            source = AssetDatabase.LoadAssetAtPath<DarkNights.View.Terrain.CaveTerrainStyle>(path);
            Assert.That(source.OutlineAmplitude, Is.EqualTo(4)); Assert.That(source.StoneSize, Is.EqualTo(5));
        }

        [TestCase("star-shift")] [TestCase("fade")] [TestCase("none")]
        public void JourneyPreviewUsesFormalStrategyAndSeekStopsPlayback(string kind)
        {
            var planet = new PlanetPreset { TransitionKind = kind };
            var preview = new TerrainJourneyPreview(); preview.Play(); preview.Seek(.5f);
            var origin = new Vector2(.17f, .31f);
            var expected = new ConfiguredTravelTransition(kind).StarPosition(origin, .5f * planet.TransitSeconds, planet.StarSpeed);
            Assert.That(preview.StarPosition(planet, 0), Is.EqualTo(expected));
            Assert.That(preview.Playing, Is.False); Assert.That(preview.Progress, Is.EqualTo(.5f));
        }
    }
}
