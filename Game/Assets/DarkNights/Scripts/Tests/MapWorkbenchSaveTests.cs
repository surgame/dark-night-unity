using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DarkNights.Editor;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 当前配置文件的持久化回归；通过真实预览与保存入口检查参数、私有表现、另存和冲突保护。
    /// 只写独立验证文档，来源资产只读，磁盘夹具保留供批次统一归档。
    /// </summary>
    public sealed class MapWorkbenchSaveTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Folder = "Assets/__TunerRecipeSaveValidation20261025";
        private CaveTerrainStyle style;
        private MapWorkbenchWindow window;
        private string documentPath, sourcePath, stylePath, sourceBefore, styleBefore;
        private UnityEngine.Object[] selection;
        private TerrainStyleDrafts Styles => (TerrainStyleDrafts)typeof(MapWorkbenchWindow)
            .GetField("drafts", Flags).GetValue(window);
        private TerrainGenerationPreview Model => (TerrainGenerationPreview)Read(window, "generation");
        private static object Read(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);

        private static object Invoke(object target, string method, params object[] args)
        {
            try { return target.GetType().GetMethod(method, Flags).Invoke(target, args); }
            catch (TargetInvocationException error)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }

        private object Store(string method, params object[] args)
        {
            var type = typeof(MapWorkbenchWindow).Assembly.GetType("DarkNights.Editor.Terrain.TerrainDocumentStore");
            try { return type.GetMethod(method, Flags).Invoke(null, args); }
            catch (TargetInvocationException error)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }

        private IEnumerator Ready()
        {
            double deadline = EditorApplication.timeSinceStartup + 90;
            var preview = Read(window, "preview");
            while (!Model.CandidateFresh || (bool)preview.GetType().GetProperty("Pending", Flags).GetValue(preview))
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待当前配置的真实预览超时。");
                yield return null;
            }
        }

        [SetUp]
        public void OpenIndependentConfiguration()
        {
            selection = Selection.objects;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__TunerRecipeSaveValidation20261025");
            documentPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Config.asset");
            sourcePath = "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset";
            stylePath = "Assets/DarkNights/Res/Terrain/StrataCave/Style.asset";
            sourceBefore = File.ReadAllText(sourcePath); styleBefore = File.ReadAllText(stylePath);
            window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            window.ShowUtility();
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
            typeof(MapWorkbenchWindow).Assembly.GetType("DarkNights.Editor.Terrain.MapWorkbenchFiles")
                .GetMethod("New", Flags).Invoke(null, new object[] { window });
            Invoke(Read(window, "commands"), "Edit", "装入旧步骤序列化夹具",
                (Action)(() => window.Draft.Config.Modifiers.Add(new EntranceWalkwayModifierConfig())));
            style = (CaveTerrainStyle)Read(window, "style");
        }

        [TearDown]
        public void ReleaseWindowAndKeepFixtures()
        {
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
            window.Close();
            Selection.objects = selection;
            Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(sourceBefore));
            Assert.That(File.ReadAllText(stylePath), Is.EqualTo(styleBefore));
            TestContext.WriteLine("Retained configuration: " + documentPath);
        }

        [UnityTest]
        public IEnumerator SaveReopenPersistsModifiersAndPrivateStyleTogether()
        {
            yield return Ready();
            foreach (bool enabled in new[] { false, true })
            {
                Invoke(Read(window, "commands"), "Edit", "验证生成步骤保存",
                    (Action)(() => ((EntranceWalkwayModifierConfig)window.Draft.Config.Modifiers[0]).Enabled = enabled));
                Styles.Draft((CaveTerrainStyle)Read(window, "style")).OutlineAmplitude = style.OutlineAmplitude + 1;
                float expected = style.OutlineAmplitude + 1;
                yield return Ready();
                var document = (TerrainMapDocument)Store("SaveTo", window, documentPath);
                Assert.That(document.Input.Modifiers[0].Enabled, Is.EqualTo(enabled));
                Assert.That(document.Style, Is.Not.SameAs(style));
                Assert.That(document.Style.OutlineAmplitude, Is.EqualTo(expected));
                Assert.That(Styles.HasChanges, Is.False);
                Store("Open", window, document);
                yield return Ready();
                Assert.That(window.Draft.Config.Modifiers[0].Enabled, Is.EqualTo(enabled));
                Assert.That(window.hasUnsavedChanges, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ExternalDocumentEditBlocksSaveAndKeepsPendingParameters()
        {
            yield return Ready();
            var document = (TerrainMapDocument)Store("SaveTo", window, documentPath);
            Invoke(Read(window, "commands"), "Edit", "验证保存冲突",
                (Action)(() => ((EntranceWalkwayModifierConfig)window.Draft.Config.Modifiers[0]).Enabled = false));
            yield return Ready();
            string diskBefore = File.ReadAllText(documentPath);
            document.name = "外部修改"; EditorUtility.SetDirty(document);
            Assert.Throws<InvalidOperationException>(() => Invoke(Read(window, "commands"), "SaveDraft"));
            Assert.That(window.hasUnsavedChanges, Is.True);
            Assert.That(window.Draft.Config.Modifiers[0].Enabled, Is.False);
            Assert.That(document.Input.Modifiers[0].Enabled, Is.True);
            Assert.That(File.ReadAllText(documentPath), Is.EqualTo(diskBefore));
            AssetDatabase.SaveAssetIfDirty(document);
            window.DiscardChanges();
            yield return Ready();
            Assert.That(window.Draft.Config.Modifiers[0].Enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SaveAsPreservesPreviousConfigurationAndPrivateAppearance()
        {
            yield return Ready();
            var first = (TerrainMapDocument)Store("SaveTo", window, documentPath);
            string before = File.ReadAllText(documentPath);
            Styles.Draft(style).OutlineAmplitude = style.OutlineAmplitude + 2;
            string copyPath = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Copy.asset");
            var copy = (TerrainMapDocument)Store("SaveTo", window, copyPath);
            Assert.That(copy, Is.Not.SameAs(first));
            Assert.That(copy.Style, Is.Not.SameAs(first.Style));
            Assert.That(copy.Style.OutlineAmplitude, Is.EqualTo(style.OutlineAmplitude + 2));
            Assert.That(File.ReadAllText(documentPath), Is.EqualTo(before));
            Store("Open", window, copy);
            yield return Ready();
            Assert.That(window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void SaveRejectsIncompletePreviewBeforeWritingAnyFile()
        {
            Assert.Throws<InvalidOperationException>(() => Store("SaveTo", window, documentPath));
            Assert.That(File.Exists(documentPath), Is.False);
        }
    }
}
