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
    /// <summary>聚合操作栏与采集辅助区的实际窗口回归；检查目标输入、资源保持及释放，临时对象不写作者资产。</summary>
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
        public void InvalidMiningAssemblyReportsWithoutModifyingDefinition(string failure)
        {
            var original = Asset("ShipTrade/item-pickaxe.asset");
            var copy = UnityEngine.Object.Instantiate(original); copy.hideFlags = HideFlags.HideAndDontSave;
            object panel = null;
            try
            {
                var config = copy.SharedConfigs.OfType<MiningToolConfig>().Single();
                if (failure == "missing-config") copy.SharedConfigs.Remove(config);
                if (failure == "missing-behaviour") copy.BehaviourTypes.Remove(typeof(MiningToolBehaviour).FullName);
                if (failure == "duplicate-config") copy.SharedConfigs.Add(new MiningToolConfig());
                if (failure == "wrong-whitelist") config.Deposits = new[] { new DefinitionReference(Asset("ShipTrade/item-pistol.asset").Guid) };
                if (failure == "missing-reference") config.Deposits = new[] { new DefinitionReference(DefinitionGuid.Parse(Guid.NewGuid().ToString("N"))) };
                bool dirty = EditorUtility.IsDirty(copy); string before = JsonUtility.ToJson(copy);
                var state = Create("MiningDefinitionPanelState"); Set(state, "Initialized", true); Set(state, "Tool", copy);
                Set(state, "Deposit", Asset("MineralDeposit/MineralDeposit.asset"));
                panel = Create("MiningDefinitionPanel", state);
                Assert.That(((HelpBox)Get(panel, "assembly")).messageType, Is.EqualTo(HelpBoxMessageType.Warning));
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
                yield return null; host.rootVisualElement.Add(panel);
                Assert.DoesNotThrow(() => panel.Q<ObjectField>("mining-deposit").value = copy);
                Assert.That(((HelpBox)Get(panel, "result")).messageType, Is.EqualTo(HelpBoxMessageType.Error));
            }
            finally { ((IDisposable)panel).Dispose(); UnityEngine.Object.DestroyImmediate(copy); host.Close(); }
        }

        [UnityTest]
        public IEnumerator TaskSwitchAndRebuildRestoreSelectionWithoutOpeningNativeEditors()
        {
            var window = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            var restored = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            int nativeCount = Resources.FindObjectsOfTypeAll<GameCore.Editor.Objects.Definition.ObjectDefinitionWorkshopWindow>().Length;
            var asset = Asset("ShipTrade/item-pistol.asset"); string before = JsonUtility.ToJson(asset); bool dirty = EditorUtility.IsDirty(asset);
            try
            {
                window.Show(); yield return null; Call(window, "CreateGUI");
                window.rootVisualElement.Q<ObjectField>("mining-tool").value = asset;
                var panel = Get(window, "miningPanel");
                Call(window, "ShowWorkspace", "ui");
                Assert.That((bool)Get(panel, "disposed"), Is.True);
                string path = "Assets/DarkNights/Res/UI/ShipEquipment/ShipEquipment.uxml";
                window.rootVisualElement.Q<PopupField<string>>("source-choice").value = path;
                Call(window, "ShowWorkspace", "journey"); Call(window, "ShowWorkspace", "ui");
                Assert.That(window.rootVisualElement.Q<PopupField<string>>("source-choice").value, Is.EqualTo(path));
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(window), restored); Call(restored, "CreateGUI");
                Assert.That(Get(restored, "workspace"), Is.EqualTo("ui"));
                Assert.That(restored.rootVisualElement.Q<PopupField<string>>("source-choice").value, Is.EqualTo(path));
                Call(restored, "ShowWorkspace", "objects");
                Assert.That(restored.rootVisualElement.Q<ObjectField>("mining-tool").value, Is.SameAs(asset));
                Assert.That(JsonUtility.ToJson(asset), Is.EqualTo(before));
                Assert.That(EditorUtility.IsDirty(asset), Is.EqualTo(dirty));
                Assert.That(Resources.FindObjectsOfTypeAll<GameCore.Editor.Objects.Definition.ObjectDefinitionWorkshopWindow>().Length, Is.EqualTo(nativeCount));
            }
            finally { window.Close(); UnityEngine.Object.DestroyImmediate(restored); }
        }

        [UnityTest]
        public IEnumerator SceneSearchRestoresAcrossTaskSwitchAndGuiRebuild()
        {
            var window = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow"));
            try
            {
                window.Show(); yield return null; Call(window, "CreateGUI");
                Call(window, "ShowWorkspace", "scenes");
                window.rootVisualElement.Q<ToolbarSearchField>("scene-search").value = "Assets/Scenes/Bootstrap.unity";
                Assert.That(window.rootVisualElement.Q<Button>("action-bootstrap"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q("launch-expedition"), Is.Null);
                var launcher = Get(window, "launcher");
                Call(window, "ShowWorkspace", "objects");
                Assert.That((bool)Get(launcher, "disposed"), Is.True);
                Call(window, "ShowWorkspace", "scenes");
                Assert.That(window.rootVisualElement.Q<ToolbarSearchField>("scene-search").value, Is.EqualTo("Assets/Scenes/Bootstrap.unity"));
                Call(window, "CreateGUI");
                Assert.That(window.rootVisualElement.Q<Button>("action-bootstrap"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<ToolbarSearchField>("scene-search").value, Is.EqualTo("Assets/Scenes/Bootstrap.unity"));
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator ReadOnlyMatchingTracksUndoRedoAndDisposesRefresh()
        {
            var copy = UnityEngine.Object.Instantiate(Asset("ShipTrade/item-pickaxe.asset")); copy.hideFlags = HideFlags.HideAndDontSave;
            object panel = null;
            var host = (EditorWindow)ScriptableObject.CreateInstance(Type("DarkNightsWorkbenchWindow")); host.Show();
            try
            {
                yield return null;
                var state = Create("MiningDefinitionPanelState"); Set(state, "Initialized", true); Set(state, "Tool", copy);
                Set(state, "Deposit", Asset("MineralDeposit/MineralDeposit.asset")); Set(state, "Mineral", false); Set(state, "Material", "stone");
                panel = Create("MiningDefinitionPanel", state); host.rootVisualElement.Add((VisualElement)panel);
                var config = copy.SharedConfigs.OfType<MiningToolConfig>().Single();
                string original = ((HelpBox)Get(panel, "result")).text;
                Undo.IncrementCurrentGroup(); Undo.RecordObject(copy, "采集辅助区临时 Undo 验收");
                config.AllMaterials = false; config.Materials = new[] { "__other_material__" };
                EditorUtility.SetDirty(copy); Undo.FlushUndoRecordObjects();
                Call(panel, "UpdateState"); string changed = ((HelpBox)Get(panel, "result")).text;
                Assert.That(changed, Is.Not.EqualTo(original));
                Undo.PerformUndo(); Call(panel, "UpdateState"); Assert.That(((HelpBox)Get(panel, "result")).text, Is.EqualTo(original));
                Undo.PerformRedo(); Call(panel, "UpdateState"); Assert.That(((HelpBox)Get(panel, "result")).text, Is.EqualTo(changed));
                ((IDisposable)panel).Dispose(); ((IDisposable)panel).Dispose();
                Assert.That((bool)Get(panel, "disposed"), Is.True);
            }
            finally { (panel as IDisposable)?.Dispose(); Undo.ClearUndo(copy); UnityEngine.Object.DestroyImmediate(copy); host.Close(); }
        }
    }
}
