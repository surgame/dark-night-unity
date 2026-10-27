using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图画布的原生模式互斥组与独立布尔开关；窗口状态是唯一来源，通知静默同步避免回写和重复执行。
    /// 开关由checked表达，hover和focus只作交互提示；图标沿用主题，事件随界面树释放，不修改制作配置。
    /// </summary>
    internal sealed class MapWorkbenchDisplayTools
    {
        private readonly MapWorkbenchWindow owner;
        private readonly VisualElement root;
        private readonly Dictionary<Toggle, Func<bool>> toggles = new Dictionary<Toggle, Func<bool>>();
        private readonly ToggleButtonGroup modes;

        internal MapWorkbenchDisplayTools(MapWorkbenchWindow owner, VisualElement root)
        {
            this.owner = owner; this.root = root;
            modes = root.Q<ToggleButtonGroup>("mapModeSwitch");
            modes.RegisterValueChangedCallback(change =>
                owner.Execute(() => owner.Preview.SetRenderMode(change.newValue[1])));
            var fit = root.Q<Button>("displayFit");
            AddIcon(fit, fit, "displayFit");
            fit.clicked += () => owner.Execute(() => { owner.Preview.Fit(); owner.Repaint(); });
            Bind("displayGrid", () => owner.Preview.Canvas.ShowGrid, x => owner.Preview.Canvas.ShowGrid = x);
            Bind("displayRegions", () => owner.State.Regions, x => owner.State.Regions = x);
            Bind("displayConnections", () => owner.State.Connections, x => owner.State.Connections = x);
            Bind("displayMinerals", () => owner.State.Minerals, x => owner.State.Minerals = x);
            Bind("displaySites", () => owner.State.Sites, x => owner.State.Sites = x);
            root.Q("displayLanding")?.AddToClassList("map-hidden");
            Bind("displayScenery", () => owner.State.DesignScenery, x => owner.State.DesignScenery = x);
            Refresh();
        }

        private static void AddIcon(VisualElement parent, VisualElement control, string name)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("map-display-icon");
            icon.generateVisualContent += context => Draw(context.painter2D, name, control.resolvedStyle.color);
            parent.Add(icon);
        }

        private void Bind(string name, Func<bool> read, Action<bool> write)
        {
            var toggle = root.Q<Toggle>(name);
            AddIcon(toggle.Q(className: Toggle.inputUssClassName), toggle, name);
            toggle.userData = toggle.tooltip;
            toggles.Add(toggle, read);
            toggle.RegisterValueChangedCallback(change => owner.Execute(() =>
            {
                write(change.newValue); owner.State.Grid = owner.Preview.Canvas.ShowGrid;
                toggle.Q(className: "map-display-icon").MarkDirtyRepaint();
                Refresh(); owner.Refresh(); owner.Repaint();
            }));
        }

        internal void Refresh()
        {
            modes.SetValueWithoutNotify(new ToggleButtonGroupState(owner.Preview.DesignMode ? 1UL : 2UL, 2));
            root.Q<Toggle>("displayScenery").SetEnabled(owner.Preview.DesignMode);
            foreach (var entry in toggles)
            {
                var toggle = entry.Key;
                bool enabled = entry.Value();
                if (toggle.value != enabled)
                {
                    toggle.SetValueWithoutNotify(enabled);
                    toggle.Q(className: "map-display-icon").MarkDirtyRepaint();
                }
                toggle.tooltip = toggle.userData + (!toggle.enabledInHierarchy ? "：当前模式不可用。" :
                    enabled ? "：已开启，点击关闭。" : "：已关闭，点击开启。") +
                    (toggle.name == "displayGrid" ? "\n1：逻辑格；2：渲染格。" : "");
            }
        }

        private static void Draw(Painter2D painter, string name, Color color)
        {
            painter.strokeColor = color; painter.lineWidth = 1.5f;
            painter.lineCap = LineCap.Round; painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            switch (name)
            {
                case "displayFit":
                    Line(painter, 3, 7, 3, 3, 7, 3); Line(painter, 13, 3, 17, 3, 17, 7);
                    Line(painter, 17, 13, 17, 17, 13, 17); Line(painter, 7, 17, 3, 17, 3, 13);
                    break;
                case "displayGrid":
                    Box(painter, 3, 3, 17, 17);
                    Line(painter, 8, 3, 8, 17); Line(painter, 13, 3, 13, 17);
                    Line(painter, 3, 8, 17, 8); Line(painter, 3, 13, 17, 13);
                    break;
                case "displayRegions":
                    Line(painter, 3, 4, 9, 2, 17, 5, 16, 15, 10, 18, 3, 15, 3, 4);
                    Line(painter, 9, 2, 9, 10, 3, 15); Line(painter, 9, 10, 16, 15);
                    break;
                case "displayConnections":
                    Line(painter, 5, 5, 15, 7, 10, 16, 5, 5);
                    Box(painter, 3, 3, 7, 7); Box(painter, 13, 5, 17, 9);
                    Box(painter, 8, 14, 12, 18);
                    break;
                case "displayMinerals":
                    Line(painter, 3, 7, 7, 3, 13, 3, 17, 7, 10, 18, 3, 7);
                    Line(painter, 3, 7, 17, 7); Line(painter, 7, 3, 6, 7, 10, 18, 14, 7, 13, 3);
                    break;
                case "displaySites":
                    painter.MoveTo(new Vector2(10, 18)); painter.LineTo(new Vector2(5, 11));
                    painter.BezierCurveTo(new Vector2(0, 0), new Vector2(20, 0), new Vector2(15, 11));
                    painter.ClosePath(); Box(painter, 8, 6, 12, 10);
                    break;
                case "displayLanding":
                    Line(painter, 3, 5, 3, 17, 17, 17, 17, 5);
                    Line(painter, 10, 2, 10, 12); Line(painter, 6, 8, 10, 12, 14, 8);
                    break;
                case "displayScenery":
                    Line(painter, 5, 18, 5, 2, 16, 4, 5, 9);
                    Line(painter, 2, 18, 8, 18);
                    break;
            }
            painter.Stroke();
        }

        private static void Line(Painter2D painter, params float[] points)
        {
            painter.MoveTo(new Vector2(points[0], points[1]));
            for (int i = 2; i < points.Length; i += 2)
                painter.LineTo(new Vector2(points[i], points[i + 1]));
        }

        private static void Box(Painter2D painter, float left, float top, float right, float bottom)
        {
            Line(painter, left, top, right, top, right, bottom, left, bottom);
            painter.ClosePath();
        }
    }
}
