using System.Reflection;
using DarkNights.Editor.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>工作台禁用、重建及销毁的草稿生命周期；用户参数与样式草稿保留，预览及编辑器句柄按窗口释放。</summary>
    public sealed class MapWorkbenchLifecycleTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Get(object value, string field) => value.GetType().GetField(field, Flags).GetValue(value);
        private static void Call(object value, string method) => value.GetType().GetMethod(method, Flags).Invoke(value, null);

        [Test]
        public void ReenableKeepsAuthorAndStyleDraftAndNavigationWithoutWritingSources()
        {
            var window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            try
            {
                var model = (TerrainGenerationPreview)Get(window, "generation");
                var draft = window.Draft; model.Selected.DisplayName = "恢复后的草稿";
                var navigation = Get(window, "state");
                navigation.GetType().GetField("Page").SetValue(navigation, "advanced");
                navigation.GetType().GetField("AdvancedPage").SetValue(navigation, "planet");
                var style = (CaveTerrainStyle)Get(window, "style");
                var styles = (TerrainStyleDrafts)Get(window, "drafts");
                float before = style.OutlineAmplitude;
                styles.Draft(style).OutlineAmplitude = before + 1;
                Call(window, "OnDisable"); Call(window, "OnEnable");
                Assert.That(window.Draft, Is.SameAs(draft));
                Assert.That(window.Draft.Config.Planets[0].DisplayName, Is.EqualTo("恢复后的草稿"));
                Assert.That(Get(window, "state"), Is.SameAs(navigation));
                Assert.That(navigation.GetType().GetField("Page").GetValue(navigation), Is.EqualTo("advanced"));
                Assert.That(navigation.GetType().GetField("AdvancedPage").GetValue(navigation), Is.EqualTo("planet"));
                Assert.That(styles.Draft(style).OutlineAmplitude, Is.EqualTo(before + 1));
                Assert.That(style.OutlineAmplitude, Is.EqualTo(before));
                window.DiscardChanges();
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SerializedStyleDraftRestoresReferencesAndKeepsFieldConflictChecks()
        {
            var source = ScriptableObject.CreateInstance<CaveTerrainStyle>();
            var styles = new TerrainStyleDrafts();
            try
            {
                styles.Draft(source).OutlineAmplitude = source.OutlineAmplitude + 1;
                // 模拟域重载后的非序列化索引重建，作者和工作副本仍由序列化列表引用。
                var entries = Get(styles, "entries");
                var identity = typeof(TerrainStyleDrafts).GetProperty("VisualIdentity", Flags);
                string before = (string)identity.GetValue(styles);
                entries.GetType().GetMethod("Clear").Invoke(entries, null);
                Assert.That(styles.HasChanges, Is.True);
                Assert.That(identity.GetValue(styles), Is.EqualTo(before));
                float pending = styles.Draft(source).OutlineAmplitude;
                source.OutlineAmplitude += 3;
                Assert.That(identity.GetValue(styles), Is.EqualTo(before), "外部源不能伪装成工作副本变化");
                styles.Draft(source).OutlineAmplitude += 1;
                Assert.That(identity.GetValue(styles), Is.Not.EqualTo(before));
                styles.Draft(source).OutlineAmplitude = pending;
                Assert.Throws<System.InvalidOperationException>(() => styles.Apply());
                Assert.That(styles.Draft(source).OutlineAmplitude, Is.EqualTo(pending));
            }
            finally { styles.Dispose(); Object.DestroyImmediate(source); }
        }
    }
}
