using System;
using System.Collections.Generic;
using DarkNights.Core.ViewData;
using DarkNights.View.Expedition;
using UnityEngine.UIElements;

namespace DarkNights.Entry
{
    /// <summary>
    /// 既有飞船UIPanel内的导航内容绑定；只读取冻结航程，个人选择不更改全队目标。
    /// 开关交互许可由ShipTradeHud持有，业务动作仅发出明确目标的意图事件。
    /// </summary>
    internal sealed class ShipNavigationContent : IDisposable
    {
        private readonly VisualElement surface;
        private readonly Label status, detail, permission;
        private readonly Button open, close, confirm, depart, cancel;
        private readonly StarMapElement map;
        private readonly List<Button> rows = new List<Button>();
        private readonly VisualElement list;
        private JourneyViewData journey;
        private string selected = "", signature = "";
        private bool canChange;
        private ExpeditionViewData expedition;
        public bool IsOpen { get; private set; }
        public event Action OpenRequested;
        public event Action CloseRequested;
        public event Action<string> DestinationRequested;
        public event Action<string> OperationRequested;

        public ShipNavigationContent(VisualElement root)
        {
            surface = root.Q("navigation"); status = root.Q<Label>("flight-status");
            detail = root.Q<Label>("navigation-detail"); permission = root.Q<Label>("navigation-permission");
            open = root.Q<Button>("open-navigation"); close = root.Q<Button>("close-navigation");
            confirm = root.Q<Button>("confirm-destination"); list = root.Q("navigation-list");
            depart = root.Q<Button>("depart-expedition"); cancel = root.Q<Button>("cancel-departure");
            map = new StarMapElement(); root.Q("star-map-host").Add(map);
            map.Selected += Select; open.clicked += RequestOpen; close.clicked += RequestClose;
            confirm.clicked += Confirm;
            depart.clicked += Depart; cancel.clicked += Cancel;
        }

        public void Present(JourneyViewData data, bool permitted, ExpeditionViewData crew = null)
        {
            expedition = crew;
            journey = data; canChange = permitted;
            open.SetEnabled(data?.Navigation != null);
            if (data?.Navigation == null) { status.text = "导航资料准备中"; return; }
            var n = data.Navigation; var node = n.Definition.Find(data.PlanetId);
            string phase = data.Phase == JourneyPhase.Landed ? "矿区停泊" : data.Phase == JourneyPhase.ArrivalSync ? "正在准备降落环境" :
                data.Phase == JourneyPhase.Descent ? "自动降落" : data.Phase == JourneyPhase.Preparing ? "目的地准备中" :
                data.Phase == JourneyPhase.Gathering ? "集合撤离 · 全员登船后倒计时" : data.Phase == JourneyPhase.Ascent ? "自动升空" :
                data.Phase == JourneyPhase.Transit ? "前往" : "安全待命";
            status.text = node == null ? phase + " · 等待选择目的地" : phase + " " + node.Name +
                (data.Phase == JourneyPhase.Transit ? " · 预计抵达 " + Math.Ceiling(n.Definition.ArrivalSeconds(n.X, n.Y, node.Id)) + " 秒" : "") +
                " · 危险 " + StarMapElement.DangerLabel(node.Danger);
            if (n.SettledRunId.Length != 0) status.text += " · 上次回收 ＋" + n.LastIncome;
            if (data.Phase == JourneyPhase.Descent)
                status.text += " · 预计抵达 " + Math.Ceiling(Math.Max(0, n.Definition.DescentSeconds - data.PhaseElapsed)) + " 秒";
            if (data.Phase == JourneyPhase.Gathering && crew != null)
            {
                int outside = 0;
                foreach (var person in crew.Crew) if (!person.Boarded) outside++;
                status.text += " · 船外 " + outside + " 人" + (crew.Ship.DoorClock > 0 ? " · 起飞 " + Math.Ceiling(crew.Ship.DoorClock) + " 秒" : "");
            }
            string incoming = data.ContentFingerprint;
            if (signature != incoming)
            {
                signature = incoming; list.Clear(); rows.Clear();
                foreach (var entry in n.Definition.Nodes)
                {
                    string id = entry.Id;
                    var row = new Button(() => Select(id)) { name = "planet-row-" + id }; row.AddToClassList("navigation-row");
                    list.Add(row); rows.Add(row);
                }
            }
            if (selected.Length == 0 || n.Definition.Find(selected) == null)
                selected = node?.Id ?? n.Definition.Nodes[0].Id;
            for (int i = 0; i < rows.Count; i++)
            {
                var entry = n.Definition.Nodes[i];
                rows[i].text = entry.Name + "   距离 " + Math.Ceiling(n.Definition.Distance(n.X, n.Y, entry.Id)) +
                    " · " + Math.Ceiling(n.Definition.ArrivalSeconds(n.X, n.Y, entry.Id)) +
                    "秒   危险 " + StarMapElement.DangerLabel(entry.Danger) + (entry.PreviewOnly ? " · 仅预览" : "");
                rows[i].EnableInClassList("is-selected", selected == entry.Id);
            }
            var chosen = n.Definition.Find(selected);
            detail.text = chosen.Risk + "\n危险资料为mock · 当前船位 " + n.X.ToString("F1") + " / " + n.Y.ToString("F1");
            if (data.Error.Length != 0) detail.text += "\n" + data.Error;
            permission.text = permitted ? "在导航台确认；浏览不会改变航线" : "所有人可查看；改变航线需满足权限、位置和阶段";
            confirm.SetEnabled(canChange && !chosen.PreviewOnly && data.Phase is JourneyPhase.Orbit or JourneyPhase.Transit);
            confirm.text = chosen.PreviewOnly ? "示意节点尚未开放" : "确认前往 " + chosen.Name;
            depart.SetEnabled(canChange && data.Phase == JourneyPhase.Landed);
            cancel.SetEnabled(canChange && (data.Phase is JourneyPhase.Gathering or JourneyPhase.Preparing or JourneyPhase.Transit ||
                data.Phase == JourneyPhase.Ascent && data.Error.Length != 0));
            cancel.text = data.Phase == JourneyPhase.Ascent ? "重试安全交接" : "取消集合／航程";
            map.Present(data, selected);
        }

        public void SetOpen(bool visible)
        {
            IsOpen = visible; surface.EnableInClassList("is-open", visible);
            if (visible) close.Focus();
        }

        private void Select(string id) { selected = id; Present(journey, canChange, expedition); }
        private void RequestOpen() => OpenRequested?.Invoke();
        private void RequestClose() => CloseRequested?.Invoke();
        private void Confirm() { if (confirm.enabledInHierarchy) DestinationRequested?.Invoke(selected); }
        private void Depart() { if (depart.enabledInHierarchy) OperationRequested?.Invoke("depart"); }
        private void Cancel() { if (cancel.enabledInHierarchy) OperationRequested?.Invoke("cancel-flight"); }

        public void Dispose()
        {
            open.clicked -= RequestOpen; close.clicked -= RequestClose; confirm.clicked -= Confirm;
            depart.clicked -= Depart; cancel.clicked -= Cancel; OperationRequested = null;
            map.Selected -= Select; OpenRequested = CloseRequested = null; DestinationRequested = null;
        }
    }
}
