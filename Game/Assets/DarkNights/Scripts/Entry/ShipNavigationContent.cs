using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;
using DarkNights.View.Expedition;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 既有飞船UIPanel内的Win98导航绑定；只向本地目录列出已勘测星体。
    /// 星体／坐标选择和对话框属于个人浏览，确认才发送带明确目标的意图，权限仍由Host复核。
    /// </summary>
    internal sealed class ShipNavigationContent : IDisposable
    {
        private readonly VisualElement surface, card;
        private readonly Label detail, permission, nodeName, position, eta, current, zoom, count;
        private readonly Button close, confirm, depart, cancel, locate, fit, zoomIn, zoomOut, plot, menuPlot, menuView, menuHelp;
        private readonly DropdownField directory;
        private readonly StarMapElement map;
        private readonly ShipNavigationDialog dialog;
        private List<NavigationNodeDefinition> known = new List<NavigationNodeDefinition>();
        private JourneyViewData journey;
        private ExpeditionViewData expedition;
        private string selected = "";
        private bool canChange, coordinate;
        private float targetX, targetY;
        public bool IsOpen { get; private set; }
        public event Action CloseRequested;
        public event Action<string> DestinationRequested;
        public event Action<float, float> CoordinateRequested;
        public event Action<string> OperationRequested;

        public ShipNavigationContent(VisualElement root)
        {
            surface = root.Q("navigation"); card = root.Q(className: "navigation-card");
            current = root.Q<Label>("navigation-current");
            detail = root.Q<Label>("navigation-detail"); permission = root.Q<Label>("navigation-permission");
            nodeName = root.Q<Label>("navigation-node-name"); position = root.Q<Label>("navigation-position");
            eta = root.Q<Label>("navigation-eta"); zoom = root.Q<Label>("map-zoom"); count = root.Q<Label>("navigation-known-count");
            close = root.Q<Button>("close-navigation");
            confirm = root.Q<Button>("confirm-destination"); depart = root.Q<Button>("depart-expedition");
            cancel = root.Q<Button>("cancel-departure"); locate = root.Q<Button>("map-locate");
            fit = root.Q<Button>("map-fit"); zoomIn = root.Q<Button>("map-zoom-in"); zoomOut = root.Q<Button>("map-zoom-out");
            plot = root.Q<Button>("map-plot"); menuPlot = root.Q<Button>("navigation-menu-plot");
            menuView = root.Q<Button>("navigation-menu-view"); menuHelp = root.Q<Button>("navigation-menu-help");
            directory = root.Q<DropdownField>("navigation-list");
            map = new StarMapElement(); root.Q("star-map-host").Add(map);
            dialog = new ShipNavigationDialog(root);
            dialog.Accepted += SelectCoordinate;
            map.Selected += Select; map.CoordinateSelected += SelectCoordinate; map.ViewChanged += UpdateZoom;
            close.clicked += RequestClose; confirm.clicked += Confirm;
            depart.clicked += Depart; cancel.clicked += Cancel;
            locate.clicked += map.FocusShip; fit.clicked += map.FitAll; menuView.clicked += map.FitAll;
            zoomIn.clicked += ZoomIn; zoomOut.clicked += ZoomOut;
            plot.clicked += Plot; menuPlot.clicked += Plot; menuHelp.clicked += Help;
            directory.RegisterValueChangedCallback(ChooseDirectory);
            card.RegisterCallback<GeometryChangedEvent>(Resize);
        }

        public void Present(JourneyViewData data, bool permitted, ExpeditionViewData crew = null)
        {
            journey = data; expedition = crew; canChange = permitted;
            if (data?.Navigation == null)
            {
                current.text = "正在读取导航资料...";
                nodeName.text = "无可用资料"; detail.text = position.text = "";
                eta.text = "预计航程: --"; known.Clear(); directory.choices = new List<string>();
                confirm.SetEnabled(false); depart.SetEnabled(false); cancel.SetEnabled(false);
                map.Present(null, ""); return;
            }
            var n = data.Navigation;
            known = n.Definition.Nodes.Where(v => n.Survey.IsRevealed(v.X, v.Y)).ToList();
            var choices = known.Select(v => v.Name + " [已登记]").ToList();
            if (!directory.choices.SequenceEqual(choices)) directory.choices = choices;
            count.text = "已登记: " + known.Count + " 处";
            if (!coordinate && known.All(v => v.Id != selected)) selected = known.FirstOrDefault()?.Id ?? "";
            var chosen = coordinate ? null : known.FirstOrDefault(v => v.Id == selected);
            if (chosen != null) { targetX = chosen.X; targetY = chosen.Y; }
            directory.SetValueWithoutNotify(chosen == null ? "(未选定星体)" : chosen.Name + " [已登记]");
            nodeName.text = coordinate ? "指定坐标 [临时航点]" : chosen?.Name ?? "无可用资料";
            position.text = "X: " + Format(targetX) + "    Y: " + Format(targetY);
            detail.text = chosen != null ? "风险评定: " + StarMapElement.DangerLabel(chosen.Danger) + "。" +
                (chosen.PreviewOnly ? "仅供查阅，尚未开放降落。" : chosen.Risk.Replace("mock", "示意")) :
                n.Survey.IsRevealed(targetX, targetY) ? "该处已勘测。资料已记入星图。" : "该处尚未勘测。无可用资料。";
            double distance = Math.Sqrt(Math.Pow(targetX - n.X, 2) + Math.Pow(targetY - n.Y, 2));
            double seconds = distance / n.Definition.Speed + (coordinate ? 0 : n.Definition.DescentSeconds);
            eta.text = "预计航程: " + Math.Ceiling(seconds) + " 秒";
            bool phase = data.Phase is JourneyPhase.Orbit or JourneyPhase.Transit;
            bool enRoute = data.Phase == JourneyPhase.Transit && (coordinate ? n.CoordinateFlight &&
                targetX == n.TargetX && targetY == n.TargetY : !n.CoordinateFlight && selected == data.PlanetId);
            bool available = coordinate || chosen != null && !chosen.PreviewOnly;
            confirm.SetEnabled(permitted && phase && available && !enRoute && (!coordinate || distance > .01));
            confirm.text = !available ? "尚未开放" : enRoute ? "正在前往..." : data.Phase == JourneyPhase.Transit ? "更改航向(G)" : "开始航行(G)";
            permission.text = !available ? "此星体尚未开放。请指定其他目的地。" :
                !phase ? data.Phase == JourneyPhase.Landed ? "本船已停泊。请召集全员撤离后再行操作。" : "正在执行起降任务，请稍候。" :
                !permitted ? "请到导航台操作，并确认具有本房间的航行权限。" :
                "在迷雾中单击指定航点；按住鼠标拖动可移动星图。查看位置不会改变勘测记录。";
            if (data.Error.Length != 0) permission.text = data.Error;
            PresentStatus(data, crew);
            depart.SetEnabled(permitted && data.Phase == JourneyPhase.Landed);
            bool cancellable = data.Phase is JourneyPhase.Gathering or JourneyPhase.Preparing or JourneyPhase.Transit ||
                data.Phase == JourneyPhase.Ascent && data.Error.Length != 0;
            cancel.SetEnabled(permitted && cancellable);
            depart.EnableInClassList("is-unavailable", data.Phase != JourneyPhase.Landed);
            cancel.EnableInClassList("is-unavailable", !cancellable);
            cancel.text = data.Phase == JourneyPhase.Ascent ? "重试安全交接" : "取消航行／集合";
            map.Present(data, selected, coordinate, targetX, targetY);
        }

        private void PresentStatus(JourneyViewData data, ExpeditionViewData crew)
        {
            var n = data.Navigation; var active = n.Definition.Find(data.PlanetId);
            string message = JourneyPresentationRules.Stage(data.Phase) + "。";
            if (n.CoordinateFlight)
                message = "正在前往坐标 X " + Format(n.TargetX) + "，Y " + Format(n.TargetY) + "。正在记录星域资料...";
            else if (active != null)
                message += " " + active.Name + (data.Phase == JourneyPhase.Transit ? "，预计抵达 " +
                    Math.Ceiling(n.Definition.ArrivalSeconds(n.X, n.Y, active.Id)) + " 秒。" : "");
            if (data.Phase == JourneyPhase.Orbit) message = "准备就绪。本船在当前位置待命。";
            if (data.Phase == JourneyPhase.Gathering && crew != null)
                message += " 船外 " + crew.Crew.Count(v => !v.Boarded) + " 人。" +
                    (crew.Ship.DoorClock > 0 ? " 起飞倒计时 " + Math.Ceiling(crew.Ship.DoorClock) + " 秒。" : "");
            if (data.Phase == JourneyPhase.Descent)
                message += " 降落预计 " + Math.Ceiling(Math.Max(0, n.Definition.DescentSeconds - data.PhaseElapsed)) + " 秒。";
            if (n.SettledRunId.Length != 0) message += " 上次回收: +" + n.LastIncome + "。";
            current.text = message;
        }

        public void SetOpen(bool visible)
        {
            IsOpen = visible; surface.EnableInClassList("is-open", visible);
            if (!visible) { map.CancelGesture(); dialog.Close(); coordinate = false; selected = ""; }
            else close.Focus();
        }
        private void Select(string id) { selected = id; coordinate = false; Present(journey, canChange, expedition); }
        private void SelectCoordinate(float x, float y)
        { coordinate = true; targetX = x; targetY = y; Present(journey, canChange, expedition); }
        private void ChooseDirectory(ChangeEvent<string> evt)
        {
            int index = directory.choices.IndexOf(evt.newValue);
            if (index < 0 || index >= known.Count) return;
            Select(known[index].Id); map.FocusSelection();
        }
        private void Plot() => dialog.Open(false);
        private void Help() => dialog.Open(true);
        private static string Format(float v) => (v < 0 ? "−" : "+") + Math.Abs(v).ToString("000");
        private void ZoomIn() => map.ZoomBy(1.25f);
        private void ZoomOut() => map.ZoomBy(.8f);
        private void UpdateZoom() => zoom.text = map.ZoomPercent + "%";
        private void Resize(GeometryChangedEvent evt)
        {
            card.EnableInClassList("is-compact", evt.newRect.width < 800);
            card.EnableInClassList("is-narrow", evt.newRect.width < 550);
        }
        private void RequestClose() => CloseRequested?.Invoke();
        private void Confirm()
        {
            if (!confirm.enabledInHierarchy) return;
            if (coordinate) CoordinateRequested?.Invoke(targetX, targetY); else DestinationRequested?.Invoke(selected);
        }
        private void Depart() { if (depart.enabledInHierarchy) OperationRequested?.Invoke("depart"); }
        private void Cancel() { if (cancel.enabledInHierarchy) OperationRequested?.Invoke("cancel-flight"); }
        public void Dispose()
        {
            close.clicked -= RequestClose; confirm.clicked -= Confirm;
            depart.clicked -= Depart; cancel.clicked -= Cancel;
            locate.clicked -= map.FocusShip; fit.clicked -= map.FitAll; menuView.clicked -= map.FitAll;
            zoomIn.clicked -= ZoomIn; zoomOut.clicked -= ZoomOut;
            plot.clicked -= Plot; menuPlot.clicked -= Plot; menuHelp.clicked -= Help;
            directory.UnregisterValueChangedCallback(ChooseDirectory); card.UnregisterCallback<GeometryChangedEvent>(Resize);
            dialog.Accepted -= SelectCoordinate; dialog.Dispose();
            map.Selected -= Select; map.CoordinateSelected -= SelectCoordinate; map.ViewChanged -= UpdateZoom; map.Dispose();
            CloseRequested = null; DestinationRequested = OperationRequested = null; CoordinateRequested = null;
        }
    }
}
