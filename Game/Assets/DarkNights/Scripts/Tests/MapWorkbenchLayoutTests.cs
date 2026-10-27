using DarkNights.Editor.Terrain;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>
    /// 任务式地图工作台的UITK结构合同；只构建编辑器树，不启动Play、不生成地图或写作者资产。
    /// 现有草稿与正式配置保持，完整人工布局和实际生成／冻结仍需独立授权验收。
    /// </summary>
    public sealed class MapWorkbenchLayoutTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        [Test]
        public void ThreePagesShareCanvasAndOneCurrentConfiguration()
        {
            var window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            try
            {
                string before = window.Draft.Config.CanonicalIdentity();
                window.CreateGUI();
                var root = window.rootVisualElement;
                var tabs = root.Q("taskNavigation").Query<Button>().ToList();
                CollectionAssert.AreEqual(new[] { "地形", "矿物", "环境" }, tabs.ConvertAll(x => x.text));
                Assert.That(root.Q<IMGUIContainer>("terrainCanvas"), Is.Not.Null);
                Assert.That(root.Query<IMGUIContainer>().ToList().Count, Is.EqualTo(1),
                    "默认来源页不能把整个旧OnGUI嵌入新窗口");
                Assert.That(root.Q<Label>("canvasIdentity"), Is.Not.Null);
                Assert.That(root.Q<Label>("mapDocumentName"), Is.Not.Null);
                Assert.That(root.Q("currentDocument"), Is.Null, "地图选择统一使用左侧文件列表");
                Assert.That(root.Q<Label>("newGameReference"), Is.Null);
                Assert.That(root.Q<Button>("saveDraft").text, Is.EqualTo("保存"));
                Assert.That(window.Draft.Config.CanonicalIdentity(), Is.EqualTo(before));
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TaskRoundTripDoesNotMutateAuthorOrDuplicateCanvas()
        {
            var window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            try
            {
                window.CreateGUI();
                string before = window.Draft.Config.CanonicalIdentity();
                foreach (string page in new[] { "space", "minerals", "environment", "extras", "appearance", "release", "source" })
                {
                    typeof(MapWorkbenchWindow).GetMethod("SelectPage", Flags).Invoke(window, new object[] { page });
                    Assert.That(window.rootVisualElement.Query<IMGUIContainer>().ToList().Count, Is.EqualTo(1), page);
                    var selected = window.rootVisualElement.Q("taskNavigation").Query<Button>().ToList()
                        .FindAll(x => x.ClassListContains("map-selected"));
                    Assert.That(selected.Count, Is.EqualTo(page == "space" || page == "minerals" || page == "environment" ? 1 : 0));
                    Assert.That(window.rootVisualElement.Q<Label>("canvasLegend"), Is.Not.Null);
                    Assert.That(window.Draft.Config.CanonicalIdentity(), Is.EqualTo(before), page);
                }
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void GlobalActionsAndInspectorStayOutsideScrollingProperties()
        {
            var window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            try
            {
                window.CreateGUI();
                var root = window.rootVisualElement;
                var properties = root.Q<ScrollView>("taskProperties");
                Assert.That(properties.Contains(root.Q<Button>("updateCandidate")), Is.False);
                Assert.That(root.Q("headerActions").Contains(root.Q<Button>("saveDraft")), Is.True);
                Assert.That(properties.Contains(root.Q<Label>("inspectorTitle")), Is.False);
                Assert.That(properties.Contains(root.Q<Button>("nextTask")), Is.False);
                Assert.That(root.Q<Button>("discardWindow"), Is.Null, "破坏性操作收进更多菜单");
                Assert.That(root.Q<ToolbarMenu>("displayMenu"), Is.Null);
                Assert.That(root.Q("canvasTools").Contains(root.Q<Button>("displayFit")), Is.True);
                var modes = root.Q<ToggleButtonGroup>("mapModeSwitch");
                Assert.That(modes.isMultipleSelection, Is.False);
                Assert.That(modes.allowEmptySelection, Is.False);
                Assert.That(modes.Contains(root.Q<Button>("designMode")), Is.True);
                Assert.That(modes.Contains(root.Q<Button>("renderMode")), Is.True);
                Assert.That(root.Q("canvasTools").Contains(modes), Is.False);
                typeof(MapWorkbenchWindow).GetMethod("SelectPage", Flags).Invoke(window, new object[] { "minerals" });
                Assert.That(root.Q<Label>("inspectorTitle").text, Is.EqualTo("矿物"));
                Assert.That(root.Q("headerActions").Contains(root.Q<ToolbarMenu>("fileMenu")), Is.True);
                Assert.That(root.Q("headerActions").Contains(root.Q<ToolbarMenu>("workbenchMore")), Is.True);
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DisplayIconsTogglePresentationWithoutChangingConfiguration()
        {
            var window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            try
            {
                window.CreateGUI();
                string before = window.Draft.Config.CanonicalIdentity();
                var toggles = window.rootVisualElement.Q("canvasTools").Query<Toggle>(className: "map-display-toggle").ToList();
                Assert.That(toggles.Count, Is.EqualTo(7));
                foreach (var toggle in toggles)
                {
                    Assert.That(toggle.tooltip, Is.Not.Empty);
                    Assert.That(toggle.Q(className: "map-display-icon"), Is.Not.Null);
                    bool selected = toggle.value;
                    toggle.value = !selected;
                    Assert.That(window.Draft.Config.CanonicalIdentity(), Is.EqualTo(before), toggle.name);
                    toggle.value = selected;
                    Assert.That(toggle.value, Is.EqualTo(selected), toggle.name);
                }
                Assert.That(window.Draft.Config.CanonicalIdentity(), Is.EqualTo(before));
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DraftGeologyOverlayIsScopedToCandidateCollapseTaskEvenWhenInputIsStale()
        {
            var window = ScriptableObject.CreateInstance<MapWorkbenchWindow>();
            try
            {
                var state = typeof(MapWorkbenchWindow).GetField("state", Flags).GetValue(window);
                var preview = typeof(MapWorkbenchWindow).GetField("preview", Flags).GetValue(window);
                var type = preview.GetType();
                type.GetProperty("IsCandidate", Flags).SetValue(preview, true);
                window.Draft.Config.Geology.Enabled = true;
                state.GetType().GetField("Sites").SetValue(state, true);
                state.GetType().GetField("Extra").SetValue(state, "collapse");
                foreach (string page in new[] { "source", "space", "minerals", "appearance", "release", "advanced" })
                {
                    state.GetType().GetField("Page").SetValue(state, page);
                    Assert.That(type.GetProperty("ShowGeologyDraft", Flags).GetValue(preview), Is.False, page);
                }
                state.GetType().GetField("Page").SetValue(state, "extras");
                Assert.That(type.GetProperty("ShowGeologyDraft", Flags).GetValue(preview), Is.True);
                type.GetProperty("IsCandidate", Flags).SetValue(preview, false);
                Assert.That(type.GetProperty("ShowGeologyDraft", Flags).GetValue(preview), Is.False, "冻结版不得混入草图");
                type.GetProperty("IsCandidate", Flags).SetValue(preview, true);
                state.GetType().GetField("Extra").SetValue(state, "nest");
                Assert.That(type.GetProperty("ShowGeologyDraft", Flags).GetValue(preview), Is.False);
                state.GetType().GetField("Extra").SetValue(state, "collapse");
                state.GetType().GetField("Sites").SetValue(state, false);
                Assert.That(type.GetProperty("ShowGeologyDraft", Flags).GetValue(preview), Is.False);
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                Object.DestroyImmediate(window);
            }
        }
    }
}
