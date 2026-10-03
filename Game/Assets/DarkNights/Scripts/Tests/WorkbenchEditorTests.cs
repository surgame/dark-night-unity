using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>真实 Editor 工作台的保存守卫、窗口状态恢复、选择缓存与 Undo 回归；仅修改临时对象，不写作者资产。</summary>
    public sealed class WorkbenchEditorTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const string Root = "Assets/DarkNights/Res/Objects/";
        private static Type Type(string name) => typeof(DarkNights.Editor.DarkNightsMenu).Assembly.GetType("DarkNights.Editor." + name, true);
        private static object Get(object value, string field) => value.GetType().GetField(field, Flags).GetValue(value);
        private static void Set(object value, string field, object next) => value.GetType().GetField(field, Flags).SetValue(value, next);
        private static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, Flags).Invoke(value, args);
        private static object Create(string name, params object[] args) => Activator.CreateInstance(Type(name), Flags, null, args, null);
        private static ObjectDefinition Asset(string path) => AssetDatabase.LoadAssetAtPath<ObjectDefinition>(Root + path);

        [TestCase("missing-config")]
        [TestCase("missing-behaviour")]
        [TestCase("duplicate-config")]
        [TestCase("wrong-whitelist")]
        [TestCase("missing-reference")]
        public void InvalidMiningAssemblyCannotSaveOrDirtySource(string failure)
        {
            var original = Asset("ShipTrade/item-pickaxe.asset");
            var copy = UnityEngine.Object.Instantiate(original); copy.hideFlags = HideFlags.HideAndDontSave;
            object panel = null;
            try
            {
                var config = copy.SharedConfigs.OfType<MiningToolConfig>().Single();
                Assert.That(config, Is.Not.SameAs(original.SharedConfigs.OfType<MiningToolConfig>().Single()));
                if (failure == "missing-config") copy.SharedConfigs.Remove(config);
                if (failure == "missing-behaviour") copy.BehaviourTypes.Remove(typeof(MiningToolBehaviour).FullName);
                if (failure == "duplicate-config") copy.SharedConfigs.Add(new MiningToolConfig());
                if (failure == "wrong-whitelist") config.Deposits = new[] { new DefinitionReference(Asset("ShipTrade/item-pistol.asset").Guid) };
                if (failure == "missing-reference") config.Deposits = new[] { new DefinitionReference(DefinitionGuid.Parse(Guid.NewGuid().ToString("N"))) };
                var state = Create("MiningDefinitionPanelState"); Set(state, "Initialized", true); Set(state, "Tool", copy);
                panel = Create("MiningDefinitionPanel", state);
                bool dirty = EditorUtility.IsDirty(copy); string before = JsonUtility.ToJson(copy);
                Call(panel, "SaveSelected");
                Assert.That(((HelpBox)Get(panel, "result")).messageType, Is.EqualTo(HelpBoxMessageType.Error));
                Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(before));
                Assert.That(EditorUtility.IsDirty(copy), Is.EqualTo(dirty));
            }
            finally { (panel as IDisposable)?.Dispose(); UnityEngine.Object.DestroyImmediate(copy); }
        }

        [UnityTest]
        public IEnumerator DuplicateTargetSelectionShowsErrorWithoutThrowingCallback()
        {
            var copy = UnityEngine.Object.Instantiate(Asset("MineralDeposit/MineralDeposit.asset")); copy.hideFlags = HideFlags.HideAndDontSave;
            copy.SharedConfigs.Add(new MineralDepositRuleConfig());
            var panel = (VisualElement)Create("MiningDefinitionPanel");
            var host = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow")); host.Show();
            try
            {
                yield return null;
                host.rootVisualElement.Add(panel);
                Call(panel, "ShowTab", "match");
                var picker = panel.Children().OfType<ObjectField>().Single(field => field.name == "mining-deposit");
                Assert.DoesNotThrow(() => picker.value = copy);
                Assert.That(((HelpBox)Get(panel, "result")).messageType, Is.EqualTo(HelpBoxMessageType.Error));
            }
            finally { ((IDisposable)panel).Dispose(); UnityEngine.Object.DestroyImmediate(copy); host.Close(); }
        }

        [Test]
        public void WindowSerializationRestoresMiningAndBrowserViewState()
        {
            var window = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            var restored = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            try
            {
                var tool = Asset("ShipTrade/item-pistol.asset");
                var mining = Get(window, "miningSelection");
                Set(mining, "Initialized", true); Set(mining, "Tool", tool); Set(mining, "Tab", "match");
                Set(mining, "Material", "gold"); Set(mining, "Mineral", false); Set(mining, "Rare", true);
                var browser = Get(window, "browserSelection"); Set(browser, "Selected", tool); Set(browser, "Query", "item.pistol");
                Set(window, "selectedId", "mining");
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(window), restored);
                Call(restored, "CreateGUI");
                var panel = Get(restored, "miningPanel");
                Assert.That(Get(Get(restored, "miningSelection"), "Tool"), Is.SameAs(tool));
                Assert.That(((TextField)Get(panel, "material")).value, Is.EqualTo("gold"));
                Assert.That(((DropdownField)Get(panel, "kind")).value, Is.EqualTo("前景岩壁"));
                Assert.That(((Toggle)Get(panel, "rare")).value, Is.True);
                Set(restored, "selectedId", "objects"); Call(restored, "RebuildNavigation");
                var browserPanel = Get(restored, "definitions");
                var editor = Get(browserPanel, "inspector");
                Assert.That(editor.GetType().GetProperty("Target", Flags).GetValue(editor), Is.SameAs(tool));
            }
            finally { UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(restored); }
        }

        [UnityTest]
        public IEnumerator SearchRetainsActiveSourceAndGuiRebuildRestoresIt()
        {
            var window = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            try
            {
                Set(window, "selectedId", "equipment"); window.Show(); yield return null; Call(window, "CreateGUI");
                var details = (ScrollView)Get(window, "details");
                var choice = details.Children().OfType<PopupField<string>>().Single();
                string path = Root + "ShipTrade/item-pistol.asset"; choice.value = path;
                var editor = Get(window, "definitionEditor");
                window.rootVisualElement.Q<ToolbarSearchField>("workbench-search").value = "飞船交易";
                Assert.That(Get(window, "definitionEditor"), Is.SameAs(editor));
                Call(window, "CreateGUI");
                details = (ScrollView)Get(window, "details");
                Assert.That(details.Children().OfType<PopupField<string>>().Single().value, Is.EqualTo(path));
                Assert.That((bool)Get(editor, "disposed"), Is.True);
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator BrowserEmptySearchPreservesLastSelectedDefinition()
        {
            var browser = (VisualElement)Create("DarkNightsDefinitionBrowser");
            var host = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow")); host.Show();
            try
            {
                yield return null;
                host.rootVisualElement.Add(browser);
                var search = browser.Children().OfType<ToolbarSearchField>().Single();
                search.value = "item.pistol";
                var editor = Get(browser, "inspector");
                Call(browser, "Reload"); Assert.That(Get(browser, "inspector"), Is.SameAs(editor));
                search.value = "__missing_definition__"; Assert.That(Get(browser, "inspector"), Is.Null);
                search.value = ""; editor = Get(browser, "inspector");
                Assert.That(editor.GetType().GetProperty("Target", Flags).GetValue(editor), Is.SameAs(Asset("ShipTrade/item-pistol.asset")));
            }
            finally { ((IDisposable)browser).Dispose(); host.Close(); }
        }

        [UnityTest]
        public IEnumerator WorkspaceSwitchReleasesInspectorAndRestoresIndependentSearches()
        {
            var window = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            var restored = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            try
            {
                Set(window, "selectedId", "equipment"); window.Show(); yield return null; Call(window, "CreateGUI");
                string path = Root + "ShipTrade/item-pistol.asset";
                window.rootVisualElement.Q<PopupField<string>>("source-choice").value = path;
                var original = Asset("ShipTrade/item-pistol.asset");
                bool dirty = EditorUtility.IsDirty(original); string before = JsonUtility.ToJson(original);
                var editor = Get(window, "definitionEditor");
                Call(window, "ShowWorkspace", "scenes");
                Assert.That((bool)Get(editor, "disposed"), Is.True);
                window.rootVisualElement.Q<ToolbarSearchField>("workbench-search").value = "Assets/Scenes/Bootstrap.unity";
                Assert.That(window.rootVisualElement.Q<Button>("action-bootstrap"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q("launch-expedition"), Is.Null);
                Call(window, "ShowWorkspace", "tools");
                window.rootVisualElement.Q<ToolbarSearchField>("workbench-search").value = "岩壁";
                Call(window, "ShowWorkspace", "scenes");
                Assert.That(window.rootVisualElement.Q<ToolbarSearchField>("workbench-search").value, Is.EqualTo("Assets/Scenes/Bootstrap.unity"));
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(window), restored); Call(restored, "CreateGUI");
                Assert.That(Get(restored, "workspace"), Is.EqualTo("scenes"));
                Call(restored, "ShowWorkspace", "editors");
                Assert.That(restored.rootVisualElement.Q<PopupField<string>>("source-choice").value, Is.EqualTo(path));
                Assert.That(JsonUtility.ToJson(original), Is.EqualTo(before));
                Assert.That(EditorUtility.IsDirty(original), Is.EqualTo(dirty));
            }
            finally { window.Close(); UnityEngine.Object.DestroyImmediate(restored); }
        }

        [UnityTest]
        public IEnumerator EmptySearchReleasesEditorAndClearingItRestoresSelectedSource()
        {
            var window = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            try
            {
                Set(window, "selectedId", "equipment"); window.Show(); yield return null; Call(window, "CreateGUI");
                string path = Root + "ShipTrade/item-bomb.asset";
                window.rootVisualElement.Q<PopupField<string>>("source-choice").value = path;
                var editor = Get(window, "definitionEditor");
                var search = window.rootVisualElement.Q<ToolbarSearchField>("workbench-search");
                search.value = "__missing_workbench__";
                Assert.That((bool)Get(editor, "disposed"), Is.True);
                Assert.That(Get(window, "definitionEditor"), Is.Null);
                search.value = "";
                Assert.That(window.rootVisualElement.Q<PopupField<string>>("source-choice").value, Is.EqualTo(path));
            }
            finally { window.Close(); }
        }

        [Test]
        public void NativeSaveRejectsTransientDefinitionWithoutModifyingIt()
        {
            var copy = UnityEngine.Object.Instantiate(Asset("ShipTrade/item-pickaxe.asset")); copy.hideFlags = HideFlags.HideAndDontSave;
            var editor = Create("DarkNightsDefinitionEditor", copy, false);
            try
            {
                string before = JsonUtility.ToJson(copy); bool dirty = EditorUtility.IsDirty(copy);
                Assert.That((bool)Call(editor, "Save"), Is.False);
                Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(before));
                Assert.That(EditorUtility.IsDirty(copy), Is.EqualTo(dirty));
            }
            finally { ((IDisposable)editor).Dispose(); UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void NativeEditorRefreshesActualConfigAfterUndoAndRedo()
        {
            var copy = UnityEngine.Object.Instantiate(Asset("ShipTrade/item-pickaxe.asset")); copy.hideFlags = HideFlags.HideAndDontSave;
            object editor = null;
            try
            {
                editor = Create("DarkNightsDefinitionEditor", copy, false);
                int original = copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage;
                Undo.IncrementCurrentGroup(); Undo.RecordObject(copy, "工作台临时 Undo 验收");
                copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage = original + 7;
                EditorUtility.SetDirty(copy); Undo.FlushUndoRecordObjects();
                Undo.PerformUndo(); Call(editor, "OnUndoRedo");
                Assert.That(copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage, Is.EqualTo(original));
                Undo.PerformRedo(); Call(editor, "OnUndoRedo");
                Assert.That(copy.SharedConfigs.OfType<MiningToolConfig>().Single().Damage, Is.EqualTo(original + 7));
                ((IDisposable)editor).Dispose();
                Assert.That((bool)Call(editor, "Save"), Is.False);
            }
            finally { (editor as IDisposable)?.Dispose(); Undo.ClearUndo(copy); UnityEngine.Object.DestroyImmediate(copy); }
        }
    }
}
