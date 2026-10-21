using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DarkNights.View.Terrain;
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
        private readonly Dictionary<string, Button> navigation = new Dictionary<string, Button>();
        private VisualElement root;
        private ScrollView properties;
        private IMGUIContainer canvas;
        private TerrainWorkbenchFields fields;
        private TerrainWorkbenchAppearancePage appearance;
        private TerrainWorkbenchAdvancedPage advanced;
        private TerrainWorkbenchReleasePage release;
        private Toggle grid, logical, sites;
        private Button candidate, installed;
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
            BuildNavigation();
            root.Q<Button>("saveDraft").clicked += () => owner.Execute(owner.Commands.SaveDraft);
            root.Q<Button>("openRelease").clicked += () => owner.SelectPage("release");
            root.Q<Button>("updateCandidate").clicked += () => owner.Execute(owner.Commands.Generate);
            root.Q<Button>("discardWindow").clicked += () =>
            {
                if (EditorUtility.DisplayDialog("取消窗口草稿", "取消地图、表现及本机实验未保存修改？原资产与已创建版本保留。",
                    "取消窗口修改", "返回")) owner.Execute(owner.DiscardChanges);
            };
            candidate = root.Q<Button>("viewCandidate");
            installed = root.Q<Button>("viewInstalled");
            candidate.clicked += () => owner.Execute(owner.Commands.ViewCandidate);
            installed.clicked += () => owner.Execute(owner.Commands.ViewInstalled);
            BuildCanvasTools();
            canvas = new IMGUIContainer(() => owner.Preview.Draw(new Rect(Vector2.zero, canvas.contentRect.size)))
            { name = "terrainCanvas", focusable = true };
            root.Q("canvasHost").Add(canvas);
            owner.Preview.RepaintRequested += RepaintCanvas;
            host.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            root.EnableInClassList("map-narrow", owner.position.width < 1000);
            BuildPage(); Refresh();
        }

        private void BuildNavigation()
        {
            var nav = root.Q<ScrollView>("taskNavigation");
            string[] ids = { "source", "space", "minerals", "extras", "appearance", "release", "advanced" };
            string[] titles = { "地图来源", "空间生成", "矿物分布", "额外设计", "画面表现", "检查与使用", "高级与兼容" };
            string[] hints = { "新规则 / 作者图 / 版本", "入口 · 道路 · 矿室", "矿量 · 露矿 · 内容种子", "巢穴 · 人工塌方点", "岩壁 · 背景 · 装饰", "核对 · 冻结 · 正式新局", "旧能力与本机实验" };
            var heading = new Label("制作流程"); heading.AddToClassList("map-nav-heading"); nav.Add(heading);
            for (int i = 0; i < ids.Length; i++)
            {
                string page = ids[i];
                var button = new Button(() => owner.SelectPage(page)) { name = "task-" + page, tooltip = titles[i] + " · " + hints[i] };
                button.AddToClassList("map-task");
                if (page == "advanced") button.AddToClassList("map-task-advanced");
                var number = new Label(i < 6 ? (i + 1).ToString("00") : "+");
                number.AddToClassList("map-task-number"); button.Add(number);
                var copy = new VisualElement(); copy.AddToClassList("map-task-copy"); button.Add(copy);
                var title = new Label(titles[i]); title.AddToClassList("map-task-title"); copy.Add(title);
                var hint = new Label(hints[i]); hint.AddToClassList("map-task-hint"); copy.Add(hint);
                nav.Add(button); navigation.Add(page, button);
            }
        }

        private void BuildCanvasTools()
        {
            var tools = root.Q("canvasTools");
            grid = Toggle(tools, "格线", owner.Preview.Canvas.ShowGrid, value => owner.Preview.Canvas.ShowGrid = value);
            logical = Toggle(tools, "逻辑格", owner.State.LogicalGrid, value =>
            {
                owner.Preview.Canvas.SelectGrid(value ? TerrainGridMode.Logical : TerrainGridMode.Render);
            });
            Toggle(tools, "区域", owner.State.Regions, value => owner.State.Regions = value);
            Toggle(tools, "连接", owner.State.Connections, value => owner.State.Connections = value);
            Toggle(tools, "墙后矿", owner.State.Minerals, value => owner.State.Minerals = value);
            sites = Toggle(tools, "站点", owner.State.Sites, value => owner.State.Sites = value);
            sites.tooltip = "显示当前画布实际生成的巢穴与塌方点；塌方编辑草图仅在额外设计／塌方页显示。";
            Toggle(tools, "泊位", owner.State.LandingMarkers, value => owner.State.LandingMarkers = value);
            tools.Add(new Button(owner.Preview.Fit) { text = "适配" });
        }

        private static Toggle Toggle(VisualElement parent, string text, bool value, Action<bool> changed)
        {
            var toggle = new Toggle(text); toggle.SetValueWithoutNotify(value);
            toggle.RegisterValueChangedCallback(change => changed(change.newValue)); parent.Add(toggle); return toggle;
        }

        internal void BuildPage()
        {
            if (properties == null) return;
            appearance?.Dispose(); appearance = null; advanced?.Dispose(); advanced = null; release = null;
            fields?.Dispose(); properties.Clear();
            fields = new TerrainWorkbenchFields(owner, properties.contentContainer);
            foreach (var pair in navigation) pair.Value.EnableInClassList("map-selected", owner.State.Page == pair.Key);
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
                root.Q<Label>("draftStatus").text = model.HasChanges ? "● 作者草稿未保存" : "作者草稿已保存";
                string source = model.RuleMode ? "新规则生成" : owner.Draft.Config.MineMap != null ? owner.Draft.Config.MineMap.name : "旧随机来源";
                root.Q<Label>("documentSource").text = (model.Selected?.DisplayName ?? "未选星球") + " / 来源：" + source;
                root.Q<Label>("candidateStatus").text = model.CandidateFresh ? "候选与草稿一致" : "草稿候选缺失／过期";
                root.Q<Label>("canvasIdentity").text = "当前画布：" + preview.Caption;
                root.Q<Label>("canvasLegend").text = preview.OverlayLegend;
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
                root.Q<Label>("newGameReference").text = "正式新局引用：" + version +
                    (referenceError.Length == 0 ? "；查看和保存不会自动更换。" : "\n" + referenceError);
                candidate.EnableInClassList("map-selected", !state.ViewInstalled && state.Version == null);
                installed.EnableInClassList("map-selected", state.ViewInstalled);
                grid.SetValueWithoutNotify(preview.Canvas.ShowGrid);
                logical.SetValueWithoutNotify(preview.Canvas.GridMode == TerrainGridMode.Logical);
                sites.SetValueWithoutNotify(state.Sites);
                state.Grid = preview.Canvas.ShowGrid; state.LogicalGrid = preview.Canvas.GridMode == TerrainGridMode.Logical;
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
