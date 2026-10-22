using DarkNights.Editor.Terrain;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>
    /// 任务式地图工作台的UITK结构合同；只构建编辑器树，不启动Play、不生成地图或写作者资产。
    /// 现有草稿与正式配置保持，完整人工布局和实际生成／冻结仍需独立授权验收。
    /// </summary>
    public sealed class TunerWorkbenchLayoutTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        [Test]
        public void NativeTasksKeepCanvasAndSourceIdentityWithoutOldWindowShell()
        {
            var window = ScriptableObject.CreateInstance<TerrainStylePreviewWindow>();
            try
            {
                string before = window.Draft.Config.CanonicalIdentity();
                window.CreateGUI();
                var root = window.rootVisualElement;
                foreach (string name in new[] { "source", "space", "minerals", "extras", "appearance", "release", "advanced" })
                    Assert.That(root.Q<Button>("task-" + name), Is.Not.Null, name);
                Assert.That(root.Q<IMGUIContainer>("terrainCanvas"), Is.Not.Null);
                Assert.That(root.Query<IMGUIContainer>().ToList().Count, Is.EqualTo(1),
                    "默认来源页不能把整个旧OnGUI嵌入新窗口");
                Assert.That(root.Q<Label>("canvasIdentity"), Is.Not.Null);
                Assert.That(root.Q<Label>("newGameReference"), Is.Not.Null);
                Assert.That(root.Q<Button>("saveDraft").text, Is.EqualTo("保存地图草稿"));
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
            var window = ScriptableObject.CreateInstance<TerrainStylePreviewWindow>();
            try
            {
                window.CreateGUI();
                string before = window.Draft.Config.CanonicalIdentity();
                foreach (string page in new[] { "space", "minerals", "extras", "appearance", "release", "source" })
                {
                    typeof(TerrainStylePreviewWindow).GetMethod("SelectPage", Flags).Invoke(window, new object[] { page });
                    Assert.That(window.rootVisualElement.Query<IMGUIContainer>().ToList().Count, Is.EqualTo(1), page);
                    Assert.That(window.rootVisualElement.Q<Button>("task-" + page).ClassListContains("map-selected"), Is.True);
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
            var window = ScriptableObject.CreateInstance<TerrainStylePreviewWindow>();
            try
            {
                window.CreateGUI();
                var root = window.rootVisualElement;
                var properties = root.Q<ScrollView>("taskProperties");
                Assert.That(root.Q("headerActions").Contains(root.Q<Button>("updateCandidate")), Is.True);
                Assert.That(root.Q("headerActions").Contains(root.Q<Button>("saveDraft")), Is.True);
                Assert.That(properties.Contains(root.Q<Label>("inspectorTitle")), Is.False);
                Assert.That(properties.Contains(root.Q<Button>("nextTask")), Is.False);
                Assert.That(root.Q<Button>("discardWindow"), Is.Null, "破坏性操作收进更多菜单");
                Assert.That(root.Q<Button>("fitCanvas"), Is.Not.Null);
                typeof(TerrainStylePreviewWindow).GetMethod("SelectPage", Flags).Invoke(window, new object[] { "minerals" });
                Assert.That(root.Q<Label>("inspectorTitle").text, Is.EqualTo("矿物分布"));
                Assert.That(properties.Query<Foldout>().ToList().Find(f => f.text == "区域预算与失败原因").value, Is.False);
            }
            finally
            {
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void CompactLayersExposeCurrentStateWithoutChangingAuthorDraft()
        {
            var window = ScriptableObject.CreateInstance<TerrainStylePreviewWindow>();
            try
            {
                window.CreateGUI();
                string before = window.Draft.Config.CanonicalIdentity();
                var layers = window.rootVisualElement.Q("canvasTools").Query<Toggle>().ToList();
                Assert.That(layers.Count, Is.EqualTo(7));
                foreach (var toggle in layers)
                {
                    Assert.That(toggle.label, Is.Null.Or.Empty, "不使用带大标签留白的BaseField布局");
                    Assert.That(toggle.text, Is.Not.Empty);
                    Assert.That(toggle.ClassListContains("map-selected"), Is.EqualTo(toggle.value), toggle.text);
                    Assert.That(toggle.tooltip, Is.Not.Empty);
                }
                Assert.That(window.Draft.Config.CanonicalIdentity(), Is.EqualTo(before));
                foreach (var button in window.rootVisualElement.Q("taskNavigation").Query<Button>().ToList())
                    Assert.That(button.text, Is.Null.Or.Empty, "导航文字由标题和副标题构成，没有额外符号");
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
            var window = ScriptableObject.CreateInstance<TerrainStylePreviewWindow>();
            try
            {
                var state = typeof(TerrainStylePreviewWindow).GetField("state", Flags).GetValue(window);
                var preview = typeof(TerrainStylePreviewWindow).GetField("preview", Flags).GetValue(window);
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
