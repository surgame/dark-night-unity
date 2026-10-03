using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>地图、显示和诊断页；地图参数来自正式生成配置，运行时拆填只影响当前预览。</summary>
    public sealed class TerrainWorkbenchPages
    {
        private static readonly string[] Materials = { "壤土", "板岩", "玄武岩", "铜矿", "铁矿", "金矿", "苔岩" };
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
            GUI.enabled = enabled;
            GUILayout.Label("笔刷草稿：" + (edits?.ChangedCells ?? 0) + " 格 · 仅影响当前地图，重新生成或退出运行后丢弃");
            GUILayout.Space(8); GUILayout.Label("地图来源");
            GUILayout.Label("正式星球生成；旧塌方阻断已移除，入口步道由正式地形步骤配置。已有存档不会自动重绘。");
            if (picker.Pick("岩壁样式", boot.CaveStyle, out CaveTerrainStyle style) && style != boot.CaveStyle) panel.SwitchStyle(style);
            var planets = boot.MapAssemblySource.SharedConfigs.OfType<ExpeditionFlowConfig>().Single().FreezePlanets().Where(p => p.Enabled).ToArray();
            int selected = Math.Max(0, Array.FindIndex(planets, p => p.Id == boot.PlanetId));
            int next = GUILayout.SelectionGrid(selected, planets.Select(p => p.DisplayName).ToArray(), 2);
            if (next != selected) { boot.PlanetId = planets[next].Id; boot.RequestRegenerate(); }
            GUILayout.Label("完整正式星球预览（含天空、泊位、入口）；同一星球和种子逐格一致。");
            if (TerrainWorkbenchAssets.SaveGeneration != null && GUILayout.Button("保存生成参数到正式配置"))
                panel.Run(() => { TerrainWorkbenchAssets.SaveGeneration(boot.MapAssemblySource, boot.MapConfigBaseline, boot.Settings);
                    boot.AcceptMapConfigSave(); panel.SetMessage("生成参数已保存；下次正式生成使用新参数，已有地图保持当前格子。"); });
            GUILayout.Label("预览／复现种子"); GUI.SetNextControlName("TerrainSeed");
            boot.Settings.Seed = GUILayout.TextField(boot.Settings.Seed ?? "", 80);
            boot.Settings.Amplitude = TerrainFieldControls.Slider("地表起伏", (float)boot.Settings.Amplitude, .3f, 1.6f);
            boot.LiveRegenerate = GUILayout.Toggle(boot.LiveRegenerate, "自动重建生成参数");
            if (GUILayout.Button("新预览种子（重置地图）")) boot.NewSeed();
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
            var workshop = panel.Bootstrap.Workshop;
            if (workshop != null)
            {
                GUILayout.Space(8); GUILayout.Label("角色跳跃");
                workshop.JumpStrategy = (HeroJumpStrategy)GUILayout.SelectionGrid((int)workshop.JumpStrategy,
                    new[] { "固定跳跃", "按住控制跳高" }, 2);
                workshop.JetpackEnabled = GUILayout.Toggle(workshop.JetpackEnabled, "启用喷气背包");
            }
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
