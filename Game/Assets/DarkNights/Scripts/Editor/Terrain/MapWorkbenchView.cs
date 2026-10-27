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
    internal sealed class MapWorkbenchView : IDisposable
    {
        private const string Layout = "Assets/DarkNights/Res/Editor/Terrain/MapWorkbench.uxml";
        private readonly MapWorkbenchWindow owner;
        private readonly VisualElement host;
        private MapWorkbenchChrome chrome;
        private MapWorkbenchLibrary library;
        private readonly MapWorkbenchTypography typography = new MapWorkbenchTypography();
        private VisualElement root;
        private ScrollView properties;
        private IMGUIContainer canvas;
        private MapWorkbenchFields fields;
        private MapWorkbenchAppearancePage appearance;
        private MapWorkbenchAdvancedPage advanced;
        private MapWorkbenchEnvironmentPage environment;
        private MapWorkbenchFlowPage flow;
        private MapWorkbenchReleasePage release;

        internal MapWorkbenchView(MapWorkbenchWindow owner, VisualElement host)
        { this.owner = owner; this.host = host; }

        internal void Build()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Layout);
            if (tree == null) { host.Add(new HelpBox("地图工作台布局尚未导入：" + Layout, HelpBoxMessageType.Error)); return; }
            tree.CloneTree(host); root = host.Q("mapWorkbench");
            root.EnableInClassList("map-light", !EditorGUIUtility.isProSkin);
            properties = root.Q<ScrollView>("taskProperties");
            chrome = new MapWorkbenchChrome(owner, root);
            library = new MapWorkbenchLibrary(owner, root);
            root.Q("loadProgressHost").Add(new ProgressBar
            { name = "mapLoadBar", lowValue = 0, highValue = 100, pickingMode = PickingMode.Ignore });
            root.Q<Button>("cancelMapLoad").clicked += () => owner.Execute(owner.FileLoad.Cancel);
            typography.Apply(root);
            canvas = new IMGUIContainer(() => owner.Preview.Draw(new Rect(Vector2.zero, canvas.contentRect.size)))
            { name = "terrainCanvas", focusable = true };
            root.Q("canvasHost").Insert(0, canvas);
            flow = new MapWorkbenchFlowPage(owner, root);
            owner.Preview.RepaintRequested += RepaintCanvas;
            host.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            root.EnableInClassList("map-narrow", owner.position.width < 1400);
            BuildPage(); Refresh();
        }

        internal void BuildPage()
        {
            if (properties == null) return;
            appearance?.Dispose(); appearance = null; advanced?.Dispose(); advanced = null;
            environment?.Dispose(); environment = null;
            release = null;
            fields?.Dispose(); properties.Clear();
            fields = new MapWorkbenchFields(owner, properties.contentContainer);
            chrome.Refresh();
            try
            {
                if (owner.State.ViewedMap != null)
                {
                    fields.Title("地图结果 · 只读", "此文件只有生成后的地图，不能继续调参；请从左侧打开对应地图配置进行编辑。");
                    fields.Note(AssetDatabase.GetAssetPath(owner.State.ViewedMap));
                    fields.Note("画面沿用当前编辑配置的表现设置；此文件只提供地形、矿层与站点数据。");
                    fields.Note("原编辑配置及未保存输入仍保留；返回后可继续编辑和保存。");
                    fields.Button("返回编辑配置", () => MapWorkbenchFiles.ReturnToDraft(owner), primary: true);
                    fields.Button("在工程中定位文件", () => EditorGUIUtility.PingObject(owner.State.ViewedMap));
                    return;
                }
                switch (owner.State.Page)
                {
                    case "recipe": MapWorkbenchSourcePage.Build(fields); break;
                    case "surface": case "rooms": case "roads": case "entrance": MapWorkbenchSpacePage.Build(fields); break;
                    case "minerals": MapWorkbenchMineralPage.Build(fields); break;
                    case "environment": environment = new MapWorkbenchEnvironmentPage(fields); break;
                    case "extras": MapWorkbenchExtrasPage.Build(fields); break;
                    case "appearance": appearance = new MapWorkbenchAppearancePage(fields); break;
                    case "release": release = new MapWorkbenchReleasePage(fields); break;
                    default: advanced = new MapWorkbenchAdvancedPage(fields); break;
                }
            }
            catch (Exception error) { fields.Help("任务属性待修正：" + error.Message, HelpBoxMessageType.Error); }
        }

        internal void Refresh()
        {
            if (root == null) return;
            typography.Retain();
            try
            {
                var model = owner.Generation; var preview = owner.Preview; var state = owner.State;
                bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
                bool viewing = state.ViewedMap != null;
                bool reading = owner.FileLoad.Busy;
                root.Q("editSurface").SetEnabled(!playing);
                root.Q<Button>("saveDraft").SetEnabled(!playing && !viewing && preview.SaveReady);
                root.Q<Button>("updateCandidate").SetEnabled(!playing && !viewing && !reading && !preview.Map.HasChanges);
                root.Q<Button>("undo").SetEnabled(!viewing && !reading);
                root.Q<Button>("redo").SetEnabled(!viewing && !reading);
                root.Q("taskNavigation").SetEnabled(!playing && !reading);
                properties.SetEnabled(!reading);
                var progress = owner.Progress;
                root.Q("mapLoadProgress").EnableInClassList("map-hidden", !progress.Active);
                root.Q<ProgressBar>("mapLoadBar").value = progress.Value;
                root.Q<ProgressBar>("mapLoadBar").title = progress.Title;
                root.Q<ProgressBar>("mapLoadBar").tooltip = progress.Target + "；比例仅表示当前阶段的可见页完成量，平移或新输入会重新计算。";
                root.Q<Label>("loadProgressDetail").text = progress.Detail;
                root.Q<Button>("cancelMapLoad").EnableInClassList("map-hidden", !reading);
                root.Q<Label>("mapLoadTiming").text = progress.LastReport;
                root.Q<Label>("draftStatus").text = viewing ? (owner.hasUnsavedChanges ? "只读 · 编辑草稿未保存" : "只读查看") :
                    owner.hasUnsavedChanges || state.Document == null ? "未保存" : "已保存";
                root.Q<Label>("draftStatus").EnableInClassList("map-attention",
                    owner.hasUnsavedChanges || !viewing && state.Document == null);
                root.Q<Label>("candidateStatus").text = preview.UpdateStatus;
                root.Q<Label>("candidateStatus").EnableInClassList("map-attention", !model.CandidateFresh || preview.Pending);
                root.Q<Label>("canvasIdentity").text = preview.ResultOrigin + " · " + preview.Caption;
                root.Q<Label>("canvasIdentity").tooltip = preview.Caption +
                    "；打开文件沿用已保存地形。规则更新不会重写旧图；文件菜单可用当前种子重新生成，保存后才能用于新局。";
                root.Q<Label>("canvasLegend").text = preview.OverlayLegend;
                root.Q<Label>("canvasStatus").text = preview.CanvasStatus;
                root.Q<Label>("designSelection").text = preview.Design.SelectionDetail;
                root.Q<Label>("designSelection").tooltip = preview.Design.SelectionDetail;
                root.Q("designSelection").EnableInClassList("map-hidden", !preview.DesignMode);
                var map = preview.Displayed;
                var regionSummary = root.Q<Label>("regionSummary");
                if (regionSummary != null && map != null)
                {
                    string prefix = "rule-r" + state.SelectedRegion + "-";
                    var ore = map.Deposits.Where(d => d.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                    regionSummary.text = "当前结果：" + ore.Select(d => d.Id.Substring(0, d.Id.LastIndexOf('-'))).Distinct().Count() +
                        " 矿团 · " + ore.Sum(d => d.Capacity) + " 储量";
                    root.Q<Button>("rerollRegion")?.SetEnabled(ore.Length > 0 || TerrainRegionCommands.Current(owner)?.VeinCount > 0);
                }
                root.Q<Label>("mapSummary").text = map == null ? "等待载入地图" : "320 × 192 格 · " + map.Rooms.Count +
                    " 矿室 · " + map.Deposits.Count + " 矿床分片 · 储量 " + map.Deposits.Sum(d => d.Capacity) +
                    " · " + map.Nests.Count + " 巢 · " + map.GeologySites.Count + " 塌方点";
                chrome.Refresh();
                flow.Refresh();
                release?.Refresh();
                library.Refresh();
                root.Q<Label>("workbenchNotice").text = playing ? "Play期间暂停作者编辑，网络／运行会话不受本窗口控制。" :
                    state.Pick != 0 ? "接下来点击：" + new[] { "", "源岩左下格", "落地基准格", "支撑格",
                        "进入区左下格", "退避区左下格", "允许改形左下格" }[state.Pick] + "；Esc取消。" : owner.Notice;
                properties.Query<VisualElement>(className: "map-author-edit").ForEach(field =>
                    field.SetEnabled(!preview.Map.HasChanges && !field.ClassListContains("map-readonly")));
            }
            catch (Exception error) { root.Q<Label>("workbenchNotice").text = "输入待修正：" + error.Message; }
        }

        private void OnGeometry(GeometryChangedEvent change)
        { root.EnableInClassList("map-narrow", root.resolvedStyle.width < 1400); }

        private void OnKeyDown(KeyDownEvent input)
        {
            if (input.keyCode != KeyCode.S || !(input.ctrlKey || input.commandKey) || input.altKey) return;
            owner.Execute(() =>
            {
                if (owner.State.Page == "advanced" && owner.State.AdvancedPage == "study")
                    TerrainVeinStudySettings.instance.SavePreset();
                else if (input.shiftKey) TerrainDocumentStore.SaveAs(owner);
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
            appearance?.Dispose(); advanced?.Dispose(); environment?.Dispose(); flow?.Dispose(); fields?.Dispose(); library?.Dispose();
            typography.Dispose();
        }
    }
}
