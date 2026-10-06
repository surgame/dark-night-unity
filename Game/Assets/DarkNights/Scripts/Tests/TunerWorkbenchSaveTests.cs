using System;
using System.Linq;
using System.Reflection;
using DarkNights.Editor;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>
    /// 窗口保存入口的持久化回归；以独立作者夹具验证 Modifier 启停、表现与冲突保护。
    /// 不写用户作者资产或现有窗口，磁盘夹具保留供批次统一归档。
    /// </summary>
    public sealed class TunerWorkbenchSaveTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Folder = "Assets/__TunerSaveValidation20261006";
        private ObjectDefinition source;
        private CaveTerrainStyle style;
        private TerrainStylePreviewWindow window;
        private string sessionPath, stylePath;
        private TerrainStyleDrafts Styles => (TerrainStyleDrafts)typeof(TerrainStylePreviewWindow)
            .GetField("drafts", Flags).GetValue(window);

        [SetUp]
        public void OpenIsolatedAuthorDraft()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__TunerSaveValidation20261006");
            sessionPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Session.asset");
            stylePath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Style.asset");
            source = ScriptableObject.CreateInstance<ObjectDefinition>();
            source.SharedConfigs.Add(new HandheldConfig()); source.SharedConfigs.Add(new ExpeditionFlowConfig());
            style = ScriptableObject.CreateInstance<CaveTerrainStyle>();
            AssetDatabase.CreateAsset(source, sessionPath); AssetDatabase.CreateAsset(style, stylePath);
            window = ScriptableObject.CreateInstance<TerrainStylePreviewWindow>();
            typeof(ExpeditionFlowDraft).GetMethod("Load", Flags).Invoke(window.Draft, new object[] { source });
        }

        [TearDown]
        public void ReleaseWindowAndKeepFixtures()
        {
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
            UnityEngine.Object.DestroyImmediate(window);
            TestContext.WriteLine("Retained fixtures: " + sessionPath + "; " + stylePath);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SaveAllPersistsModifierStateAndStyleWhenReopened(bool enabled)
        {
            string unrelated = JsonUtility.ToJson(source.SharedConfigs.OfType<HandheldConfig>().Single());
            ((EntranceWalkwayModifierConfig)window.Draft.Config.Modifiers[0]).Enabled = enabled;
            Styles.Draft(style).OutlineAmplitude = style.OutlineAmplitude + 1;
            float expected = Styles.Draft(style).OutlineAmplitude;
            window.SaveChanges();
            Assert.That(window.hasUnsavedChanges, Is.False);
            Assert.That(Styles.HasChanges, Is.False);
            Resources.UnloadAsset(source); Resources.UnloadAsset(style);
            source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(sessionPath);
            style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(stylePath);
            Assert.That(source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single().Modifiers[0].Enabled, Is.EqualTo(enabled));
            Assert.That(style.OutlineAmplitude, Is.EqualTo(expected));
            Assert.That(JsonUtility.ToJson(source.SharedConfigs.OfType<HandheldConfig>().Single()), Is.EqualTo(unrelated));
            var reopened = ScriptableObject.CreateInstance<ExpeditionFlowDraft>();
            try
            {
                typeof(ExpeditionFlowDraft).GetMethod("Load", Flags).Invoke(reopened, new object[] { source });
                Assert.That(reopened.Config.Modifiers[0].Enabled, Is.EqualTo(enabled));
            }
            finally { UnityEngine.Object.DestroyImmediate(reopened); }
        }

        [Test]
        public void ConflictingSaveRetainsDisabledDraftAndCancelRestoresAuthor()
        {
            ((EntranceWalkwayModifierConfig)window.Draft.Config.Modifiers[0]).Enabled = false;
            typeof(TerrainStylePreviewWindow).GetMethod("RefreshChanges", Flags).Invoke(window, null);
            var actual = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            actual.Planets[0].DisplayName = "外部修改";
            Assert.Throws<InvalidOperationException>(() => window.SaveChanges());
            Assert.That(window.hasUnsavedChanges, Is.True);
            Assert.That(window.Draft.Config.Modifiers[0].Enabled, Is.False);
            Assert.That(actual.Modifiers[0].Enabled, Is.True);
            window.DiscardChanges();
            Assert.That(window.Draft.Config.Modifiers[0].Enabled, Is.True);
            Assert.That(window.Draft.Config.Planets[0].DisplayName, Is.EqualTo("外部修改"));
        }

        [Test]
        public void StyleOnlySaveDoesNotRewriteUnchangedWorldDraft()
        {
            var actual = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            actual.Planets[0].DisplayName = "另一个编辑器的新名称";
            Styles.Draft(style).OutlineAmplitude = style.OutlineAmplitude + 1;
            window.SaveChanges();
            Assert.That(source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single(), Is.SameAs(actual));
            Assert.That(actual.Planets[0].DisplayName, Is.EqualTo("另一个编辑器的新名称"));
        }
    }
}
