using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图工作台常驻左侧文件选择器；只管理搜索、资源发现和选择高亮，编辑上下文仍归窗口。
    /// 项目变化只标记目录失效，空闲时重建一次；释放时撤销订阅，不在每帧扫描资源。
    /// </summary>
    internal sealed class MapWorkbenchLibrary : IDisposable
    {
        private readonly MapWorkbenchWindow owner;
        private readonly VisualElement root;
        private readonly ScrollView list;
        private readonly Label count;
        private readonly Dictionary<MapWorkbenchMapFile, Button> rows = new Dictionary<MapWorkbenchMapFile, Button>();
        private List<MapWorkbenchMapFile> files = new List<MapWorkbenchMapFile>();
        private bool stale = true;

        internal MapWorkbenchLibrary(MapWorkbenchWindow owner, VisualElement root)
        {
            this.owner = owner; this.root = root;
            list = root.Q<ScrollView>("mapFileList"); count = root.Q<Label>("mapFileCount");
            var search = new TextField("搜索")
            { name = "mapFileSearch", tooltip = "筛选下方地图列表：编号、名称、文件名、路径、种子或类型；空格分隔多个词。" };
            search.SetValueWithoutNotify(owner.State.MapSearch);
            var clear = new Button(() => search.value = "")
            { name = "clearMapSearch", text = "清空", tooltip = "清空搜索，显示全部地图文件。" };
            clear.SetEnabled(!string.IsNullOrEmpty(search.value));
            search.RegisterValueChangedCallback(change =>
            {
                owner.State.MapSearch = change.newValue;
                clear.SetEnabled(!string.IsNullOrEmpty(change.newValue)); BuildRows();
            });
            var searchHost = root.Q("mapSearchHost");
            searchHost.Add(search); searchHost.Add(clear);
            root.Q<Button>("newMapFile").clicked += () => owner.Execute(() => MapWorkbenchFiles.New(owner));
            root.Q<Button>("refreshMapFiles").clicked += () => stale = true;
            EditorApplication.projectChanged += Invalidate;
        }

        private void Invalidate() => stale = true;

        internal void Refresh()
        {
            if (stale && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                files = MapWorkbenchMapFile.Find(); stale = false; BuildRows();
            }
            foreach (var pair in rows)
            {
                pair.Value.EnableInClassList("map-selected", pair.Key.Selected(owner));
                pair.Value.EnableInClassList("map-file-loading", pair.Key.Asset == owner.FileLoad.Target && owner.FileLoad.Busy);
            }
            root.Q("mapLibrary").SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
        }

        private void BuildRows()
        {
            list.Clear(); rows.Clear();
            foreach (var file in files.Where(x => x.Matches(owner.State.MapSearch)))
            {
                var button = new Button(() => owner.Execute(() => file.Open(owner))) { tooltip = file.Path };
                button.AddToClassList("map-file");
                AddLabel(button, "地形 " + file.Id, "map-file-id");
                AddLabel(button, file.Title, "map-file-title");
                AddLabel(button, Path.GetFileName(file.Path), "map-file-name");
                AddLabel(button, file.Kind, "map-file-kind");
                list.Add(button); rows.Add(file, button);
            }
            count.text = rows.Count + " / " + files.Count + " 份地图";
            if (rows.Count == 0)
            {
                var empty = new Label(files.Count == 0 ? "工程中尚无地图文件。" : "没有匹配的地图，试试名称或文件名。");
                empty.AddToClassList("map-note"); list.Add(empty);
            }
        }

        private static void AddLabel(VisualElement parent, string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className); parent.Add(label);
        }

        public void Dispose() => EditorApplication.projectChanged -= Invalidate;
    }
}
