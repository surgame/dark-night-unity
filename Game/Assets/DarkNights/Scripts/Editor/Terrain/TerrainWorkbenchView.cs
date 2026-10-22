using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 工作台UITK布局与任务导航；常驻画布、当前属性及版本提示分离，切任务不会重置作者输入。
    /// 原生绑定随页面释放，定时只更新轻量状态；仅已有渲染画布保留IMGUI，不包裹整套旧窗口。
    /// </summary>
    internal sealed class TerrainWorkbenchView : IDisposable
    {
        private const string Layout = "Assets/DarkNights/Res/Editor/Terrain/TerrainWorkbench.uxml";
        private readonly TerrainStylePreviewWindow owner;
        private readonly VisualElement host;
        private TerrainWorkbenchChrome chrome;
        private VisualElement root;
        private ScrollView properties;
        private IMGUIContainer canvas;
        private TerrainWorkbenchFields fields;
        private TerrainWorkbenchAppearancePage appearance;
        private TerrainWorkbenchAdvancedPage advanced;
        private TerrainWorkbenchReleasePage release;
        private string officialIdentity, referenceError = "";

        internal TerrainWorkbenchView(TerrainStylePreviewWindow owner, VisualElement host)
        { this.owner = owner; this.host = host; }

        internal void Build()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout);
            if (tree == null) { host.Add(new HelpBox("地图工作台布局尚未导入：" + Layout, HelpBoxMessageType.Error)); return; }
            tree.CloneTree(host); root = host.Q("mapWorkbench");
            root.EnableInClassList("map-light", !EditorGUIUtility.isProSkin);
            properties = root.Q<ScrollView>("taskProperties");
            chrome = new TerrainWorkbenchChrome(owner, root);
            canvas = new IMGUIContainer(() => owner.Preview.Draw(new Rect(Vector2.zero, canvas.contentRect.size)))
            { name = "terrainCanvas", focusable = true };
            root.Q("canvasHost").Add(canvas);
            owner.Preview.RepaintRequested += RepaintCanvas;
            host.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            root.EnableInClassList("map-narrow", owner.position.width < 1000);
            BuildPage(); Refresh();
        }

        internal void BuildPage()
        {
            if (properties == null) return;
            appearance?.Dispose(); appearance = null; advanced?.Dispose(); advanced = null; release = null;
            fields?.Dispose(); properties.Clear();
            fields = new TerrainWorkbenchFields(owner, properties.contentContainer);
            chrome.Refresh();
            try
            {
                switch (owner.State.Page)
                {
                    case "source": TerrainWorkbenchSourcePage.Build(fields); break;
                    case "space": TerrainWorkbenchSpacePage.Build(fields); break;
                    case "minerals": TerrainWorkbenchMineralPage.Build(fields); break;
                    case "extras": TerrainWorkbenchExtrasPage.Build(fields); break;
                    case "appearance": appearance = new TerrainWorkbenchAppearancePage(fields); break;
                    case "release": release = new TerrainWorkbenchReleasePage(fields); break;
                    default: advanced = new TerrainWorkbenchAdvancedPage(fields); break;
                }
            }
            catch (Exception error) { fields.Help("任务属性待修正：" + error.Message, HelpBoxMessageType.Error); }
        }

        internal void Refresh()
        {
            if (root == null) return;
            try
            {
                var model = owner.Generation; var preview = owner.Preview; var state = owner.State;
                bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
                root.Q("editSurface").SetEnabled(!playing);
                root.Q<Button>("saveDraft").SetEnabled(!playing && model.HasChanges);
                root.Q<Button>("updateCandidate").SetEnabled(!playing && !preview.Map.HasChanges);
                root.Q("workbenchMore").SetEnabled(!playing);
                root.Q<Label>("draftStatus").text = model.HasChanges ? "草稿未保存" : "草稿已保存";
                root.Q<Label>("draftStatus").EnableInClassList("map-attention", model.HasChanges);
                string source = model.RuleMode ? "新规则生成" : owner.Draft.Config.MineMap != null ? owner.Draft.Config.MineMap.name : "旧随机来源";
                root.Q<Label>("documentSource").text = (model.Selected?.DisplayName ?? "未选星球") + " / 来源：" + source;
                root.Q<Label>("candidateStatus").text = model.CandidateFresh ? "候选与草稿一致" : "草稿候选缺失／过期";
                root.Q<Label>("candidateStatus").EnableInClassList("map-attention", !model.CandidateFresh);
                root.Q<Label>("canvasIdentity").text = "当前画布：" + preview.Caption;
                root.Q<Label>("canvasIdentity").tooltip = preview.Caption;
                root.Q<Label>("canvasLegend").text = preview.OverlayLegend;
                root.Q<Label>("canvasStatus").text = preview.CanvasStatus;
                var map = preview.Displayed;
                root.Q<Label>("mapSummary").text = map == null ? "等待载入地图" : "320 × 192 格 · " + map.Rooms.Count +
                    " 矿室 · " + map.Deposits.Count + " 矿床分片 · 储量 " + map.Deposits.Sum(d => d.Capacity) +
                    " · " + map.Nests.Count + " 巢 · " + map.GeologySites.Count + " 塌方点";
                var applied = model.InstalledConfig.ReviewedMap;
                string currentOfficial = model.InstalledConfig.CanonicalIdentity() + "|" + applied?.SourceIdentity;
                if (officialIdentity != currentOfficial)
                {
                    officialIdentity = currentOfficial; referenceError = "";
                    if (applied != null)
                        try { applied.VerifySource(model.InstalledConfig); }
                        catch (Exception) { referenceError = "保存输入与当前冻结版不匹配，新局会拒绝；请在检查与使用中创建并装配匹配版本。"; }
                }
                string version = applied == null ? "未装配冻结版" : Path.GetFileName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(applied)));
                root.Q<Label>("newGameReference").text = "新局引用 · " + version;
                root.Q<Label>("newGameReference").tooltip = referenceError.Length == 0
                    ? "仅查看和保存草稿不会更换正式引用；请到「检查与使用」核对并装配。" : referenceError;
                root.Q<Label>("referenceWarning").text = referenceError;
                root.Q<Label>("referenceWarning").EnableInClassList("map-hidden", referenceError.Length == 0);
                chrome.Refresh();
                root.Q<Label>("workbenchNotice").text = playing ? "Play期间暂停作者编辑，网络／运行会话不受本窗口控制。" :
                    state.Pick != 0 ? "接下来点击：" + new[] { "", "源岩左下格", "落地基准格", "支撑格",
                        "进入区左下格", "退避区左下格", "允许改形左下格" }[state.Pick] + "；Esc取消。" : owner.Notice;
                properties.Query<VisualElement>(className: "map-author-edit").ForEach(field => field.SetEnabled(!preview.Map.HasChanges));
                release?.Refresh();
            }
            catch (Exception error) { root.Q<Label>("workbenchNotice").text = "输入待修正：" + error.Message; }
        }

        private void OnGeometry(GeometryChangedEvent change)
        { root.EnableInClassList("map-narrow", root.resolvedStyle.width < 1000); }

        private void OnKeyDown(KeyDownEvent input)
        {
            if (input.keyCode != KeyCode.S || !(input.ctrlKey || input.commandKey) || input.altKey || input.shiftKey) return;
            owner.Execute(() =>
            {
                if (owner.State.Page == "appearance") owner.Commands.SaveStyles();
                else if (owner.State.Page == "advanced" && owner.State.AdvancedPage == "study")
                    TerrainVeinStudySettings.instance.SavePreset();
                else owner.Commands.SaveDraft();
            });
            input.StopImmediatePropagation();
        }

        private void RepaintCanvas() => canvas?.MarkDirtyRepaint();
        public void Dispose()
        {
            if (owner.Preview != null) owner.Preview.RepaintRequested -= RepaintCanvas;
            host.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root?.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            appearance?.Dispose(); advanced?.Dispose(); fields?.Dispose();
        }
    }
}
