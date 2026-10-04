using System;
using DarkNights.Editor.Terrain;
using GameCore.Editor.Objects.Definition;
using GameCore.Editor.Objects.Runner;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 通过 Unity 公开窗口接口按需打开独立浮窗；只选择目标和窗口，不接管保存、草稿及关闭流程。
    /// 复用现有实例并解除旧布局停靠，避免关闭重建造成未保存草稿丢失。
    /// </summary>
    internal static class DarkNightsNativeWorkspace
    {
        internal static bool Blocked => EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating;

        internal static T Open<T>(string title, Vector2 minimum, bool navigationOnly = false) where T : EditorWindow
        {
            if (Blocked && !navigationOnly) throw new InvalidOperationException("请等待导入／编译完成，并退出 Play 后打开编辑器。");
            var existing = Resources.FindObjectsOfTypeAll<T>();
            var window = existing.Length > 0 ? existing[0] : ScriptableObject.CreateInstance<T>();
            bool place = existing.Length == 0 || window.docked;
            window.titleContent = new GUIContent(title);
            window.minSize = minimum;
            if (place)
            {
                var host = EditorGUIUtility.GetMainWindowPosition();
                var size = new Vector2(Mathf.Max(minimum.x, Mathf.Min(1120, host.width - 100)),
                    Mathf.Max(minimum.y, Mathf.Min(760, host.height - 120)));
                window.position = new Rect(host.center - size * 0.5f, size);
            }
            window.Show();
            window.Focus();
            return window;
        }

        internal static void Workshop() => Workshop(null);
        internal static void Workshop(ObjectDefinition target)
        {
            Open<ObjectDefinitionWorkshopWindow>("配置工坊 (Workshop)", new Vector2(1000, 500));
            // 使用原生公开入口选择目标；工坊自身继续拥有配置同步和保存语义。
            ObjectDefinitionWorkshopWindow.OpenWindow(target);
        }

        internal static void Viewer() => Open<ObjectDefinitionViewer>("定义文件", new Vector2(800, 400));
        internal static void Journey()
        {
            TerrainStylePreviewWindow.OpenJourney();
        }
        internal static void Terrain() => Open<TerrainBusinessWindow>("网格业务配置", new Vector2(680, 460));
        internal static void Visual() => Open<TerrainStylePreviewWindow>("Cave Wall Tuner", new Vector2(800, 520));
        internal static void Mining() => Open<MiningDefinitionWindow>("采集校验", new Vector2(640, 460));
    }
}
