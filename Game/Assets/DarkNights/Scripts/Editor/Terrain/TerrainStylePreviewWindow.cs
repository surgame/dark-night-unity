using System;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>编辑态岩壁调参窗口；仅捕获现有资产的只读副本异步生成预览，不启动 Play、保存场景或改动游戏接口。</summary>
    public sealed class TerrainStylePreviewWindow : EditorWindow
    {
        private const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        private TerrainMapAsset map;
        private CaveTerrainStyle style;
        private UnityEngine.Object inspected;
        private UnityEditor.Editor inspector;
        private Texture2D image;
        private Task<byte[]> job;
        private CancellationTokenSource cancellation;
        private string observed, status = "选择样板后生成预览。";
        private double due, nextRepaint, fpsStart, bakeStart;
        private int frames, canvasFps, bakeMilliseconds, jobLeft, jobTop, imageLeft, imageTop;
        private int left = 316, top = 284;
        private float zoom = 1;
        private Vector2 scroll, canvasOffset;
        private bool panning;

        public static void Open()
        {
            var window = GetWindow<TerrainStylePreviewWindow>("岩壁实时预览");
            window.minSize = new Vector2(780, 440);
        }

        private void OnEnable()
        {
            map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(Root + "ReferenceChamber.asset");
            style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset");
            inspected = style;
            EditorApplication.update += Tick;
            Invalidate();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            panning = false;
            cancellation?.Cancel();
            cancellation?.Dispose(); cancellation = null;
            if (inspector != null) DestroyImmediate(inspector);
            if (image != null) DestroyImmediate(image);
        }

        private void OnLostFocus()
        {
            if (!panning) return;
            panning = false;
            Recenter();
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
            float panelWidth = Mathf.Min(390, Mathf.Max(340, position.width * .42f));
            var panel = new Rect(0, 0, panelWidth, position.height);
            var canvas = new Rect(panelWidth + 1, 0, position.width - panelWidth - 1, position.height);
            EditorGUI.DrawRect(new Rect(panelWidth, 0, 1, position.height), new Color(.13f, .13f, .13f));
            GUILayout.BeginArea(panel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawControls();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawCanvas(canvas);
        }

        private void DrawControls()
        {
            EditorGUILayout.HelpBox("直接编辑原有共享资产；停止调整约 0.4 秒后自动烘焙。不进入 Play、不写场景。预览为初始地形的无动态灯光材质合成，底墙简化；最终画面仍以 Play 为准。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            map = (TerrainMapAsset)EditorGUILayout.ObjectField("固定地图", map, typeof(TerrainMapAsset), false);
            style = (CaveTerrainStyle)EditorGUILayout.ObjectField("岩壁样式", style, typeof(CaveTerrainStyle), false);
            left = EditorGUILayout.IntSlider("预览左边（原生 px）", left, 0, 2560 - TerrainStylePreviewBaker.Width);
            top = EditorGUILayout.IntSlider("预览上边（原生 px）", top, 0, 1536 - TerrainStylePreviewBaker.Height);
            if (EditorGUI.EndChangeCheck()) Invalidate();
            float requestedZoom = EditorGUILayout.Slider("显示倍率", zoom, .5f, 8);
            if (!Mathf.Approximately(requestedZoom, zoom)) SetZoom(requestedZoom, Vector2.zero, false);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("岩壁/外轮廓")) Inspect(style);
            if (GUILayout.Button("三层背景")) Inspect(style != null ? style.Background : null);
            if (GUILayout.Button("点缀生成器")) Inspect(style != null && style.Background != null ? style.Background.Generator : null);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (style != null) AssetButtons("前景", style.Modifiers);
            if (style != null && style.Background != null)
            {
                AssetButtons("近层", style.Background.NearModifiers);
                AssetButtons("中层", style.Background.MiddleModifiers);
                AssetButtons("深层", style.Background.DeepModifiers);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("当前编辑", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var target = EditorGUILayout.ObjectField(inspected, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck()) Inspect(target);
            if (inspected != null)
            {
                UnityEditor.Editor.CreateCachedEditor(inspected, null, ref inspector);
                if (inspector != null)
                {
                    EditorGUI.BeginChangeCheck();
                    inspector.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck()) Invalidate();
                }
            }
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("重新烘焙")) Invalidate();
        }

        private void DrawCanvas(Rect canvas)
        {
            EditorGUI.DrawRect(canvas, new Color(.12f, .13f, .15f));
            HandleCanvasInput(canvas);
            GUI.BeginGroup(canvas);
            if (image != null)
            {
                var center = new Vector2(canvas.width * .5f, canvas.height * .5f) + canvasOffset;
                var bounds = new Rect(center.x - image.width * zoom * .5f,
                    center.y - image.height * zoom * .5f, image.width * zoom, image.height * zoom);
                GUI.DrawTexture(bounds, image, ScaleMode.StretchToFill, false);
            }
            else GUI.Label(new Rect(16, 38, canvas.width - 32, 40), "等待岩壁预览…", EditorStyles.whiteLabel);
            GUI.Box(new Rect(8, 8, Mathf.Min(canvas.width - 16, 310), 42),
                "画布刷新 " + canvasFps + " FPS · 上次烘焙 " + bakeMilliseconds + " ms\n中键拖拽平移 · 滚轮围绕光标缩放");
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
                Recenter();
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

        private void Recenter()
        {
            if (image == null) return;
            int nextLeft = Mathf.Clamp(Mathf.RoundToInt(imageLeft - canvasOffset.x / zoom), 0,
                2560 - TerrainStylePreviewBaker.Width);
            int nextTop = Mathf.Clamp(Mathf.RoundToInt(imageTop - canvasOffset.y / zoom), 0,
                1536 - TerrainStylePreviewBaker.Height);
            if (left != nextLeft || top != nextTop)
            { left = nextLeft; top = nextTop; Invalidate(); }
        }

        private void AssetButtons(string name, CaveModifierAsset[] assets)
        {
            if (assets == null) return;
            for (int i = 0; i < assets.Length; i++)
                if (assets[i] != null && GUILayout.Button(name + ": " + assets[i].name)) Inspect(assets[i]);
        }

        private void Inspect(UnityEngine.Object asset)
        {
            if (inspected == asset) return;
            inspected = asset;
            if (inspector != null) DestroyImmediate(inspector);
            inspector = null;
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
                    "|" + map.Settings?.Seed + "|" + left + "|" + top;
                if (current != observed) { observed = current; Invalidate(); }
                if (job != null)
                {
                    if (!job.IsCompleted) return;
                    if (panning) return;
                    var done = job; job = null;
                    if (!done.IsCanceled && !done.IsFaulted && cancellation != null && !cancellation.IsCancellationRequested)
                    {
                        bool hadImage = image != null;
                        int imageLeftBefore = imageLeft, imageTopBefore = imageTop;
                        Present(done.Result); imageLeft = jobLeft; imageTop = jobTop;
                        if (hadImage) canvasOffset += new Vector2((jobLeft - imageLeftBefore) * zoom,
                            (jobTop - imageTopBefore) * zoom);
                        bakeMilliseconds = Mathf.RoundToInt((float)((now - bakeStart) * 1000));
                        status = "预览已更新（无动态灯光）。"; Repaint();
                    }
                    else if (done.IsFaulted && !(done.Exception.GetBaseException() is OperationCanceledException))
                    { status = "烘焙失败：" + done.Exception.GetBaseException().Message; Repaint(); }
                }
                if (EditorApplication.timeSinceStartup < due || style == null || map == null) return;
                due = double.PositiveInfinity;
                if (!style.ProceduralRock) { status = "仅支持程序岩壁样式；请选择 StrataCave/Style.asset。"; Repaint(); return; }
                if (style.Background != null && style.Background.ContourStatic &&
                    style.Background.ContentHash != BackgroundBakeDescriptor.StyleContentHash)
                    throw new InvalidOperationException("背景样式内容身份不匹配。");
                cancellation?.Dispose(); cancellation = new CancellationTokenSource();
                var token = cancellation.Token;
                var blueprint = map.ReadBlueprint();
                var materials = blueprint.CopyMaterials(); var shapes = blueprint.CopyShapes();
                var foreground = style.CaptureModifiers(); var outline = style.CaptureOutline();
                var background = style.Background;
                var generator = background != null ? background.CaptureGenerator() : new DarkNights.Core.Logic.Terrain.ContourBackgroundGenerator();
                var modifiers = background != null ? background.CaptureModifiers() : new[] {
                    DarkNights.Core.Logic.Terrain.CaveModifierStack.Empty, DarkNights.Core.Logic.Terrain.CaveModifierStack.Empty,
                    DarkNights.Core.Logic.Terrain.CaveModifierStack.Empty };
                var visible = background != null && background.ContourStatic
                    ? new[] { background.Near, background.Middle, background.Deep } : new bool[3];
                int softness = background != null ? background.MiddleSoftness : 0;
                int x = left, y = top, stone = style.StoneSize; string seed = blueprint.Settings.Seed;
                jobLeft = x; jobTop = y; bakeStart = now;
                job = Task.Run(() => TerrainStylePreviewBaker.Bake(materials, shapes, seed, x, y, stone,
                    outline, foreground, generator, modifiers, visible, softness, token.ThrowIfCancellationRequested), token);
                status = "正在从初始地图计算轮廓与三层背景…"; Repaint();
            }
            catch (Exception error)
            { status = "预览失败：" + error.Message; due = double.PositiveInfinity; Repaint(); }
        }

        private void Present(byte[] rgba)
        {
            if (image == null) image = new Texture2D(TerrainStylePreviewBaker.Width, TerrainStylePreviewBaker.Height,
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
