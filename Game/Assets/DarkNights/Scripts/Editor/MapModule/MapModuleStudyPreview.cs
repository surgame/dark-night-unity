using System;
using System.Linq;
using DarkNights.View.MapModule;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 工坊持有的只读原生预览生命周期；选择地图不改变Unity活动Scene或Hierarchy中的作者场景。
    /// 已加载作者源直接只读引用，其余只打开一个PreviewScene；窗口关闭／重载时仅释放自有预览。
    /// </summary>
    internal sealed class MapModuleStudyPreview : IDisposable
    {
        private Scene preview;
        internal Scene Select(string path)
        {
            Scene loaded = SceneManager.GetSceneByPath(path);
            if (loaded.IsValid() && loaded.isLoaded && !EditorSceneManager.IsPreviewScene(loaded))
            { Dispose(); return loaded; }
            if (preview.IsValid() && preview.isLoaded && preview.path == path) return preview;
            var next = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                if (Find(next) == null) throw new InvalidOperationException("地图缺少原生作者目录。");
                Dispose(); preview = next; return preview;
            }
            catch { EditorSceneManager.ClosePreviewScene(next); throw; }
        }

        internal static MapModuleStudyAuthoring Find(Scene scene) => scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapModuleStudyAuthoring>(true)).SingleOrDefault()
            : null;

        internal void Reload() => Dispose();
        public void Dispose()
        {
            if (preview.IsValid() && preview.isLoaded) EditorSceneManager.ClosePreviewScene(preview);
            preview = default;
        }
    }
}
