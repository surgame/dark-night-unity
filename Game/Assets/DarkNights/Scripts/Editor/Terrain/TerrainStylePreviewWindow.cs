using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 折叠分组的星球工作台；共用正式生成草稿，保留原生样式 Inspector、大画布与临时拆填。
    /// 窗口禁用时释放预览和编辑器句柄，草稿跨域重载保留；销毁时由唯一所有者释放。
    /// </summary>
    public sealed class TerrainStylePreviewWindow : EditorWindow
    {
        private const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        [SerializeField] private ExpeditionFlowDraft flowDraft;
        [SerializeField] private TerrainWorkbenchControls controls = new TerrainWorkbenchControls();
        [SerializeField] private TerrainStyleDrafts drafts = new TerrainStyleDrafts();
        [SerializeField] private CaveTerrainStyle style;
        [SerializeField] private ScriptableObject inspected;
        [SerializeField] private string planetId;
        [SerializeField] private float panelWidth = 360;
        [SerializeField] private Vector2 scroll;
        private TerrainGenerationPreview generation;
        private readonly TerrainMapDraft mapDraft = new TerrainMapDraft();
        private readonly TerrainStylePreviewCanvas preview = new TerrainStylePreviewCanvas();
        private readonly TerrainStylePreviewStage stage = new TerrainStylePreviewStage();
        private readonly TerrainJourneyPreview journey = new TerrainJourneyPreview();
        private TerrainBlueprint baselineBlueprint;
        private BackgroundBakeDescriptor backgroundReference;
        private string generatedIdentity, observedStyle, failedIdentity, status = "正在生成正式星球预览。";
        private double due = double.PositiveInfinity, nextRepaint, fpsStart;
        private int frames, canvasFps, renderMilliseconds;
        private byte fillMaterial = 1;
        private bool resizing, playingLastTick;
        private Texture Image => stage.Image;
        public ExpeditionFlowDraft Draft => flowDraft;

        public static void Open()
        {
            DarkNightsNativeWorkspace.Visual();
        }

        public static void OpenJourney()
        {
            Open(); GetWindow<TerrainStylePreviewWindow>().controls.FocusJourney();
        }

        public void ImportLegacyDraft(ExpeditionFlowDraft legacy)
        {
            if (mapDraft.HasChanges) throw new InvalidOperationException("先取消临时拆填，再转移旧航程草稿。");
            generation.Import(legacy); controls.Bind(generation); RefreshChanges();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Cave Wall Tuner"); minSize = new Vector2(800, 520);
            wantsMouseMove = true; preview.Fit();
            generation = new TerrainGenerationPreview(flowDraft); flowDraft = generation.Draft;
            if (!string.IsNullOrEmpty(planetId)) generation.PlanetId = planetId;
            controls.Bind(generation);
            if (style == null) style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset");
            if (inspected == null) inspected = style;
            saveChangesMessage = "地图与航程或表现草稿尚未保存；临时拆填不会写入作者资产。";
            Undo.undoRedoPerformed += RefreshChanges; EditorApplication.update += Tick; Invalidate();
        }

        private void OnDisable()
        {
            planetId = generation?.PlanetId;
            EditorApplication.update -= Tick; Undo.undoRedoPerformed -= RefreshChanges;
            preview.Stop(mapDraft.EndStroke); journey.Stop(); resizing = false;
            stage.Dispose(); controls.Dispose(); generation?.Dispose(false); generation = null;
        }

        private void OnDestroy()
        {
            drafts.Dispose();
            if (flowDraft != null) { Undo.ClearUndo(flowDraft); DestroyImmediate(flowDraft); }
        }

        private void OnLostFocus() { preview.Stop(mapDraft.EndStroke); resizing = false; }

        private void OnGUI()
        {
            if (generation == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorGUILayout.HelpBox("请先退出 Play；编辑态预览不改写运行场景。", MessageType.Info); return; }
            if (!controls.JourneyView && preview.HandleGridShortcut(Event.current,
                EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0)) Repaint();
            if (Event.current.type == EventType.Repaint)
            {
                frames++; double now = EditorApplication.timeSinceStartup;
                if (fpsStart == 0) fpsStart = now;
                if (now - fpsStart >= 1)
                { canvasFps = Mathf.RoundToInt((float)(frames / (now - fpsStart))); frames = 0; fpsStart = now; }
            }
            HandleSplitter(); panelWidth = Mathf.Clamp(panelWidth, 320, position.width - 360);
            float contentHeight = position.height - 82;
            var panel = new Rect(0, 32, panelWidth, contentHeight);
            var canvas = new Rect(panelWidth + 4, 82, position.width - panelWidth - 4, contentHeight - 82);
            controls.DrawHeader(new Rect(0, 0, position.width, 30), generation, RefreshChanges);
            controls.DrawCanvasTools(new Rect(canvas.x, 32, canvas.width, 48), preview, ref fillMaterial);
            EditorGUI.DrawRect(new Rect(panelWidth, 32, 4, contentHeight), new Color(.25f, .25f, .25f));
            EditorGUIUtility.AddCursorRect(new Rect(panelWidth - 3, 32, 10, contentHeight), MouseCursor.ResizeHorizontal);
            GUILayout.BeginArea(panel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            float labelWidth = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 145;
            try { controls.DrawSidebar(generation, style, drafts, ref inspected, mapDraft, RefreshChanges, Invalidate, journey.Play); }
            catch (Exception error) { status = error.Message; }
            finally { EditorGUIUtility.labelWidth = labelWidth; EditorGUILayout.EndScrollView(); GUILayout.EndArea(); }
            if (controls.JourneyView) journey.Draw(canvas, generation.Selected);
            else
            {
                preview.Input(canvas, Image, (x, y) => mapDraft.Paint(x, y,
                    preview.ActiveTool == TerrainStylePreviewTool.Fill, fillMaterial), InvalidateMap,
                    mapDraft.BeginStroke, mapDraft.EndStroke, mapDraft.Undo, mapDraft.Redo);
                preview.Draw(canvas, Image, canvasFps, renderMilliseconds,
                    !double.IsPositiveInfinity(due) || (Image != null && !stage.Ready), stage.Progress,
                    controls.LandingMarkers ? generation.Selected : null);
            }
            controls.DrawPreviewBar(new Rect(canvas.x, canvas.yMax + 2, canvas.width, 28), journey,
                mapDraft, InvalidateMap, () => { mapDraft.Cancel(); InvalidateMap(); });
            controls.DrawFooter(new Rect(0, position.height - 48, position.width, 48), generation, drafts, status,
                () => Attempt(SaveWorld), () => Attempt(ApplyDrafts), () => Attempt(DiscardChanges));
            hasUnsavedChanges = generation.HasChanges || drafts.HasChanges;
        }

        private void Attempt(Action action)
        {
            try { action(); }
            catch (Exception error) { status = "未保存：" + error.Message; }
            RefreshChanges();
        }

        private void SaveWorld() { generation.Apply(); status = "地图与航程已保存到 WorldSession；已有地图保持原格子。"; }
        private void ApplyDrafts() { int count = drafts.Apply(); drafts.Clear(); Invalidate(); status = count + " 个表现资产已保存。"; }
        public override void SaveChanges() { SaveWorld(); ApplyDrafts(); hasUnsavedChanges = false; }

        public override void DiscardChanges()
        {
            generation.Cancel(); controls.Bind(generation); drafts.Clear();
            hasUnsavedChanges = false; status = "地图与航程、表现草稿已取消。"; Invalidate();
        }

        private void RefreshChanges()
        {
            if (generation == null) return;
            hasUnsavedChanges = generation.HasChanges || drafts.HasChanges;
            if (generatedIdentity != generation.Identity && !mapDraft.HasChanges) Invalidate();
            Repaint();
        }

        private void HandleSplitter()
        {
            var input = Event.current;
            if (input.type == EventType.MouseDown && new Rect(panelWidth - 3, 32, 10, position.height - 82).Contains(input.mousePosition))
            { resizing = true; input.Use(); }
            if (input.type == EventType.MouseDrag && resizing)
            { panelWidth = Mathf.Clamp(input.mousePosition.x, 320, position.width - 360); input.Use(); Repaint(); }
            if (input.type == EventType.MouseUp && resizing) { resizing = false; input.Use(); }
        }

        private void Invalidate() { failedIdentity = null; due = EditorApplication.timeSinceStartup + .4; Repaint(); }

        private void InvalidateMap()
        {
            try { if (stage.Source != null) { stage.Source.ApplyChanges(mapDraft.DrainChangedCells()); stage.Tick(); } }
            catch (Exception error) { status = "预览刷新失败：" + error.Message; }
            Repaint();
        }

        private void Tick()
        {
            if (generation == null) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { stage.Dispose(); journey.Stop(); playingLastTick = true; return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (playingLastTick) { playingLastTick = false; Invalidate(); }
            double now = EditorApplication.timeSinceStartup;
            if (now >= nextRepaint) { nextRepaint = now + 1.0 / 30; Repaint(); }
            try
            {
                journey.Tick(generation.Selected); CheckExternalChanges();
                if (now >= due) RebuildNow();
                if (Image != null)
                {
                    bool drawn = stage.Tick();
                    if (stage.Error != null) status = "运行时预览失败：" + stage.Error.Message;
                    else if (drawn)
                    { renderMilliseconds = stage.LastCameraMilliseconds; if (stage.Ready) status = "运行时渲染链已就绪。"; }
                }
            }
            catch (Exception error)
            { status = "预览失败：" + error.Message; failedIdentity = generation.Identity; due = double.PositiveInfinity; Repaint(); }
        }

        private void CheckExternalChanges()
        {
            generation.ObserveExternalChanges();
            string current = style == null ? "none" : style.VisualIdentity;
            if (observedStyle != null && observedStyle != current)
            {
                if (drafts.HasChanges) { status = "表现源资产在窗口外变化；保存时会检查字段冲突。"; return; }
                drafts.Clear(); Invalidate();
            }
            observedStyle = current;
            if (generatedIdentity != generation.Identity && failedIdentity != generation.Identity && double.IsPositiveInfinity(due))
            {
                if (mapDraft.HasChanges) status = "正式生成配置已变化；请先取消临时拆填再重建。";
                else Invalidate();
            }
        }

        private void RebuildNow()
        {
            due = double.PositiveInfinity;
            if (generatedIdentity != generation.Identity) OpenMapSnapshot();
            if (style == null || baselineBlueprint == null) { status = "正式地图或岩壁样式不可用。"; return; }
            var background = drafts.Draft(drafts.Draft(style).Background);
            if (background != null && background.ContourStatic && background.ContentHash != BackgroundBakeDescriptor.StyleContentHash)
                throw new InvalidOperationException("背景样式内容身份不匹配。");
            stage.Open(generation.Definition, baselineBlueprint, style, drafts, mapDraft.CopyMaterials(),
                mapDraft.CopyShapes(), backgroundReference);
            status = "正在加载正式洞穴渲染…";
        }

        private void OpenMapSnapshot()
        {
            var result = generation.Generate();
            baselineBlueprint = result.Blueprint(); backgroundReference = result.Background;
            generatedIdentity = generation.Identity; mapDraft.OpenGenerated(baselineBlueprint);
        }
    }
}
