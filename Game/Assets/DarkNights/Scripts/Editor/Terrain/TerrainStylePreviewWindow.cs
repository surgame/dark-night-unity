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
        private double due;
        private int left = 316, top = 284;
        private float zoom = 1;
        private Vector2 scroll;

        [MenuItem("Dark Nights/Terrain/岩壁实时预览（编辑态）")]
        public static void Open() => GetWindow<TerrainStylePreviewWindow>("岩壁调参");

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
            cancellation?.Cancel();
            cancellation?.Dispose(); cancellation = null;
            if (inspector != null) DestroyImmediate(inspector);
            if (image != null) DestroyImmediate(image);
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("请先退出 Play；编辑态预览不会在运行场景中改写资产。", MessageType.Warning); return; }
            EditorGUILayout.HelpBox("直接编辑原有共享资产；停止调整约 0.4 秒后自动烘焙。不进入 Play、不写场景。预览为初始地形的无动态灯光材质合成，底墙简化；最终画面仍以 Play 为准。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            map = (TerrainMapAsset)EditorGUILayout.ObjectField("固定地图", map, typeof(TerrainMapAsset), false);
            style = (CaveTerrainStyle)EditorGUILayout.ObjectField("岩壁样式", style, typeof(CaveTerrainStyle), false);
            left = EditorGUILayout.IntSlider("预览左边（原生 px）", left, 0, 2560 - TerrainStylePreviewBaker.Width);
            top = EditorGUILayout.IntSlider("预览上边（原生 px）", top, 0, 1536 - TerrainStylePreviewBaker.Height);
            if (EditorGUI.EndChangeCheck()) Invalidate();
            zoom = EditorGUILayout.Slider("显示倍率", zoom, 1, 3);

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
            if (image == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label(image, GUILayout.Width(image.width * zoom), GUILayout.Height(image.height * zoom));
            EditorGUILayout.EndScrollView();
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
                    var done = job; job = null;
                    if (!done.IsCanceled && !done.IsFaulted && cancellation != null && !cancellation.IsCancellationRequested)
                    { Present(done.Result); status = "预览已更新（无动态灯光）。"; Repaint(); }
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
