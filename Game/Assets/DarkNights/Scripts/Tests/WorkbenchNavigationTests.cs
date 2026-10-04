using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>
    /// 工作台导航的行为回归：全局检索、清除返回、重建恢复以及菜单命令迁移。
    /// 只操作临时窗口与导航状态，不运行安装、构建或验证命令，不保存作者资源。
    /// </summary>
    public sealed class WorkbenchNavigationTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static Assembly Assembly => typeof(DarkNights.Editor.DarkNightsMenu).Assembly;
        private static Type WindowType => Assembly.GetType("DarkNights.Editor.DarkNightsWorkbenchWindow", true);
        private static void Call(EditorWindow window, string method, params object[] args) =>
            WindowType.GetMethod(method, Flags).Invoke(window, args);

        [UnityTest]
        public IEnumerator SearchFindsToolsScenesAndMaintenanceAndRestoresTask()
        {
            var window = (EditorWindow)ScriptableObject.CreateInstance(WindowType);
            try
            {
                window.Show(); yield return null;
                Call(window, "Navigate", "ui");
                var search = window.rootVisualElement.Q<TextField>("workbench-search");
                search.value = "矿镐";
                Assert.That(window.rootVisualElement.Q<Button>("action-mining"), Is.Not.Null);
                search.value = "Assets/Scenes/Bootstrap.unity";
                Assert.That(window.rootVisualElement.Q<Button>("action-bootstrap"), Is.Not.Null);
                search.value = "Build/Windows IL2CPP";
                Assert.That(window.rootVisualElement.Q<Button>("action-command-DarkNightsMenu-BuildIl2Cpp"), Is.Not.Null);
                Call(window, "CreateGUI");
                search = window.rootVisualElement.Q<TextField>("workbench-search");
                Assert.That(search.value, Is.EqualTo("Build/Windows IL2CPP"));
                search.value = "__no_matching_workbench_entry__";
                Assert.That(window.rootVisualElement.Q(className: "dn-empty"), Is.Not.Null);
                search.value = "";
                Assert.That(window.rootVisualElement.Q<Label>("task-title").text, Is.EqualTo("界面资源"));
                Assert.That(window.rootVisualElement.Q<PopupField<string>>("source-choice"), Is.Not.Null);
            }
            finally { window.Close(); }
        }

        [Test]
        public void MigratedMaintenanceCommandsAreUniqueDiscoverableAndNotMenus()
        {
            var attribute = Assembly.GetType("DarkNights.Editor.DarkNightsWorkbenchCommandAttribute", true);
            var methods = Assembly.GetTypes().SelectMany(type => type.GetMethods(Flags))
                .Where(method => method.IsDefined(attribute, false)).ToArray();
            Assert.That(methods.Length, Is.EqualTo(36));
            foreach (var method in methods)
            {
                Assert.That(method.IsStatic && method.ReturnType == typeof(void) && method.GetParameters().Length == 0, Is.True);
                Assert.That(method.GetCustomAttributes<MenuItem>(), Is.Empty, method.Name);
            }
            var entries = (IEnumerable)Assembly.GetType("DarkNights.Editor.DarkNightsWorkbenchCommands", true)
                .GetProperty("Entries", Flags).GetValue(null);
            var ids = entries.Cast<object>().Select(entry => (string)entry.GetType().GetProperty("Id", Flags).GetValue(entry)).ToArray();
            Assert.That(ids.Length, Is.EqualTo(methods.Length));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }

        [UnityTest]
        public IEnumerator ExpandedContentKeepsHeaderSeparateAndSearchHandlesKeyboard()
        {
            var window = (EditorWindow)ScriptableObject.CreateInstance(WindowType);
            try
            {
                window.Show(); yield return null;
                foreach (var size in new[] { new Vector2(1120, 760), new Vector2(680, 480) })
                {
                    window.position = new Rect(new Vector2(80, 80), size);
                    window.rootVisualElement.Q<Foldout>("mining-validation").value = true;
                    window.Repaint(); yield return null; yield return null;
                    var description = window.rootVisualElement.Q<Label>("task-description");
                    var shortcuts = window.rootVisualElement.Q("scene-shortcuts");
                    Assert.That(description.worldBound.yMax, Is.LessThanOrEqualTo(shortcuts.worldBound.yMin), size.ToString());
                    Assert.That(window.rootVisualElement.Q<ScrollView>("task-content").worldBound.height, Is.GreaterThan(60));
                }
                var search = window.rootVisualElement.Q<TextField>("workbench-search");
                window.rootVisualElement.Q<Button>("workspace-objects").Focus();
                using (var key = KeyDownEvent.GetPooled('\0', KeyCode.K, EventModifiers.Control))
                    window.rootVisualElement.SendEvent(key);
                yield return null;
                var focused = window.rootVisualElement.focusController.focusedElement as VisualElement;
                Assert.That(focused == search || search.Contains(focused), Is.True);
                search.value = "Viewer";
                using (var key = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None))
                    window.rootVisualElement.SendEvent(key);
                Assert.That(search.value, Is.Empty);
                Assert.That(window.rootVisualElement.Q<Label>("task-title").text, Is.EqualTo("对象与装备"));
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator OpeningDockedWorkbenchUndocksSameInstanceAndPreservesNavigation()
        {
            var existing = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(window => window.GetType() == WindowType);
            var create = typeof(EditorWindow).GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method =>
                method.Name == "GetWindow" && method.IsGenericMethodDefinition && method.GetParameters().Length == 3 &&
                method.GetParameters()[0].ParameterType == typeof(string));
            var window = existing ?? (EditorWindow)create.MakeGenericMethod(WindowType)
                .Invoke(null, new object[] { "Dark Nights 工作台", true, new[] { typeof(SceneView) } });
            string previous = (string)WindowType.GetField("workspace", Flags).GetValue(window);
            try
            {
                yield return null;
                Call(window, "Navigate", "journey");
                WindowType.GetMethod("Open", Flags).Invoke(null, null); yield return null;
                Assert.That(window.docked, Is.False);
                Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>().Single(value => value.GetType() == WindowType), Is.SameAs(window));
                Assert.That(window.rootVisualElement.Q<Label>("task-title").text, Is.EqualTo("星球与航程"));
            }
            finally { if (existing == null) window.Close(); else Call(window, "Navigate", previous); }
        }
    }
}
