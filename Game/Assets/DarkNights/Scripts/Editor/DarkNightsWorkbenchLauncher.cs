using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 独立任务区的完整场景目录；分组和搜索直接过滤打开／定位操作，不为每个场景创建中间页面。
    /// 定时器只刷新场景状态，资产存在性在目录重建时读取；事件和定时器随面板释放。
    /// </summary>
    internal sealed class DarkNightsWorkbenchLauncher : VisualElement, IDisposable
    {
        private readonly DarkNightsWorkbenchEntry[] entries = GameSceneWorkbenchCatalog.Entries;
        private readonly VisualElement filters = new VisualElement();
        private readonly ScrollView content = new ScrollView();
        private readonly HelpBox error = new HelpBox("", HelpBoxMessageType.Error);
        private readonly List<Tuple<Button, DarkNightsWorkbenchEntry, VisualElement, bool>> actions = new List<Tuple<Button, DarkNightsWorkbenchEntry, VisualElement, bool>>();
        private readonly Action<string> rememberGroup, setStatus;
        private readonly IVisualElementScheduledItem refresh;
        private readonly Dictionary<string, bool> expanded = new Dictionary<string, bool>();
        private string query, group;
        private int visible;
        private bool disposed, subscribed;

        internal DarkNightsWorkbenchLauncher(string query, string group, Action<string> rememberGroup, Action<string> setStatus)
        {
            this.query = query; this.group = group; this.rememberGroup = rememberGroup; this.setStatus = setStatus;
            AddToClassList("dn-launcher"); filters.AddToClassList("dn-filters"); Add(filters);
            error.style.display = DisplayStyle.None; Add(error);
            content.name = "launcher-content"; content.AddToClassList("dn-launcher-content"); Add(content);
            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            refresh = schedule.Execute(UpdateAvailability).Every(250); Rebuild();
        }

        private static string Family(DarkNightsWorkbenchEntry entry) =>
            entry.Group == "独立样例" || entry.Group == "编辑器模板" ? "样例与模板" : entry.Group;
        internal void Filter(string value) { query = value; Rebuild(); }

        private void Rebuild()
        {
            if (disposed) return;
            filters.Clear(); content.Clear(); actions.Clear(); error.style.display = DisplayStyle.None;
            var groups = new[] { "全部" }.Concat(entries.Select(Family).Distinct()).ToArray();
            if (!groups.Contains(group)) { group = "全部"; rememberGroup(group); }
            foreach (string category in groups)
            {
                var chip = new Button(() => { group = category; rememberGroup(group); Rebuild(); }) { text = category, name = "filter-" + category };
                chip.AddToClassList("dn-chip"); chip.EnableInClassList("dn-chip-active", group == category); filters.Add(chip);
            }
            var matches = entries.Where(entry => entry.Matches(query) && (group == "全部" || Family(entry) == group) &&
                (entry.Group != "已退役" || group == "已退役" || !string.IsNullOrWhiteSpace(query))).ToArray();
            visible = matches.Length;
            foreach (var section in matches.GroupBy(Family))
            {
                VisualElement container;
                if (section.Key == "专用测试" || section.Key == "样例与模板" || section.Key == "已退役")
                {
                    string key = section.Key;
                    var foldout = new Foldout { text = key + " · " + section.Count(), value = group != "全部" || !string.IsNullOrWhiteSpace(query) || expanded.TryGetValue(key, out bool open) && open };
                    foldout.RegisterValueChangedCallback(change => { if (change.target == foldout) expanded[key] = change.newValue; });
                    content.Add(foldout); container = foldout;
                }
                else
                {
                    var label = new Label(section.Key + " · " + section.Count()); label.AddToClassList("dn-section-title"); content.Add(label);
                    container = new VisualElement(); content.Add(container);
                }
                foreach (var entry in section) container.Add(CreateEntry(entry));
            }
            if (matches.Length == 0)
            {
                var empty = new Label("没有匹配的场景。试试用途、名称或路径，或清除搜索。"); empty.AddToClassList("dn-empty"); content.Add(empty);
            }
            UpdateAvailability();
        }

        private VisualElement CreateEntry(DarkNightsWorkbenchEntry entry)
        {
            var row = new VisualElement { name = "launch-" + entry.Id }; row.AddToClassList("dn-scene-row");
            var info = new VisualElement(); info.AddToClassList("dn-entry-info"); row.Add(info);
            var title = new Label(entry.Title); title.AddToClassList("dn-entry-title"); info.Add(title);
            var description = new Label(entry.Description); description.AddToClassList("dn-description"); info.Add(description);
            var asset = AssetDatabase.LoadMainAssetAtPath(entry.Assets[0]);
            var path = new Label(entry.Assets[0]) { tooltip = entry.Assets[0] }; path.AddToClassList("dn-path"); info.Add(path);
            if (asset == null) info.Add(new HelpBox("资产不存在：" + entry.Assets[0], HelpBoxMessageType.Warning));
            var buttons = new VisualElement(); buttons.AddToClassList("dn-actions"); row.Add(buttons);
            var action = new Button(() => Execute(entry)) { text = entry.ActionLabel, name = "action-" + entry.Id, tooltip = entry.SaveHint };
            buttons.Add(action); actions.Add(Tuple.Create(action, entry, row, asset != null));
            if (entry.Kind == DarkNightsWorkbenchEntryKind.Scene)
            {
                var locate = new Button(() => DarkNightsWorkbenchWindow.Locate(asset)) { text = "定位", tooltip = entry.Assets[0] };
                locate.SetEnabled(asset != null); buttons.Add(locate);
            }
            return row;
        }

        private void Execute(DarkNightsWorkbenchEntry entry)
        {
            if (entry.Kind == DarkNightsWorkbenchEntryKind.Scene && DarkNightsNativeWorkspace.Blocked) return;
            try { entry.OpenEditor?.Invoke(); error.style.display = DisplayStyle.None; }
            catch (Exception exception) { error.text = exception.Message; error.style.display = DisplayStyle.Flex; }
        }

        private void UpdateAvailability()
        {
            if (disposed) return;
            string path = SceneManager.GetActiveScene().path;
            foreach (var item in actions)
            {
                item.Item1.SetEnabled(item.Item4 && (item.Item2.Kind == DarkNightsWorkbenchEntryKind.Reference || !DarkNightsNativeWorkspace.Blocked));
                item.Item3.EnableInClassList("dn-current-scene", item.Item2.Assets[0] == path);
            }
            setStatus(visible + " / " + entries.Length + " 个场景 · 打开前提示保存场景；退役项仅定位");
        }
        private void Subscribe() { if (disposed || subscribed) return; EditorApplication.projectChanged += Rebuild; subscribed = true; }
        private void Unsubscribe() { if (subscribed) EditorApplication.projectChanged -= Rebuild; subscribed = false; }
        public void Dispose() { if (disposed) return; disposed = true; Unsubscribe(); refresh.Pause(); actions.Clear(); Clear(); }
    }
}
