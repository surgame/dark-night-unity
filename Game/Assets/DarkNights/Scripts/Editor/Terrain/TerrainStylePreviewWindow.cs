using System;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>Cave Wall Tuner 编辑窗口；草稿参数异步烘焙完整地图，只在 Apply 时提交原资产。</summary>
    public sealed class TerrainStylePreviewWindow : EditorWindow
    {
        private const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        private TerrainMapAsset map;
        private CaveTerrainStyle style;
        private ScriptableObject inspected;
        private readonly TerrainStyleDrafts drafts = new TerrainStyleDrafts();
        private readonly TerrainMapDraft mapDraft = new TerrainMapDraft();
        private readonly TerrainStylePreviewCanvas preview = new TerrainStylePreviewCanvas();
        private Texture2D image;
        private Task<byte[]> job;
        private CancellationTokenSource cancellation;
        private string observed, status = "选择样板后生成预览。";
        private double due, nextRepaint, fpsStart, bakeStart;
        private int frames, canvasFps, bakeMilliseconds;
        private float panelWidth = 440;
        private byte fillMaterial = 1;
        private Vector2 scroll;
        private bool resizing;

        public static void Open()
        {
            var window = GetWindow<TerrainStylePreviewWindow>("Cave Wall Tuner");
            window.titleContent = new GUIContent("Cave Wall Tuner");
            window.minSize = new Vector2(800, 440);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Cave Wall Tuner");
            wantsMouseMove = true;
            preview.Fit();
            map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(Root + "ReferenceChamber.asset");
            mapDraft.Open(map);
            style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset");
            inspected = style;
            EditorApplication.update += Tick;
            Invalidate();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            preview.Stop(); resizing = false;
            cancellation?.Cancel();
            cancellation?.Dispose(); cancellation = null;
            drafts.Dispose();
            if (image != null) DestroyImmediate(image);
        }

        private void OnLostFocus()
        {
            preview.Stop(); resizing = false;
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("请先退出 Play；编辑态预览不会在运行场景中改写资产。", MessageType.Warning); return; }
            if (Event.current.type == EventType.Repaint)
            {
                frames++;
                double now = EditorApplication.timeSinceStartup;
                if (fpsStart == 0) fpsStart = now;
                if (now - fpsStart >= 1)
                { canvasFps = Mathf.RoundToInt((float)(frames / (now - fpsStart))); frames = 0; fpsStart = now; }
            }
            HandleSplitter();
            panelWidth = Mathf.Clamp(panelWidth, 390, position.width - 320);
            var panel = new Rect(0, 0, panelWidth, position.height);
            var canvas = new Rect(panelWidth + 4, 0, position.width - panelWidth - 4, position.height);
            preview.Input(canvas, image, (x, y) => mapDraft.Paint(x, y,
                preview.ActiveTool == TerrainStylePreviewTool.Fill, fillMaterial), Invalidate);
            EditorGUI.DrawRect(new Rect(panelWidth, 0, 4, position.height), new Color(.25f, .25f, .25f));
            EditorGUIUtility.AddCursorRect(new Rect(panelWidth - 3, 0, 10, position.height), MouseCursor.ResizeHorizontal);
            GUILayout.BeginArea(panel);
            scroll = EditorGUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 120;
            GUILayout.BeginVertical(GUILayout.Width(panel.width - 22));
            DrawControls();
            GUILayout.EndVertical();
            EditorGUIUtility.labelWidth = labelWidth;
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            preview.Draw(canvas, image, canvasFps, bakeMilliseconds,
                job != null || !double.IsPositiveInfinity(due));
        }

        private void DrawControls()
        {
            EditorGUILayout.HelpBox("参数和地图格子先进入草稿；各自 Apply 后才写盘。预览不含动态灯光。", MessageType.Info);
            var selectedMap = (TerrainMapAsset)EditorGUILayout.ObjectField("固定地图", map, typeof(TerrainMapAsset), false);
            if (selectedMap != map)
            {
                if (mapDraft.HasChanges) status = "先应用或取消当前地图草稿，再切换地图。";
                else
                {
                    map = selectedMap; mapDraft.Open(map);
                    if (image != null) { DestroyImmediate(image); image = null; }
                    Invalidate();
                }
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("初始地图绘制", EditorStyles.boldLabel);
            preview.ActiveTool = (TerrainStylePreviewTool)GUILayout.Toolbar((int)preview.ActiveTool,
                new[] { "平移", "拆", "填" });
            if (preview.ActiveTool == TerrainStylePreviewTool.Fill)
                fillMaterial = (byte)(EditorGUILayout.Popup("填入材料", fillMaterial - 1,
                    new[] { "壤土", "板岩", "玄武岩", "铜矿", "铁矿", "金矿", "苔岩" }) + 1);
            preview.ShowGrid = EditorGUILayout.Toggle("显示地形网格", preview.ShowGrid);
            EditorGUILayout.LabelField(preview.ActiveTool == TerrainStylePreviewTool.Pan
                ? "左键拖拽平移画布；选择“拆”或“填”后才绘制地形格。"
                : "左键单击或拖动连续绘制地形格；边界、保护格和基岩不可修改。",
                EditorStyles.wordWrappedMiniLabel);
            if (!mapDraft.IsReady && map != null)
                EditorGUILayout.HelpBox(mapDraft.Error ?? "地图草稿不可用。", MessageType.Warning);
            EditorGUILayout.LabelField("地图草稿改动：" + mapDraft.ChangedCells + " 格", EditorStyles.miniLabel);
            using (new EditorGUI.DisabledScope(!mapDraft.HasChanges))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("应用地图")) ApplyMap();
                if (GUILayout.Button("取消地图")) { mapDraft.Open(map); Invalidate(); }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            inspected = drafts.ChooseAsset(style, inspected);
            EditorGUILayout.LabelField("当前编辑", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("原资产", inspected, typeof(ScriptableObject), false);
            if (inspected != null)
                drafts.DrawInspector(inspected, Invalidate);
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(!drafts.HasChanges))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply")) ApplyDrafts();
                if (GUILayout.Button("Cancel")) CancelDrafts();
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("重新烘焙")) Invalidate();
        }

        private void ApplyDrafts()
        {
            try
            {
                int count = drafts.Apply();
                drafts.Clear(); Invalidate();
                status = count + " 个资产已应用。";
            }
            catch (Exception error) { status = "Apply 失败：" + error.Message; }
        }

        private void CancelDrafts()
        {
            drafts.Clear(); Invalidate();
            status = "草稿已丢弃，原资产未更改。";
        }

        private void ApplyMap()
        {
            try
            {
                int count = mapDraft.ChangedCells;
                mapDraft.Apply(); Invalidate();
                status = count + " 个初始地图格已应用。";
            }
            catch (Exception error) { status = "应用地图失败：" + error.Message; }
        }

        private void HandleSplitter()
        {
            var input = Event.current;
            if (input.type == EventType.MouseDown && input.button == 0 &&
                Mathf.Abs(input.mousePosition.x - panelWidth) <= 5)
            { resizing = true; input.Use(); }
            else if (input.type == EventType.MouseDrag && resizing)
            { panelWidth = Mathf.Clamp(input.mousePosition.x, 390, position.width - 320); input.Use(); Repaint(); }
            else if (input.type == EventType.MouseUp && resizing)
            { resizing = false; input.Use(); }
        }

        private void Invalidate()
        {
            due = EditorApplication.timeSinceStartup + .4;
            cancellation?.Cancel();
            status = "参数已变化，等待重烘焙…";
            Repaint();
        }

        private void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { cancellation?.Cancel(); return; }
            double now = EditorApplication.timeSinceStartup;
            if (now >= nextRepaint) { nextRepaint = now + 1.0 / 30; Repaint(); }
            try
            {
                string current = style == null || map == null ? "none" :
                    style.VisualIdentity + "|" + AssetDatabase.GetAssetPath(map) + "|" +
                    (map.InitialCells != null ? AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(map.InitialCells)).ToString() : "none") +
                    "|" + map.Settings?.Seed;
                if (current != observed) { observed = current; Invalidate(); }
                if (job != null)
                {
                    if (!job.IsCompleted) return;
                    var done = job; job = null;
                    if (!done.IsCanceled && !done.IsFaulted && cancellation != null && !cancellation.IsCancellationRequested)
                    {
                        Present(done.Result);
                        bakeMilliseconds = Mathf.RoundToInt((float)((now - bakeStart) * 1000));
                        status = "预览已更新（无动态灯光）。"; Repaint();
                    }
                    else if (done.IsFaulted && !(done.Exception.GetBaseException() is OperationCanceledException))
                    { status = "烘焙失败：" + done.Exception.GetBaseException().Message; Repaint(); }
                }
                if (EditorApplication.timeSinceStartup < due || style == null || map == null) return;
                due = double.PositiveInfinity;
                var working = drafts.Draft(style);
                var background = drafts.Draft(working.Background);
                if (!working.ProceduralRock) { status = "仅支持程序岩壁样式；请选择 StrataCave/Style.asset。"; Repaint(); return; }
                if (background != null && background.ContourStatic &&
                    background.ContentHash != BackgroundBakeDescriptor.StyleContentHash)
                    throw new InvalidOperationException("背景样式内容身份不匹配。");
                cancellation?.Dispose(); cancellation = new CancellationTokenSource();
                var token = cancellation.Token;
                var blueprint = map.ReadBlueprint();
                var materials = mapDraft.IsReady ? mapDraft.CopyMaterials() : blueprint.CopyMaterials();
                var shapes = mapDraft.IsReady ? mapDraft.CopyShapes() : blueprint.CopyShapes();
                var referenceMaterials = mapDraft.IsReady ? mapDraft.CopyOriginalMaterials() : blueprint.CopyMaterials();
                var referenceShapes = mapDraft.IsReady ? mapDraft.CopyOriginalShapes() : blueprint.CopyShapes();
                var foreground = drafts.Capture(working.Modifiers); var outline = working.CaptureOutline();
                var generator = background != null && background.Generator != null
                    ? drafts.Draft(background.Generator).Capture() : new DarkNights.Core.Logic.Terrain.ContourBackgroundGenerator();
                var modifiers = background != null ? new[] {
                    drafts.Capture(background.NearModifiers), drafts.Capture(background.MiddleModifiers),
                    drafts.Capture(background.DeepModifiers) } : new[] {
                    DarkNights.Core.Logic.Terrain.CaveModifierStack.Empty, DarkNights.Core.Logic.Terrain.CaveModifierStack.Empty,
                    DarkNights.Core.Logic.Terrain.CaveModifierStack.Empty };
                var visible = background != null && background.ContourStatic
                    ? new[] { background.Near, background.Middle, background.Deep } : new bool[3];
                int softness = background != null ? background.MiddleSoftness : 0;
                int stone = working.StoneSize; string seed = blueprint.Settings.Seed;
                bakeStart = now;
                job = Task.Run(() => TerrainStylePreviewBaker.BakeFull(materials, shapes, seed, stone,
                    outline, foreground, generator, modifiers, visible, softness, token.ThrowIfCancellationRequested,
                    referenceMaterials, referenceShapes), token);
                status = "正在烘焙完整初始地图…"; Repaint();
            }
            catch (Exception error)
            { status = "预览失败：" + error.Message; due = double.PositiveInfinity; Repaint(); }
        }

        private void Present(byte[] rgba)
        {
            if (image == null) image = new Texture2D(TerrainStylePreviewBaker.WorldWidth, TerrainStylePreviewBaker.WorldHeight,
                TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[image.width * image.height];
            for (int y = 0; y < image.height; y++) for (int x = 0; x < image.width; x++)
            {
                int p = (y * image.width + x) * 4;
                pixels[(image.height - 1 - y) * image.width + x] = new Color32(rgba[p], rgba[p + 1], rgba[p + 2], 255);
            }
            image.SetPixels32(pixels); image.Apply(false, false);
        }
    }
}
