using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>Cave Wall Tuner 编辑窗口；样式与初始地图使用独立草稿，画面由正式 TerrainPreview 渲染。</summary>
    public sealed class TerrainStylePreviewWindow : EditorWindow
    {
        private const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        private TerrainMapAsset map;
        private CaveTerrainStyle style;
        private ScriptableObject inspected;
        private readonly TerrainStyleDrafts drafts = new TerrainStyleDrafts();
        private readonly TerrainMapDraft mapDraft = new TerrainMapDraft();
        private readonly TerrainStylePreviewCanvas preview = new TerrainStylePreviewCanvas();
        private readonly TerrainStylePreviewStage stage = new TerrainStylePreviewStage();
        private TerrainBlueprint baselineBlueprint;
        private BackgroundBakeDescriptor backgroundReference;
        private string observed, status = "选择样板后生成预览。";
        private double due = double.PositiveInfinity, nextRepaint, fpsStart;
        private int frames, canvasFps, renderMilliseconds;
        private float panelWidth = 440;
        private byte fillMaterial = 1;
        private Vector2 scroll;
        private bool resizing, playingLastTick;
        private Texture Image => stage.Image;

        public static void Open()
        {
            var window = GetWindow<TerrainStylePreviewWindow>("Cave Wall Tuner");
            window.titleContent = new GUIContent("Cave Wall Tuner"); window.minSize = new Vector2(800, 440);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Cave Wall Tuner"); wantsMouseMove = true; preview.Fit();
            map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(Root + "ReferenceChamber.asset");
            OpenMapSnapshot(); style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset");
            inspected = style; EditorApplication.update += Tick; Invalidate();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick; preview.Stop(mapDraft.EndStroke); resizing = false;
            stage.Dispose(); drafts.Dispose();
        }

        private void OnLostFocus() { preview.Stop(mapDraft.EndStroke); resizing = false; }

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("请先退出 Play；编辑态预览不会在运行场景中改写资产。", MessageType.Warning); return; }
            if (Event.current.type == EventType.Repaint)
            {
                frames++; double now = EditorApplication.timeSinceStartup;
                if (fpsStart == 0) fpsStart = now;
                if (now - fpsStart >= 1)
                { canvasFps = Mathf.RoundToInt((float)(frames / (now - fpsStart))); frames = 0; fpsStart = now; }
            }
            HandleSplitter(); panelWidth = Mathf.Clamp(panelWidth, 390, position.width - 320);
            var panel = new Rect(0, 0, panelWidth, position.height);
            var canvas = new Rect(panelWidth + 4, 0, position.width - panelWidth - 4, position.height);
            preview.Input(canvas, Image, (x, y) => mapDraft.Paint(x, y,
                preview.ActiveTool == TerrainStylePreviewTool.Fill, fillMaterial), InvalidateMap,
                mapDraft.BeginStroke, mapDraft.EndStroke, mapDraft.Undo, mapDraft.Redo);
            EditorGUI.DrawRect(new Rect(panelWidth, 0, 4, position.height), new Color(.25f, .25f, .25f));
            EditorGUIUtility.AddCursorRect(new Rect(panelWidth - 3, 0, 10, position.height), MouseCursor.ResizeHorizontal);
            GUILayout.BeginArea(panel);
            scroll = EditorGUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            float labelWidth = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 120;
            GUILayout.BeginVertical(GUILayout.Width(panel.width - 22)); DrawControls(); GUILayout.EndVertical();
            EditorGUIUtility.labelWidth = labelWidth; EditorGUILayout.EndScrollView(); GUILayout.EndArea();
            preview.Draw(canvas, Image, canvasFps, renderMilliseconds,
                !double.IsPositiveInfinity(due) || (Image != null && !stage.Ready),
                !double.IsPositiveInfinity(due) ? "配置变化：等待重建（不属于填拆增量）" : stage.Progress);
        }

        private void DrawControls()
        {
            EditorGUILayout.HelpBox("预览只创建空白离屏宿主和当前地图，复用运行时 TerrainPreview、AnyRuleD 与 Cave shader；不加载游戏场景、角色或游戏会话。", MessageType.Info);
            var selectedMap = (TerrainMapAsset)EditorGUILayout.ObjectField("固定地图", map, typeof(TerrainMapAsset), false);
            if (selectedMap != map)
            {
                if (mapDraft.HasChanges) status = "先应用或取消当前地图草稿，再切换地图。";
                else { map = selectedMap; OpenMapSnapshot(); stage.Dispose(); observed = null; Invalidate(); }
            }
            var selectedStyle = (CaveTerrainStyle)EditorGUILayout.ObjectField("岩壁样式", style, typeof(CaveTerrainStyle), false);
            if (selectedStyle != style)
            {
                if (drafts.HasChanges) status = "先 Apply 或 Cancel 当前草稿，再切换样式。";
                else { drafts.Clear(); style = selectedStyle; inspected = style; Invalidate(); }
            }
            float requestedZoom = EditorGUILayout.Slider("显示倍率", preview.Zoom, .5f, 8);
            if (!Mathf.Approximately(requestedZoom, preview.Zoom)) preview.SetZoom(requestedZoom, Vector2.zero, false);
            if (GUILayout.Button("适配画布")) { preview.Fit(); Repaint(); }

            EditorGUILayout.Space(); EditorGUILayout.LabelField("初始地图绘制", EditorStyles.boldLabel);
            preview.ActiveTool = (TerrainStylePreviewTool)GUILayout.Toolbar((int)preview.ActiveTool, new[] { "平移", "拆", "填" });
            if (preview.ActiveTool == TerrainStylePreviewTool.Fill)
                fillMaterial = (byte)(EditorGUILayout.Popup("填入材料", fillMaterial - 1,
                    new[] { "壤土", "板岩", "玄武岩", "铜矿", "铁矿", "金矿", "苔岩" }) + 1);
            preview.ShowGrid = EditorGUILayout.Toggle("显示地形网格", preview.ShowGrid);
            EditorGUILayout.LabelField(preview.ActiveTool == TerrainStylePreviewTool.Pan
                ? "左键拖拽平移画布；选择“拆”或“填”后才绘制地形格。"
                : "左键单击或拖动连续绘制地形格；边界、保护格和基岩不可修改。", EditorStyles.wordWrappedMiniLabel);
            if (!mapDraft.IsReady && map != null) EditorGUILayout.HelpBox(mapDraft.Error ?? "地图草稿不可用。", MessageType.Warning);
            EditorGUILayout.LabelField("地图草稿改动：" + mapDraft.ChangedCells + " 格", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!mapDraft.CanUndo)) if (GUILayout.Button("撤销")) { mapDraft.Undo(); InvalidateMap(); }
            using (new EditorGUI.DisabledScope(!mapDraft.CanRedo)) if (GUILayout.Button("重做")) { mapDraft.Redo(); InvalidateMap(); }
            using (new EditorGUI.DisabledScope(!mapDraft.HasChanges)) if (GUILayout.Button("取消草稿"))
            { mapDraft.Cancel(); InvalidateMap(); status = "地图草稿已撤销，原资产未更改。"; }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(!mapDraft.HasChanges))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("应用地图")) ApplyMap();
                if (GUILayout.Button("取消地图")) { OpenMapSnapshot(); stage.Dispose(); RebuildSoon(); }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(); inspected = drafts.ChooseAsset(style, inspected);
            EditorGUILayout.LabelField("当前编辑", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("原资产", inspected, typeof(ScriptableObject), false);
            if (inspected != null) drafts.DrawInspector(inspected, Invalidate);
            EditorGUILayout.LabelField("刷新路径：" + stage.RefreshPath, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(!drafts.HasChanges))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply")) ApplyDrafts(); if (GUILayout.Button("Cancel")) CancelDrafts();
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("重建运行时预览")) Invalidate();
        }

        private void ApplyDrafts()
        {
            try { int count = drafts.Apply(); drafts.Clear(); Invalidate(); status = count + " 个资产已应用。"; }
            catch (Exception error) { status = "Apply 失败：" + error.Message; }
        }

        private void CancelDrafts()
        { drafts.Clear(); Invalidate(); status = "草稿已丢弃，原资产未更改。"; }

        private void ApplyMap()
        {
            try
            {
                int count = mapDraft.ChangedCells; mapDraft.Apply(); OpenMapSnapshot(); stage.Dispose(); observed = null;
                RebuildNow(); status = count + " 个初始地图格已应用。";
            }
            catch (Exception error) { status = "应用地图失败：" + error.Message; }
        }

        private void HandleSplitter()
        {
            var input = Event.current;
            if (input.type == EventType.MouseDown && input.button == 0 && Mathf.Abs(input.mousePosition.x - panelWidth) <= 5)
            { resizing = true; input.Use(); }
            else if (input.type == EventType.MouseDrag && resizing)
            { panelWidth = Mathf.Clamp(input.mousePosition.x, 390, position.width - 320); input.Use(); Repaint(); }
            else if (input.type == EventType.MouseUp && resizing) { resizing = false; input.Use(); }
        }

        private void Invalidate() => ScheduleRebuild(.4);

        private void InvalidateMap()
        {
            try
            {
                if (stage.Source != null)
                {
                    stage.Source.ApplyChanges(mapDraft.DrainChangedCells());
                    // 与 AnyRuleD 工作台相同：输入提交后立即泵送，不等下一次 Editor update。
                    stage.Tick();
                }
                status = stage.RefreshPath + " · " + stage.Progress;
            }
            catch (Exception error) { status = "预览刷新失败：" + error.Message; }
            Repaint();
        }

        private void ScheduleRebuild(double delay)
        { due = EditorApplication.timeSinceStartup + delay; status = "预览配置已变化，等待重建运行时渲染…"; Repaint(); }

        private void RebuildSoon() => ScheduleRebuild(.05);

        private void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { if (Image != null) stage.Dispose(); playingLastTick = true; return; }
            double now = EditorApplication.timeSinceStartup;
            if (playingLastTick) { playingLastTick = false; ScheduleRebuild(.05); }
            if (now >= nextRepaint) { nextRepaint = now + 1.0 / 30; Repaint(); }
            try
            {
                CheckExternalChanges();
                if (now >= due) RebuildNow();
                if (Image != null)
                {
                    bool drawn = stage.Tick();
                    if (stage.Error != null) status = "运行时预览失败：" + stage.Error.Message;
                    if (drawn)
                    {
                        renderMilliseconds = stage.LastCameraMilliseconds;
                        if (stage.Error != null) status = "运行时预览失败：" + stage.Error.Message;
                        else if (stage.Ready) status = "运行时渲染链已就绪。";
                        Repaint();
                    }
                }
            }
            catch (Exception error) { status = "运行时预览失败：" + error.Message; due = double.PositiveInfinity; Repaint(); }
        }

        private void CheckExternalChanges()
        {
            string current = style == null || map == null ? "none" :
                style.VisualIdentity + "|" + AssetDatabase.GetAssetPath(map) + "|" +
                (map.InitialCells != null ? AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(map.InitialCells)).ToString() : "none") +
                "|" + map.Settings?.Seed;
            if (current == observed) return;
            bool wasObserved = observed != null; observed = current;
            if (!wasObserved) return;
            if (mapDraft.HasChanges || drafts.HasChanges)
            { status = "原资产在窗口外变化；请先取消草稿并重新打开预览。"; return; }
            drafts.Clear(); OpenMapSnapshot(); stage.Dispose(); ScheduleRebuild(.1);
        }

        private void RebuildNow()
        {
            due = double.PositiveInfinity;
            if (map == null || style == null || baselineBlueprint == null)
            { stage.Dispose(); status = "选择固定地图和岩壁样式以显示运行时预览。"; Repaint(); return; }
            var background = drafts.Draft(drafts.Draft(style).Background);
            if (background != null && background.ContourStatic &&
                background.ContentHash != BackgroundBakeDescriptor.StyleContentHash)
                throw new InvalidOperationException("背景样式内容身份不匹配。");
            byte[] materials = mapDraft.IsReady ? mapDraft.CopyMaterials() : baselineBlueprint.CopyMaterials();
            byte[] shapes = mapDraft.IsReady ? mapDraft.CopyShapes() : baselineBlueprint.CopyShapes();
            stage.Open(map, baselineBlueprint, style, drafts, materials, shapes, backgroundReference);
            status = "正在加载 AnyRuleD 与洞穴材质分页…"; Repaint();
        }

        private void OpenMapSnapshot()
        {
            mapDraft.Open(map); baselineBlueprint = null; backgroundReference = null;
            if (map == null || !mapDraft.IsReady) return;
            baselineBlueprint = map.ReadBlueprint();
            backgroundReference = new BackgroundBakeDescriptor(Guid.NewGuid().ToString("N"),
                baselineBlueprint.Settings.Seed, baselineBlueprint.CopyMaterials(), baselineBlueprint.CopyShapes());
        }
    }
}
