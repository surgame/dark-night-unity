using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>地图、显示和诊断页；固定蓝图隐藏无效随机参数，地图保存保留打开时的原文件基线以检查外部冲突。</summary>
    public sealed class TerrainWorkbenchPages
    {
        private int generation = -1;
        private byte[] originalCells;
        private static readonly string[] Materials = { "壤土", "板岩", "玄武岩", "铜矿", "铁矿", "金矿", "苔岩" };
        public void Observe(TerrainDebugBootstrap bootstrap)
        {
            if (generation == bootstrap.Generation) return;
            generation = bootstrap.Generation;
            originalCells = bootstrap.FixedMap?.InitialCells != null ? (byte[])bootstrap.FixedMap.InitialCells.bytes.Clone() : null;
        }
        public void DrawMap(TerrainDebugPanel panel, TerrainStyleControls picker)
        {
            var boot = panel.Bootstrap; var edits = boot.Workshop?.Edits;
            GUILayout.Label("交互模式");
            int tool = GUILayout.SelectionGrid(panel.MapTool, new[] { "角色", "平移", "拆格", "填格" }, 2);
            if (tool != panel.MapTool) { edits?.EndStroke(); panel.MapTool = tool; GUI.FocusControl(null); }
            GUILayout.Label(panel.MapTool == 0 ? "Tab 切换行走/观察；左键手采，右键爆破。" : "左键拖动操作；中键平移，滚轮缩放。退出工具后回到角色镜头。");
            if (panel.MapTool == 3) panel.FillMaterial = (byte)(GUILayout.SelectionGrid(panel.FillMaterial - 1, Materials, 3) + 1);
            panel.ShowGrid = GUILayout.Toggle(panel.ShowGrid, "显示格子网格");
            bool enabled = GUI.enabled;
            GUILayout.BeginHorizontal();
            GUI.enabled = enabled && edits?.CanUndo == true && !boot.Generating;
            if (GUILayout.Button("撤销")) panel.Run(() => { edits.Undo(); boot.Preview.NotifyReplicaChanged(); });
            GUI.enabled = enabled && edits?.CanRedo == true && !boot.Generating;
            if (GUILayout.Button("重做")) panel.Run(() => { edits.Redo(); boot.Preview.NotifyReplicaChanged(); });
            GUILayout.EndHorizontal();
            GUI.enabled = enabled && edits != null && edits.ChangedCells > 0 && !boot.Generating;
            if (GUILayout.Button("取消地图草稿")) panel.Run(() => { edits.Cancel(); boot.Preview.NotifyReplicaChanged(); });
            if (TerrainWorkbenchAssets.SaveMap != null && boot.FixedMap != null && GUILayout.Button("应用地图到固定资产"))
                panel.Run(() =>
                {
                    TerrainWorkbenchAssets.SaveMap(boot.FixedMap, originalCells, edits.CaptureCells());
                    originalCells = (byte[])boot.FixedMap.InitialCells.bytes.Clone();
                    boot.RequestRegenerate(); panel.SetMessage("地图已保存；重新加载固定样板。");
                });
            GUI.enabled = enabled;
            GUILayout.Label("笔刷草稿：" + (edits?.ChangedCells ?? 0) + " 格 · 退出运行丢弃未保存修改");
            GUILayout.Space(8); GUILayout.Label("地图来源");
            if (picker.Pick("固定蓝图", boot.FixedMap, out TerrainMapAsset map) && map != boot.FixedMap)
            {
                if (edits?.ChangedCells > 0) panel.SetMessage("先保存或取消地图草稿，再切换来源。");
                else if (map != null && map.Definition != boot.Definition) panel.SetMessage("该地图的规则定义不兼容当前工作台。");
                else { boot.FixedMap = map; boot.RequestRegenerate(); }
            }
            if (picker.Pick("岩壁样式", boot.CaveStyle, out CaveTerrainStyle style) && style != boot.CaveStyle) panel.SwitchStyle(style);
            if (boot.FixedMap == null)
            {
                GUILayout.Label("随机种子"); GUI.SetNextControlName("TerrainSeed");
                boot.Settings.Seed = GUILayout.TextField(boot.Settings.Seed ?? "", 80);
                if (boot.Settings.ResourceProfile != TerrainGenerationSettings.CaveExplorationProfile)
                {
                    int surface = Math.Max(0, Array.IndexOf(TerrainGenerationSettings.SurfaceNames, boot.Settings.Surface));
                    boot.Settings.Surface = TerrainGenerationSettings.SurfaceNames[GUILayout.SelectionGrid(surface,
                        new[] { "针峰", "台地", "喀斯特", "盆地", "丘陵", "断层" }, 3)];
                    boot.Settings.OrganicCaves = GUILayout.Toggle(boot.Settings.OrganicCaves, "叠加自然洞穴");
                    boot.Settings.OreDensity = TerrainFieldControls.Slider("矿脉密度", (float)boot.Settings.OreDensity, .2f, 2);
                }
                boot.Settings.Amplitude = TerrainFieldControls.Slider("地表起伏", (float)boot.Settings.Amplitude, .3f, 1.6f);
                boot.LiveRegenerate = GUILayout.Toggle(boot.LiveRegenerate, "自动重建随机参数");
                if (GUILayout.Button("新随机种子（重置地图）")) boot.NewSeed();
            }
            if (GUILayout.Button("重新加载地图（丢弃运行时拆填）")) boot.RequestRegenerate();
            GUILayout.Space(8); GUILayout.Label("房间定位");
            int rooms = boot.Blueprint?.Rooms.Count ?? 0;
            for (int start = 0; start < rooms; start += 3)
            {
                GUILayout.BeginHorizontal();
                for (int i = start; i < Math.Min(start + 3, rooms); i++)
                    if (GUILayout.Button(i == 0 ? "入口" : "洞室 " + i)) { panel.MapTool = 0; boot.VisitRoom(i); }
                GUILayout.EndHorizontal();
            }
        }
        public void DrawDisplay(TerrainDebugPanel panel)
        {
            var flyer = panel.Bootstrap.Flyer;
            GUILayout.Label("界面（独立于游戏镜头）");
            panel.UiScale = TerrainFieldControls.Slider("UI 缩放", panel.UiScale, .75f, 1.75f);
            panel.PanelWidth = TerrainFieldControls.Slider("面板宽度", panel.PanelWidth, 280, 520);
            if (GUILayout.Button("恢复自适应布局")) { panel.UiScale = 1; panel.PanelWidth = 380; }
            GUILayout.Label("当前有效缩放 " + panel.Layout.Scale.ToString("0.00") + "× · 小窗口自动限制，内容纵向滚动。");
            GUILayout.Space(8); GUILayout.Label("镜头");
            flyer.CameraDistance = TerrainFieldControls.Slider("距离（越小越近）", flyer.CameraDistance, 5, panel.MapTool == 0 ? 100 : 600);
            flyer.Speed = TerrainFieldControls.Slider("观察飞行速度", flyer.Speed, 2, 100);
            if (GUILayout.Button("适配全图")) { panel.MapTool = 1; flyer.FitWorkbenchMap(panel.Layout.Panel.xMax * panel.Layout.Scale); }
            if (GUILayout.Button("回到角色镜头")) { panel.MapTool = 0; flyer.ResetWorkbenchCamera(); }
            panel.ShowGrid = GUILayout.Toggle(panel.ShowGrid, "显示地形网格");
        }
        public void DrawStatus(TerrainDebugPanel panel)
        {
            var boot = panel.Bootstrap; var preview = boot.Preview;
            GUILayout.Label(boot.Status);
            if (!string.IsNullOrEmpty(boot.LastError)) GUILayout.Label("最近生成错误：" + boot.LastError);
            if (preview?.LastError != null) GUILayout.Label("表现错误：" + preview.LastError.Message);
            GUILayout.Label("刷新路径：" + (preview?.RefreshPath ?? "尚未创建"));
            GUILayout.Label("进度：" + (preview?.PresentationWaitReason ?? "等待地图"));
            GUILayout.Label("岩壁提交：" + (preview?.RockBuildCount ?? 0) + " · 背景提交：" + (preview?.BackgroundBuildCount ?? 0));
            GUILayout.Label("源提交 / 已安装 / 已绘制：\n" + preview?.ReceivedSourceCommit + " / " + preview?.InstalledSourceCommit + " / " + preview?.PresentedSourceCommit);
            GUILayout.Label("上次变化区块：" + preview?.LastChangedChunkCount + " · 刷新批次：" + preview?.RefreshBatchCount);
            var p = boot.Flyer.transform.position; GUILayout.Label($"角色格子：{p.x:F1}, {-p.y:F1}");
            if (boot.Workshop != null) GUILayout.Label("喷气燃料：" + boot.Workshop.Fuel.ToString("0.0") + " 秒");
            if (GUILayout.Button("重建表现（保留地图）")) boot.RequestStyleRefresh();
            GUILayout.Space(8); GUILayout.Label("F1 收起 · Tab 行走/观察 · F 回入口 · R 重置地图\n文本编辑期间屏蔽角色快捷键。面板内滚轮仅滚动 UI。");
        }
    }
}
