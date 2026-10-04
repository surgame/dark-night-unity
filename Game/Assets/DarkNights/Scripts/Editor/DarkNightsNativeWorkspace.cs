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
    /// 通过 Unity 公开停靠接口按需打开原生编辑器；只选择目标和窗口，不转移控件或接管保存、草稿及关闭流程。
    /// 已存在窗口原位复用；新窗口优先与工作台停靠，无法找到停靠区时由 Unity 使用普通窗口。
    /// </summary>
    internal static class DarkNightsNativeWorkspace
    {
        internal static bool Blocked => EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating;

        private static T Open<T>(string title, Vector2 minimum) where T : EditorWindow
        {
            if (Blocked) throw new InvalidOperationException("请等待导入／编译完成，并退出 Play 后打开编辑器。");
            var window = EditorWindow.GetWindow<T>(title, true, typeof(DarkNightsWorkbenchWindow),
                typeof(ObjectDefinitionWorkshopWindow), typeof(ExpeditionFlowWindow), typeof(TerrainBusinessWindow),
                typeof(TerrainStylePreviewWindow), typeof(ObjectDefinitionViewer));
            window.minSize = minimum;
            window.Show();
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
            Open<TerrainStylePreviewWindow>("Cave Wall Tuner", new Vector2(800, 520));
            TerrainStylePreviewWindow.OpenJourney();
        }
        internal static void Terrain() => Open<TerrainBusinessWindow>("网格业务配置", new Vector2(680, 460));
        internal static void Visual() => Open<TerrainStylePreviewWindow>("Cave Wall Tuner", new Vector2(800, 520));
        internal static void Mining() => Open<MiningDefinitionWindow>("采集校验", new Vector2(640, 460));
    }
}
