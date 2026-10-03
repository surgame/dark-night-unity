using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 场景目录和专用工具的直接操作面板；分组过滤与折叠属于导航，不创建中间详情页。
    /// 定时器只刷新当前场景及可操作状态；项目事件、按钮记录和定时器随面板释放，资产始终引用原始来源。
    /// </summary>
    internal sealed class DarkNightsWorkbenchLauncher : VisualElement, IDisposable
    {
        private readonly bool scenes;
        private readonly DarkNightsWorkbenchEntry[] entries;
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

        internal DarkNightsWorkbenchLauncher(bool scenes, string query, string group, Action<string> rememberGroup, Action<string> setStatus)
        {
            this.scenes = scenes; this.query = query; this.group = group;
            this.rememberGroup = rememberGroup; this.setStatus = setStatus;
            entries = DarkNightsWorkbenchCatalog.Entries.Where(entry => scenes
                ? entry.Kind == DarkNightsWorkbenchEntryKind.Scene || entry.Kind == DarkNightsWorkbenchEntryKind.Reference
                : entry.Kind == DarkNightsWorkbenchEntryKind.Tool).ToArray();
            AddToClassList("dn-launcher");
            var heading = new Label(scenes ? "场景与测试" : "工具与预览"); heading.AddToClassList("dn-page-title"); Add(heading);
            var description = new Label(scenes ? "选择用途，直接打开场景。完整游戏从正式游戏入口开始。" : "直接启动专用编辑器；草稿、应用和预览由各工具管理。");
            description.AddToClassList("dn-description"); Add(description);
            filters.AddToClassList("dn-filters"); Add(filters);
            error.style.display = DisplayStyle.None; Add(error);
            content.name = "launcher-content"; content.AddToClassList("dn-launcher-content"); Add(content);
            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
            refresh = schedule.Execute(UpdateAvailability).Every(250); Rebuild();
        }

        private string Family(DarkNightsWorkbenchEntry entry)
        {
            if (entry.Group == "独立样例" || entry.Group == "编辑器模板") return "样例与模板";
            return entry.Group;
        }

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
                bool secondary = scenes && (section.Key == "专用测试" || section.Key == "样例与模板" || section.Key == "已退役");
                VisualElement container;
                if (secondary)
                {
                    string key = section.Key;
                    var foldout = new Foldout { text = key + " · " + section.Count(), value = group != "全部" || !string.IsNullOrWhiteSpace(query) || expanded.TryGetValue(key, out bool open) && open };
                    foldout.AddToClassList("dn-section-foldout");
                    foldout.RegisterValueChangedCallback(change => { if (change.target == foldout) expanded[key] = change.newValue; });
                    content.Add(foldout); container = foldout;
                }
                else
                {
                    var label = new Label(section.Key + " · " + section.Count()); label.AddToClassList("dn-section-title"); content.Add(label);
                    container = new VisualElement(); content.Add(container);
                }
                if (!scenes) container.AddToClassList("dn-tool-grid");
                foreach (var entry in section) container.Add(CreateEntry(entry));
            }
            if (matches.Length == 0)
            {
                var empty = new Label("没有匹配的入口。试试用途、名称或路径，或清除搜索。"); empty.AddToClassList("dn-empty"); content.Add(empty);
            }
            UpdateAvailability();
        }

        private VisualElement CreateEntry(DarkNightsWorkbenchEntry entry)
        {
            var card = new VisualElement { name = "launch-" + entry.Id }; card.AddToClassList(scenes ? "dn-scene-row" : "dn-tool-card");
            var info = new VisualElement(); info.AddToClassList("dn-entry-info"); card.Add(info);
            var title = new Label(entry.Title); title.AddToClassList("dn-entry-title"); info.Add(title);
            var description = new Label(entry.Description); description.AddToClassList("dn-description"); info.Add(description);
            var asset = entry.Assets.Count > 0 ? AssetDatabase.LoadMainAssetAtPath(entry.Assets[0]) : null;
            if (scenes)
            {
                var path = new Label(entry.Assets[0]) { tooltip = entry.Assets[0] }; path.AddToClassList("dn-path"); info.Add(path);
            }
            else
            {
                var hint = new Label(entry.SaveHint); hint.AddToClassList("dn-save-hint"); info.Add(hint);
                var sources = new Foldout { text = "作者资产 · " + entry.Assets.Count, value = false }; sources.AddToClassList("dn-sources"); info.Add(sources);
                foreach (string path in entry.Assets)
                {
                    var source = AssetDatabase.LoadMainAssetAtPath(path);
                    var locate = new Button(() => DarkNightsWorkbenchWindow.Locate(source)) { text = System.IO.Path.GetFileName(path), tooltip = path };
                    locate.SetEnabled(source != null); sources.Add(locate);
                    if (source == null) sources.Add(new HelpBox("资产不存在：" + path, HelpBoxMessageType.Warning));
                }
            }
            if (asset == null) info.Add(new HelpBox("资产不存在：" + entry.Assets.FirstOrDefault(), HelpBoxMessageType.Warning));
            var buttons = new VisualElement(); buttons.AddToClassList("dn-actions"); card.Add(buttons);
            var action = new Button(() => Execute(entry)) { text = entry.ActionLabel, name = "action-" + entry.Id, tooltip = entry.SaveHint };
            action.AddToClassList(entry.Kind == DarkNightsWorkbenchEntryKind.Reference ? "dn-secondary" : "dn-primary"); buttons.Add(action);
            actions.Add(Tuple.Create(action, entry, card, entry.Assets.All(source => AssetDatabase.LoadMainAssetAtPath(source) != null)));
            if (scenes && entry.Kind == DarkNightsWorkbenchEntryKind.Scene)
            {
                var locate = new Button(() => DarkNightsWorkbenchWindow.Locate(asset)) { text = "定位", tooltip = entry.Assets[0] };
                locate.SetEnabled(asset != null); buttons.Add(locate);
            }
            return card;
        }

        private void Execute(DarkNightsWorkbenchEntry entry)
        {
            if (entry.Kind == DarkNightsWorkbenchEntryKind.Scene && (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)) return;
            try { entry.OpenEditor?.Invoke(); error.style.display = DisplayStyle.None; }
            catch (Exception exception) { error.text = exception.Message; error.style.display = DisplayStyle.Flex; }
        }

        private void UpdateAvailability()
        {
            if (disposed) return;
            bool blocked = EditorApplication.isCompiling || EditorApplication.isUpdating;
            bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
            string path = SceneManager.GetActiveScene().path;
            foreach (var item in actions)
            {
                var entry = item.Item2;
                item.Item1.SetEnabled(item.Item4 && (entry.Kind == DarkNightsWorkbenchEntryKind.Reference || !blocked && (entry.Kind != DarkNightsWorkbenchEntryKind.Scene || !playing)));
                item.Item3.EnableInClassList("dn-current-scene", scenes && entry.Assets[0] == path);
            }
            setStatus(visible + " / " + entries.Length + (scenes ? " 个场景 · " + (playing ? "请退出 Play 后打开场景" : blocked ? "导入／编译中，请稍候" : "打开前提示保存场景；退役项仅定位")
                : " 个工具 · 草稿在各自窗口应用或取消"));
        }
        private void Subscribe() { if (disposed || subscribed) return; EditorApplication.projectChanged += Rebuild; subscribed = true; }
        private void Unsubscribe() { if (subscribed) EditorApplication.projectChanged -= Rebuild; subscribed = false; }
        public void Dispose() { if (disposed) return; disposed = true; Unsubscribe(); refresh.Pause(); actions.Clear(); Clear(); }
    }
}
