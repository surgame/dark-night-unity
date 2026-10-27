using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 项目特有的非 Definition 作者来源面板；只缓存当前原生 Inspector，不复制配置。
    /// 重建及释放销毁句柄；源文件使用原生编辑器，Play、导入和编译期间禁止写入。
    /// </summary>
    internal sealed class DarkNightsWorkbenchSources : VisualElement, IDisposable
    {
        private UnityEditor.Editor inspector;
        private readonly VisualElement area = new VisualElement();
        private readonly Label status = new Label();
        private readonly IVisualElementScheduledItem refresh;
        private UnityEngine.Object asset;
        private bool disposed;

        internal DarkNightsWorkbenchSources(DarkNightsWorkbenchEntry entry, string selected, Action<string> remember)
        {
            AddToClassList("dn-sources");
            Add(new Label(entry.Title) { name = "source-title" });
            var description = new Label(entry.Description); description.AddToClassList("dn-description"); Add(description);
            var paths = entry.Assets.ToList();
            var choice = new PopupField<string>("编辑内容", paths.Select(Label).ToList(),
                Math.Max(0, paths.IndexOf(selected))) { name = "source-choice" };
            Add(choice); Add(area);
            choice.RegisterValueChangedCallback(change => { remember(paths[choice.index]); Draw(paths[choice.index]); });
            remember(paths[choice.index]); Draw(paths[choice.index]);
            refresh = schedule.Execute(UpdateState).Every(250);
        }

        private void Draw(string path)
        {
            if (inspector != null) UnityEngine.Object.DestroyImmediate(inspector);
            inspector = null; area.Clear(); asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) { area.Add(new HelpBox("资产不存在：" + path, HelpBoxMessageType.Warning)); return; }
            var location = new Label(path); location.AddToClassList("dn-path"); area.Add(location);
            var actions = new VisualElement(); actions.AddToClassList("dn-actions"); area.Add(actions);
            actions.Add(new Button(() => DarkNightsWorkbenchWindow.Locate(asset)) { text = "定位作者来源" });
            if (asset is UnityEngine.TextAsset || path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".tss", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(new Button(() => { if (!DarkNightsNativeWorkspace.Blocked) AssetDatabase.OpenAsset(asset); })
                    { text = "用原生 / 外部编辑器打开", name = "open-source" });
                return;
            }
            UnityEditor.Editor.CreateCachedEditor(asset, null, ref inspector);
            area.Add(new IMGUIContainer(() =>
            {
                if (disposed || inspector == null) return;
                using (new EditorGUI.DisabledScope(DarkNightsNativeWorkspace.Blocked)) inspector.OnInspectorGUI();
            }));
            area.Add(status);
            area.Add(new Button(() => { if (!DarkNightsNativeWorkspace.Blocked) AssetDatabase.SaveAssetIfDirty(asset); })
                { text = "保存当前资产", name = "save-source" });
            UpdateState();
        }

        private static string Label(string path)
        {
            string file = System.IO.Path.GetFileName(path);
            if (path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase)) return "界面布局 UXML";
            if (path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase)) return "外观样式 USS";
            if (path.EndsWith(".tss", StringComparison.OrdinalIgnoreCase)) return "界面主题 TSS";
            if (file.Contains("PanelSettings")) return "缩放与渲染 PanelSettings";
            if (file == "balance.json") return "经济与数值 balance.json";
            if (file == "pinewatch.json") return "关卡元数据 pinewatch.json";
            return file;
        }

        private void UpdateState()
        {
            if (disposed) return;
            this.Q<Button>("open-source")?.SetEnabled(!DarkNightsNativeWorkspace.Blocked);
            this.Q<Button>("save-source")?.SetEnabled(!DarkNightsNativeWorkspace.Blocked);
            status.text = DarkNightsNativeWorkspace.Blocked ? "Play／导入／编译期间暂停编辑。" :
                asset != null && EditorUtility.IsDirty(asset) ? "当前作者资产有未保存修改。" : "当前作者资产已保存。";
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; refresh.Pause();
            if (inspector != null) UnityEngine.Object.DestroyImmediate(inspector);
            inspector = null; Clear();
        }
    }
}
