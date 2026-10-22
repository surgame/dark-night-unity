using System;
using System.Collections.Generic;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 工作台的任务导航、显示工具与全局操作入口；仅持有控件，不另建作者或生成状态。
    /// 选中态使用完整背景而非异宽边框，显示选项仍由窗口状态和原画布唯一拥有。
    /// </summary>
    internal sealed class TerrainWorkbenchChrome
    {
        private static readonly string[] Pages = { "source", "space", "minerals", "extras", "appearance", "release", "advanced" };
        private static readonly string[] Titles = { "地图来源", "空间生成", "矿物分布", "额外设计", "画面表现", "检查与使用", "高级与兼容" };
        private static readonly string[] Hints = { "新规则 / 作者图 / 版本", "入口 · 道路 · 矿室", "矿量 · 露矿 · 内容种子", "巢穴 · 人工塌方点", "岩壁 · 背景 · 装饰", "核对 · 冻结 · 正式新局", "旧能力与本机实验" };
        private readonly TerrainStylePreviewWindow owner;
        private readonly VisualElement root;
        private readonly Dictionary<string, Button> navigation = new Dictionary<string, Button>();
        private readonly List<Action> syncLayers = new List<Action>();
        private readonly Button previous, next;

        internal TerrainWorkbenchChrome(TerrainStylePreviewWindow owner, VisualElement root)
        {
            this.owner = owner; this.root = root;
            BuildNavigation(); BuildLayers();
            root.Q<Button>("saveDraft").clicked += () => owner.Execute(owner.Commands.SaveDraft);
            root.Q<Button>("updateCandidate").clicked += () => owner.Execute(owner.Commands.Generate);
            root.Q<Button>("viewCandidate").clicked += () => owner.Execute(owner.Commands.ViewCandidate);
            root.Q<Button>("viewInstalled").clicked += () => owner.Execute(owner.Commands.ViewInstalled);
            root.Q<Button>("fitCanvas").clicked += owner.Preview.Fit;
            previous = root.Q<Button>("previousTask"); next = root.Q<Button>("nextTask");
            previous.clicked += () => Move(-1); next.clicked += () => Move(1);
            var more = new ToolbarMenu { text = "更多", name = "workbenchMore" };
            more.AddToClassList("map-more");
            more.menu.AppendAction("取消窗口未保存修改…", _ =>
            {
                if (EditorUtility.DisplayDialog("取消窗口草稿", "取消地图、表现及本机实验未保存修改？原资产与已创建版本保留。",
                    "取消窗口修改", "返回")) owner.Execute(owner.DiscardChanges);
            });
            root.Q("headerActions").Add(more);
        }

        private void BuildNavigation()
        {
            var nav = root.Q<ScrollView>("taskNavigation");
            var heading = new Label("制作流程"); heading.AddToClassList("map-nav-heading"); nav.Add(heading);
            for (int i = 0; i < Pages.Length; i++)
            {
                string page = Pages[i];
                var button = new Button(() => owner.SelectPage(page))
                { name = "task-" + page, tooltip = Titles[i] + " · " + Hints[i] };
                button.AddToClassList("map-task");
                if (i == 6) button.AddToClassList("map-task-advanced");
                if (i < 6)
                {
                    var number = new Label((i + 1).ToString("00"));
                    number.AddToClassList("map-task-number"); button.Add(number);
                }
                var copy = new VisualElement(); copy.AddToClassList("map-task-copy"); button.Add(copy);
                var title = new Label(Titles[i]); title.AddToClassList("map-task-title"); copy.Add(title);
                var hint = new Label(Hints[i]); hint.AddToClassList("map-task-hint"); copy.Add(hint);
                nav.Add(button); navigation.Add(page, button);
            }
        }

        private void BuildLayers()
        {
            Layer("格线", () => owner.Preview.Canvas.ShowGrid, value => owner.Preview.Canvas.ShowGrid = value);
            Layer("逻辑格", () => owner.Preview.Canvas.GridMode == TerrainGridMode.Logical,
                value => owner.Preview.Canvas.SelectGrid(value ? TerrainGridMode.Logical : TerrainGridMode.Render),
                "切换格线尺寸：1 逻辑格 / 2 渲染格。");
            Layer("区域", () => owner.State.Regions, value => owner.State.Regions = value);
            Layer("连接", () => owner.State.Connections, value => owner.State.Connections = value);
            Layer("墙后矿", () => owner.State.Minerals, value => owner.State.Minerals = value);
            Layer("站点", () => owner.State.Sites, value => owner.State.Sites = value,
                "实际巢穴与塌方点可点击选择；编辑草图只在额外设计／塌方页显示。");
            Layer("泊位", () => owner.State.LandingMarkers, value => owner.State.LandingMarkers = value);
        }

        private void Layer(string title, Func<bool> read, Action<bool> write, string tooltip = null)
        {
            var toggle = new Toggle { text = title, tooltip = tooltip ?? "显示／隐藏" + title + "叠层" };
            toggle.AddToClassList("map-layer-chip");
            toggle.RegisterValueChangedCallback(change =>
            {
                write(change.newValue); toggle.EnableInClassList("map-selected", read());
            });
            syncLayers.Add(() =>
            {
                toggle.SetValueWithoutNotify(read()); toggle.EnableInClassList("map-selected", read());
            });
            root.Q("canvasTools").Add(toggle);
        }

        private void Move(int direction)
        {
            int index = Array.IndexOf(Pages, owner.State.Page);
            int target = index + direction;
            if (target >= 0 && target < 6) owner.SelectPage(Pages[target]);
        }

        internal void Refresh()
        {
            int index = Array.IndexOf(Pages, owner.State.Page);
            foreach (var pair in navigation) pair.Value.EnableInClassList("map-selected", owner.State.Page == pair.Key);
            previous.SetEnabled(index > 0 && index < 6);
            next.SetEnabled(index >= 0 && index < 5);
            previous.text = index > 0 && index < 6 ? "上一步 · " + Titles[index - 1] : "上一步";
            next.text = index >= 0 && index < 5 ? "下一步 · " + Titles[index + 1] : "已到最后一步";
            if (index == 6) next.text = "独立实验区";
            foreach (var sync in syncLayers) sync();
            root.Q<Button>("viewCandidate").EnableInClassList("map-selected", !owner.State.ViewInstalled && owner.State.Version == null);
            root.Q<Button>("viewInstalled").EnableInClassList("map-selected", owner.State.ViewInstalled);
            owner.State.Grid = owner.Preview.Canvas.ShowGrid;
            owner.State.LogicalGrid = owner.Preview.Canvas.GridMode == TerrainGridMode.Logical;
        }
    }
}
