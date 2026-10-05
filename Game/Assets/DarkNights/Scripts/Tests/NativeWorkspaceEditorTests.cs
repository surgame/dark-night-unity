using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using DarkNights.Runtime.Objects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 原生工坊的目标转交与原始保存／Undo 语义回归；仅使用临时 Definition。
    /// 焦点回调完成后核对配置与 dirty 保持；持久资产保存重开由独立框架导航回归覆盖。
    /// </summary>
    public sealed class NativeWorkspaceEditorTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private const string LastTarget = "ObjectDefinitionWorkshop_LastTarget";
        private static Type Bridge => typeof(DarkNights.Editor.DarkNightsMenu).Assembly.GetType("DarkNights.Editor.DarkNightsNativeWorkspace", true);
        private static void Open(ObjectDefinition definition) => Bridge.GetMethod("Workshop", Flags, null, new[] { typeof(ObjectDefinition) }, null).Invoke(null, new object[] { definition });
        private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);

        private static IEnumerator WaitForEditor()
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            while (EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline), "等待 Editor 导入完成超时");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ToolsOpenOnDemandReuseWindowsAndKeepJourneyDraft()
        {
            var assembly = typeof(DarkNights.Editor.DarkNightsMenu).Assembly;
            var types = new[] { typeof(GameCore.Editor.Objects.Runner.ObjectDefinitionViewer),
                typeof(DarkNights.Editor.Terrain.TerrainStylePreviewWindow), assembly.GetType("DarkNights.Editor.Terrain.TerrainBusinessWindow", true),
                typeof(DarkNights.Editor.Terrain.TerrainStylePreviewWindow) };
            var methods = new[] { "Viewer", "Journey", "Terrain", "Visual" };
            var before = Resources.FindObjectsOfTypeAll<EditorWindow>();
            int suiteCount = before.Count(window => window.GetType().Name == "ObjectArchetypeManagerWindow" || window.GetType().Name == "DIDebugWindow");
            try
            {
                for (int index = 0; index < types.Length; index++)
                {
                    yield return WaitForEditor();
                    Bridge.GetMethod(methods[index], Flags).Invoke(null, null); yield return null;
                    var window = Resources.FindObjectsOfTypeAll<EditorWindow>().Single(value => value.GetType() == types[index]);
                    Assert.That(window.docked, Is.False, methods[index] + " 应以独立浮窗打开");
                    object draft = index == 1 ? ((DarkNights.Editor.Terrain.TerrainStylePreviewWindow)window).Draft : null;
                    string original = draft == null ? "" : EditorJsonUtility.ToJson((UnityEngine.Object)draft);
                    yield return WaitForEditor();
                    Bridge.GetMethod(methods[index], Flags).Invoke(null, null); yield return null;
                    Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>().Single(value => value.GetType() == types[index]), Is.SameAs(window));
                    if (draft != null)
                    {
                        Assert.That(((DarkNights.Editor.Terrain.TerrainStylePreviewWindow)window).Draft, Is.SameAs(draft));
                        Assert.That(EditorJsonUtility.ToJson((UnityEngine.Object)draft), Is.EqualTo(original));
                    }
                }
                Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>().Count(window => window.GetType().Name == "ObjectArchetypeManagerWindow" ||
                    window.GetType().Name == "DIDebugWindow"), Is.EqualTo(suiteCount));
            }
            finally
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(window => types.Contains(window.GetType()) && !before.Contains(window)))
                    window.Close();
            }
        }

        [UnityTest]
        public IEnumerator WorkshopUsesOriginalWindowAndTargetWithoutEmbedding()
        {
            var existing = Resources.FindObjectsOfTypeAll<ObjectDefinitionWorkshopWindow>().FirstOrDefault();
            var previousTarget = existing == null ? null : Get(existing, "_targetDefinition") as ObjectDefinition;
            bool hadPreference = EditorPrefs.HasKey(LastTarget); string preference = EditorPrefs.GetString(LastTarget);
            EditorPrefs.DeleteKey(LastTarget);
            var source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/ShipTrade/item-pickaxe.asset");
            var copy = UnityEngine.Object.Instantiate(source); copy.hideFlags = HideFlags.HideAndDontSave;
            var hostType = typeof(DarkNights.Editor.DarkNightsMenu).Assembly.GetType("DarkNights.Editor.DarkNightsWorkbenchWindow", true);
            var host = (EditorWindow)ScriptableObject.CreateInstance(hostType); host.Show();
            ObjectDefinitionWorkshopWindow workshop = null;
            try
            {
                yield return null;
                string before = JsonUtility.ToJson(copy);
                yield return WaitForEditor();
                Open(copy); yield return null;
                workshop = Resources.FindObjectsOfTypeAll<ObjectDefinitionWorkshopWindow>().Single();
                Assert.That(workshop.docked, Is.False);
                Assert.That(Get(workshop, "_targetDefinition"), Is.SameAs(copy));
                Assert.That(workshop.rootVisualElement.parent, Is.Not.SameAs(host.rootVisualElement));
                Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(before));
                yield return WaitForEditor(); Open(copy); yield return null;
                Assert.That(Resources.FindObjectsOfTypeAll<ObjectDefinitionWorkshopWindow>().Single(), Is.SameAs(workshop));
                var next = UnityEngine.Object.Instantiate(source); next.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    yield return WaitForEditor(); Open(next); yield return null;
                    Assert.That(Get(workshop, "_targetDefinition"), Is.SameAs(next));
                    yield return WaitForEditor(); Open(copy); yield return null;
                }
                finally { UnityEngine.Object.DestroyImmediate(next); }

                int original = copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage;
                Undo.IncrementCurrentGroup(); Undo.RecordObject(copy, "原生工坊临时 Undo 验收");
                copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage = original + 7;
                EditorUtility.SetDirty(copy); Undo.FlushUndoRecordObjects();
                Undo.PerformUndo(); yield return null;
                Assert.That(copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage, Is.EqualTo(original));
                Undo.PerformRedo(); yield return null;
                Assert.That(copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage, Is.EqualTo(original + 7));
                workshop.GetType().GetMethod("SaveTargetDefinition", Flags).Invoke(workshop, null);
                Assert.That(copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage, Is.EqualTo(original + 7));
                Assert.That(File.ReadAllText(AssetDatabase.GetAssetPath(source)), Does.Contain("m_Name: item-pickaxe"));

                EditorUtility.ClearDirty(copy);
                string beforeFocus = JsonUtility.ToJson(copy);
                workshop.GetType().GetMethod("OnFocus", Flags).Invoke(workshop, null);
                bool focused = false;
                EditorApplication.CallbackFunction completed = () => focused = true;
                EditorApplication.delayCall += completed;
                try
                {
                    double deadline = EditorApplication.timeSinceStartup + 10;
                    workshop.Repaint();
                    // 后台 CLI 没有 Inspector 重绘时，显式驱动 Unity 的真实延迟队列；不调用业务回调替身。
                    typeof(EditorApplication).GetMethod("Internal_CallDelayFunctions", Flags).Invoke(null, null);
                    while (!focused && EditorApplication.timeSinceStartup < deadline) yield return null;
                }
                finally { EditorApplication.delayCall -= completed; }
                Assert.That(focused, Is.True, "工坊焦点后的 delayCall 尚未执行，不能记录诊断结论。");
                bool focusDirties = EditorUtility.IsDirty(copy);
                string diagnostic = "{\"navigation_clean\":" + (!focusDirties).ToString().ToLowerInvariant() +
                    ",\"focus_marks_dirty\":" + focusDirties.ToString().ToLowerInvariant() +
                    ",\"config_changed\":" + (JsonUtility.ToJson(copy) != beforeFocus).ToString().ToLowerInvariant() +
                    ",\"scope\":\"temporary_definition\"}";
                TestContext.WriteLine(diagnostic);
                Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(beforeFocus));
                Assert.That(focusDirties, Is.False, "原生导航不得把刷新标记为资产修改。");

                host.Close(); host = null; yield return null;
                Assert.That(workshop, Is.Not.Null);
                Assert.That(Get(workshop, "_targetDefinition"), Is.SameAs(copy));
            }
            finally
            {
                if (host != null) host.Close();
                if (existing == null && workshop != null) workshop.Close();
                else if (existing != null) existing.GetType().GetMethod("LoadTarget", Flags).Invoke(existing, new object[] { previousTarget });
                if (hadPreference) EditorPrefs.SetString(LastTarget, preference); else EditorPrefs.DeleteKey(LastTarget);
                Undo.ClearUndo(copy); UnityEngine.Object.DestroyImmediate(copy);
            }
        }
    }
}
