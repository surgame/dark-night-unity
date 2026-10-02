using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DarkNights.Editor;
using DarkNights.Entry;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>快速选择及完整场景导航的编辑合同；记忆只存 ID，盘点不遗漏场景，模板保留但不进入正式构建。</summary>
    public sealed class QuickTestEditorTests
    {
        [Test]
        public void PreferencePersistsSelectionAndRetiredIdsFallBack()
        {
            string key = QuickTestSelection.PreferenceKey;
            bool existed = EditorPrefs.HasKey(key); string previous = EditorPrefs.GetString(key);
            try
            {
                EditorPrefs.SetString(key, "retired-test");
                Assert.That(QuickTestSelection.SelectedId, Is.EqualTo(QuickTestPreset.LandedPickaxeId));
                QuickTestSelection.Select(QuickTestPreset.LandedPickaxeId);
                Assert.That(EditorPrefs.GetString(key), Is.EqualTo(QuickTestPreset.LandedPickaxeId));
                Assert.Throws<ArgumentException>(() => QuickTestSelection.Select("../Saves"));
                Assert.That(QuickTestSelection.SelectedId, Is.EqualTo(QuickTestPreset.LandedPickaxeId));
            }
            finally { if (existed) EditorPrefs.SetString(key, previous); else EditorPrefs.DeleteKey(key); }
        }

        [Test]
        public void SceneNavigationCoversEveryProjectSceneAndRetiredItemsOnlyLocate()
        {
            var catalog = typeof(GameSceneCatalogOrganizer).Assembly.GetType("DarkNights.Editor.GameSceneWorkbenchCatalog");
            var entries = ((IEnumerable)catalog.GetField("Entries", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Cast<object>().ToArray();
            const BindingFlags properties = BindingFlags.NonPublic | BindingFlags.Instance;
            var paths = entries.SelectMany(entry => (System.Collections.Generic.IEnumerable<string>)
                entry.GetType().GetProperty("Assets", properties).GetValue(entry)).Distinct().OrderBy(value => value).ToArray();
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(value => value).ToArray();
            Assert.That(scenes.Length, Is.EqualTo(16)); Assert.That(paths, Is.EqualTo(scenes));
            foreach (var entry in entries.Where(e => (string)e.GetType().GetProperty("Group", properties).GetValue(e) == "已退役"))
                Assert.That((string)entry.GetType().GetProperty("ActionLabel", properties).GetValue(entry), Is.EqualTo("在 Project 中定位"));
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Does.Not.Contain("Assets/Scenes/SampleScene.unity"));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/SampleScene.unity"), Is.Not.Null);
            Assert.That(System.IO.File.ReadAllText("ProjectSettings/ProjectSettings.asset"), Does.Contain("templateDefaultScene: Assets/Scenes/SampleScene.unity"));
        }
    }
}
