using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.View.MapModule;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEngine.UIElements;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 工坊的扁平地图目录；原生Scene／Case身份保留在内部，不增加用户必须逐层选择的研究／样例层。
    /// 元数据从已有作者Scene或临时只读PreviewScene读取，点击只提交预览选择，不切活动场景。
    /// </summary>
    internal sealed class MapModuleStudyNavigation
    {
        private readonly List<(string Path, string CaseId, string Title, int Modules)> maps =
            new List<(string, string, string, int)>();
        internal string Filter = "";
        internal (string Path, string CaseId) First => maps.Count == 0 ? ("", "") : (maps[0].Path, maps[0].CaseId);

        internal void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            maps.Clear();
            string folder = System.IO.Path.GetDirectoryName(MapModulePaths.Scene).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool temporary = !scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene);
                if (temporary) scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var study = MapModuleStudyPreview.Find(scene);
                    if (study == null) continue;
                    var catalog = GameSceneWorkbenchCatalog.Entries.FirstOrDefault(e => e.Assets.Contains(path));
                    foreach (var item in study.Cases.Where(c => c != null))
                        maps.Add((path, item.CaseId, study.Cases.Length == 1 ? catalog?.Title ?? item.DisplayName :
                            item.DisplayName, item.Modules.Count(m => m != null)));
                }
                finally { if (temporary) EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        internal void Draw(VisualElement parent, string selectedPath, string selectedCase, Action<string, string> select)
        {
            parent.Clear();
            int shown = 0;
            foreach (var map in maps)
            {
                if (!string.IsNullOrWhiteSpace(Filter) &&
                    (map.Title + " " + map.Path).IndexOf(Filter.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool selected = map.Path == selectedPath && map.CaseId == selectedCase;
                var button = new Button(() => select(map.Path, map.CaseId))
                    { name = "map-" + AssetDatabase.AssetPathToGUID(map.Path) + "-" + map.CaseId, tooltip = map.Path + "\n" + map.CaseId };
                button.AddToClassList("study-item");
                button.Add(new Label(map.Title) { pickingMode = PickingMode.Ignore });
                var caption = new Label((selected ? "已选 · " : "") + map.Modules + "个模块") { pickingMode = PickingMode.Ignore };
                caption.AddToClassList("item-caption"); button.Add(caption);
                button.EnableInClassList("selected", selected);
                button.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling);
                parent.Add(button); shown++;
            }
            if (shown == 0) parent.Add(new Label(maps.Count == 0 ? "尚无地图。" : "没有匹配的地图。"));
        }
    }
}
