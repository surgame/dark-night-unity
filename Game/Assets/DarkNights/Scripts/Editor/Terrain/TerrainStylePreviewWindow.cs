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
        private Texture2D image;
        private Task<byte[]> job;
        private CancellationTokenSource cancellation;
        private string observed, status = "选择样板后生成预览。";
        private double due, nextRepaint, fpsStart, bakeStart;
        private int frames, canvasFps, bakeMilliseconds;
        private float panelWidth = 440;
        private float zoom = 1;
        private Vector2 scroll, canvasOffset;
        private bool panning, resizing;

        public static void Open()
        {
            var window = GetWindow<TerrainStylePreviewWindow>("Cave Wall Tuner");
            window.titleContent = new GUIContent("Cave Wall Tuner");
            window.minSize = new Vector2(800, 440);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Cave Wall Tuner");
            zoom = 1; canvasOffset = Vector2.zero;
            map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(Root + "ReferenceChamber.asset");
            style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset");
            inspected = style;
            EditorApplication.update += Tick;
            Invalidate();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            panning = false; resizing = false;
            cancellation?.Cancel();
            cancellation?.Dispose(); cancellation = null;
            drafts.Dispose();
            if (image != null) DestroyImmediate(image);
        }

        private void OnLostFocus()
        {
            panning = false; resizing = false;
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
            DrawCanvas(canvas);
        }

        private void DrawControls()
        {
            EditorGUILayout.HelpBox("参数先进入草稿；Apply 写回共享资产，Cancel 丢弃。预览不含动态灯光。", MessageType.Info);
            var selectedMap = (TerrainMapAsset)EditorGUILayout.ObjectField("固定地图", map, typeof(TerrainMapAsset), false);
            if (selectedMap != map) { map = selectedMap; Invalidate(); }
            var selectedStyle = (CaveTerrainStyle)EditorGUILayout.ObjectField("岩壁样式", style, typeof(CaveTerrainStyle), false);
            if (selectedStyle != style)
            {
                if (drafts.HasChanges) status = "先 Apply 或 Cancel 当前草稿，再切换样式。";
                else { drafts.Clear(); style = selectedStyle; inspected = style; Invalidate(); }
            }
            float requestedZoom = EditorGUILayout.Slider("显示倍率", zoom, .5f, 8);
            if (!Mathf.Approximately(requestedZoom, zoom)) SetZoom(requestedZoom, Vector2.zero, false);
            if (GUILayout.Button("适配画布")) { zoom = 1; canvasOffset = Vector2.zero; Repaint(); }

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

        private void DrawCanvas(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(.12f, .13f, .15f));
            HandleCanvasInput(canvas);
            GUI.BeginGroup(canvas);
            if (image != null)
            {
                var center = new Vector2(canvas.width * .5f, canvas.height * .5f) + canvasOffset;
                float scale = Mathf.Min(canvas.width / image.width, canvas.height / image.height) * zoom;
                var bounds = new Rect(center.x - image.width * scale * .5f,
                    center.y - image.height * scale * .5f, image.width * scale, image.height * scale);
                GUI.DrawTexture(bounds, image, ScaleMode.StretchToFill, false);
            }
            else GUI.Label(new Rect(16, 38, canvas.width - 32, 40), "正在生成完整画面…", EditorStyles.whiteLabel);
            GUI.Box(new Rect(8, 8, Mathf.Min(canvas.width - 16, 310), 42),
                "画布刷新 " + canvasFps + " FPS · 全图烘焙 " + bakeMilliseconds + " ms\n中键拖拽平移 · 滚轮围绕光标缩放");
            if (job != null || !double.IsPositiveInfinity(due)) GUI.Label(new Rect(8, canvas.height - 30, canvas.width - 16, 22), "完整画面更新中…", EditorStyles.whiteLabel);
            GUI.EndGroup();
        }

        private void HandleCanvasInput(Rect canvas)
        {
            var input = Event.current;
            if (input.type == EventType.MouseDown && input.button == 2 && canvas.Contains(input.mousePosition))
            { panning = true; input.Use(); }
            else if (input.type == EventType.MouseDrag && input.button == 2 && panning)
            { canvasOffset += input.delta; input.Use(); Repaint(); }
            else if (input.type == EventType.MouseUp && input.button == 2 && panning)
            {
                panning = false;
                input.Use();
            }
            else if (input.type == EventType.ScrollWheel && canvas.Contains(input.mousePosition))
            {
                var pivot = input.mousePosition - canvas.center;
                SetZoom(zoom * Mathf.Pow(1.1f, -input.delta.y), pivot, true);
                input.Use();
            }
        }

        private void SetZoom(float requested, Vector2 pivot, bool anchored)
        {
            float previous = zoom;
            zoom = Mathf.Clamp(requested, .5f, 8);
            if (anchored && !Mathf.Approximately(previous, zoom))
                canvasOffset = pivot + (canvasOffset - pivot) * (zoom / previous);
            Repaint();
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
                var materials = blueprint.CopyMaterials(); var shapes = blueprint.CopyShapes();
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
                    outline, foreground, generator, modifiers, visible, softness, token.ThrowIfCancellationRequested), token);
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
