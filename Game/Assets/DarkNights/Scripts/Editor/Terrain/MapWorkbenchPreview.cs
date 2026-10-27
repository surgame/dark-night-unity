using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 同一地图工作台的只读显示快照与原渲染宿主；只呈现当前编辑配置，不结算或保存业务状态。
    /// IMGUI只保留已有地图画布及实验回放，任务栏和属性由UITK管理；关闭或Play时释放被动渲染资源。
    /// </summary>
    internal sealed class MapWorkbenchPreview : IDisposable
    {
        private readonly MapWorkbenchWindow owner;
        private readonly TerrainStylePreviewStage stage = new TerrainStylePreviewStage(terrainFirst: true);
        private readonly TerrainStylePreviewCanvas canvas = new TerrainStylePreviewCanvas();
        private readonly TerrainVeinStudyPreview study = new TerrainVeinStudyPreview();
        private readonly TerrainJourneyPreview journey = new TerrainJourneyPreview();
        private readonly MapWorkbenchPicking picking;
        private readonly TerrainPreviewComparison previous = new TerrainPreviewComparison();
        private readonly TerrainDesignOverlay designOverlay;
        private readonly MapWorkbenchCanvas presenter;
        internal TerrainDesignSurface Design { get; } = new TerrainDesignSurface();
        internal bool DesignMode => !owner.State.RenderMode;
        private string renderedGeometry, renderedSeed;
        internal bool Compare { get; set; }
        internal bool HasPrevious => previous.Image != null;
        private bool pendingOpen, pendingStyle, pendingEnvironment, waitingForReplacementFrame;
        private double nextPaint, styleAfter = double.PositiveInfinity, updateStarted = -1;
        private int lastUpdateMilliseconds;
        private string renderedStyleIdentity, renderedTerrainStyleIdentity;
        private PlayableTerrain frozenMap;
        internal TerrainMapDraft Map { get; } = new TerrainMapDraft();
        internal PlayableTerrain Displayed { get; private set; }
        internal string Caption { get; private set; } = "尚未加载地图";
        internal string ResultOrigin => !IsCandidate || owner.Generation.CandidateFromSaved
            ? "文件内已保存结果"
            : "当前规则生成 · 尚未保存";
        internal string DisplayedIdentity { get; private set; }
        internal bool IsCandidate { get; private set; }
        internal TerrainStylePreviewCanvas Canvas => canvas;
        internal TerrainVeinStudyPreview Study => study;
        internal bool InputPending => owner.FileLoad?.Busy == true || owner.WaitingForInput || pendingOpen;
        internal bool Pending => InputPending || Displayed == null ||
            (DesignMode ? !Design.Ready : pendingStyle || pendingEnvironment || !stage.TerrainReady);
        internal bool NavigationOnly => !IsCandidate || InputPending || Compare ||
            (!DesignMode && (pendingStyle || pendingEnvironment || waitingForReplacementFrame));
        internal bool SaveReady => !InputPending && IsCandidate && owner.Generation.CandidateFresh &&
            ReferenceEquals(Displayed, owner.Generation.LastCandidate) && !Map.HasChanges;
        internal bool BackgroundPending => !DesignMode && !Pending && !stage.Ready;
        internal bool AwaitingRender => waitingForReplacementFrame;
        internal string UpdateStatus => owner.WaitingForInput ? "正在合并输入…"
            : pendingOpen ? "地图计算中…"
            : Pending ? DesignMode ? "白模更新中…" : "渲染更新中…"
            : DesignMode ? "结构白模已就绪"
            : BackgroundPending ? "地形已就绪 · 背景加载中…"
            : lastUpdateMilliseconds > 0 ? "已同步 · " + lastUpdateMilliseconds + " ms" : "已同步";
        internal string CanvasStatus => pendingOpen ? "后台生成最新地图；保留上一幅画面"
            : InputPending ? "正在读取当前地图…"
            : DesignMode ? Design.CacheStatus + " · 缓存 " + Design.CachedMaps + "/3 · 缩放 " + canvas.Zoom.ToString("0.0") + "×"
            : Pending ? stage.Progress ?? "画面更新中…" : "相机 " + stage.LastCameraMilliseconds + " ms · 缩放 " + canvas.Zoom.ToString("0.0") + "×";
        internal bool ShowGeologyDraft => IsCandidate && owner.State.Sites && owner.State.Page == "extras" &&
            owner.State.Extra == "collapse" && owner.Draft.Config.Geology?.Enabled == true;
        internal string OverlayLegend => DesignMode ? designOverlay.Legend : ShowGeologyDraft
            ? "塌方编辑草图（不是已生成站点）：青＝源岩 · 橙＝进入区 · 黄＝支撑 · 绿＝退避 · 灰＝允许改形 · 棕＝落地；可定位放大。"
            : owner.State.Sites ? "实际站点：金色方块＝巢穴 · 棕色方块＝塌方点；点击标记选择。塌方编辑草图仅在对应设计页显示。"
            : "站点叠层已隐藏；区域／连接为规划诊断，不代表人物合法路线。";
        internal event Action RepaintRequested;

        internal MapWorkbenchPreview(MapWorkbenchWindow owner)
        {
            this.owner = owner; picking = new MapWorkbenchPicking(owner);
            designOverlay = new TerrainDesignOverlay(owner);
            presenter = new MapWorkbenchCanvas(owner, stage, canvas, previous, picking, designOverlay);
            canvas.ShowGrid = owner.State.Grid;
            canvas.GridMode = owner.State.LogicalGrid ? TerrainGridMode.Logical : TerrainGridMode.Render;
        }

        internal void RequestOpen() { BeginUpdate(); pendingOpen = true; }
        internal void SetFrozen(PlayableTerrain map) { frozenMap = map; RequestOpen(); }
        internal void RequestStyle()
        {
            BeginUpdate(); pendingStyle = true;
            styleAfter = Math.Min(styleAfter, EditorApplication.timeSinceStartup + .08);
        }
        internal void RequestEnvironment()
        {
            BeginUpdate(); pendingEnvironment = true;
            styleAfter = Math.Min(styleAfter, EditorApplication.timeSinceStartup + 1.0 / 30);
        }
        private void BeginUpdate()
        {
            if (updateStarted < 0) updateStarted = EditorApplication.timeSinceStartup;
            if (!owner.Progress.Active) owner.Progress.Begin("更新地图");
        }
        internal void RestoreStyleAfterUndo()
        {
            if (DesignMode) { RequestEnvironment(); return; }
            if (renderedStyleIdentity == null || renderedStyleIdentity == owner.Styles.VisualIdentity) return;
            if (renderedTerrainStyleIdentity == owner.Styles.TerrainIdentity) RequestEnvironment();
            else RequestStyle();
        }
        internal void CancelPending()
        { pendingOpen = pendingStyle = pendingEnvironment = false; styleAfter = double.PositiveInfinity; updateStarted = -1; }
        internal void StopStroke() => canvas.Stop(Map.EndStroke);
        internal void UsePan() { StopStroke(); canvas.ActiveTool = TerrainStylePreviewTool.Pan; }
        internal void FocusCell(int u, int row) { presenter.Focus(u, row); }
        internal void Fit() { canvas.Fit(); stage.SetFullMapCamera(); }
        internal void ToggleJourney(bool value) { owner.State.ShowJourney = value; if (!value) journey.Stop(); }
        internal void PlayJourney() { owner.State.ShowJourney = true; journey.Play(); }

        internal void Tick()
        {
            if (pendingOpen && !owner.FileLoad.Busy && (!owner.WaitingForInput || owner.Generation.IsGenerating))
            {
                var model = owner.Generation;
                bool needsGeneration = owner.State.ViewedMap == null && (model.GenerationRequest > 0 || !model.CandidateFresh);
                if (needsGeneration) owner.Progress.Phase("后台生成地图");
                if (!needsGeneration || model.AdvanceGeneration())
                {
                    pendingOpen = false; owner.Progress.Phase(DesignMode ? "准备结构白模" : "构建地形与背景");
                    var install = System.Diagnostics.Stopwatch.StartNew(); Open();
                    owner.Progress.MainStep("安装预览宿主", install.Elapsed.TotalMilliseconds);
                }
            }
            if (DesignMode)
            {
                if ((pendingStyle || pendingEnvironment) && Displayed != null && EditorApplication.timeSinceStartup >= styleAfter)
                {
                    designOverlay.Capture(); pendingStyle = pendingEnvironment = false;
                    styleAfter = double.PositiveInfinity;
                }
                Design.Minerals(owner.State.Minerals); Design.Prepare();
                if (!InputPending && !Design.Ready)
                {
                    owner.Progress.Phase("等待结构白模实际绘制");
                    owner.Progress.Detail = "材料、12种坡形、保护格、独立矿层与设计旗标；不构建岩壁／背景美术页。";
                }
                if (!InputPending && Design.Ready)
                {
                    owner.Progress.MarkDesignReady();
                    if (owner.Progress.Active)
                    {
                        lastUpdateMilliseconds = (int)owner.Progress.ElapsedMilliseconds;
                        owner.Progress.Finish("结构白模已就绪"); updateStarted = -1; owner.Refresh();
                        if (owner.State.Pick == 0) owner.Notice = "结构白模已就绪；切到渲染模式时才加载完整美术。";
                    }
                }
            }
            else TickRendered();
            journey.Tick(owner.Generation.Selected);
            study.Tick(owner.Generation, owner.State.ViewedMap == null && owner.State.Page == "advanced" && owner.State.ShowStudy);
            if (EditorApplication.timeSinceStartup < nextPaint) return;
            nextPaint = EditorApplication.timeSinceStartup + 1.0 / 30;
            RepaintRequested?.Invoke();
        }

        private void TickRendered()
        {
            if (!owner.FileLoad.Busy && (pendingStyle || pendingEnvironment) && Displayed != null && stage.TerrainReady &&
                EditorApplication.timeSinceStartup >= styleAfter)
            {
                previous.Capture(stage);
                if (pendingStyle) OpenStyle();
                else
                {
                    stage.ReplaceEnvironment(owner.Style, owner.Styles, Displayed.Background);
                    renderedStyleIdentity = owner.Styles.VisualIdentity;
                }
                pendingStyle = pendingEnvironment = false; styleAfter = double.PositiveInfinity;
            }
            Design.Minerals(owner.State.Minerals); Design.Prepare();
            bool ready = stage.TerrainReady;
            if (owner.Progress.Active && !owner.FileLoad.Busy && !pendingOpen)
            {
                owner.Progress.Phase(stage.TerrainReady ? "地形已就绪，补载背景" : "优先构建地形");
            }
            var tick = System.Diagnostics.Stopwatch.StartNew();
            stage.Tick();
            owner.Progress.MainStep("更新表现及相机", tick.Elapsed.TotalMilliseconds);
            if (stage.Error != null) throw stage.Error;
            if (!owner.FileLoad.Busy && !owner.WaitingForInput && !pendingOpen && !pendingStyle && !pendingEnvironment)
                owner.Progress.Observe(stage);
            if (stage.TerrainReady) waitingForReplacementFrame = false;
            if (!Pending) owner.Progress.MarkTerrainReady();
            if (!ready && stage.TerrainReady && owner.State.Pick == 0) owner.Notice = "地形已就绪，可继续编辑；背景随后补载。";
            if (!Pending && stage.Ready && updateStarted >= 0)
            {
                owner.Progress.Observe(stage, true);
                lastUpdateMilliseconds = (int)((EditorApplication.timeSinceStartup - updateStarted) * 1000);
                updateStarted = -1; owner.Progress.Finish(); owner.Refresh();
            }
        }

        private void Open()
        {
            if (Map.HasChanges) throw new InvalidOperationException("先取消临时拆填，再切换查看对象。");
            var model = owner.Generation; var state = owner.State;
            int priorRooms = model.RulePlan?.Rooms.Count ?? 0;
            bool first = Displayed == null;
            if (DesignMode && Design.Ready) previous.Capture(Design.Image);
            else if (!DesignMode && stage.TerrainReady) previous.Capture(stage);
            waitingForReplacementFrame = true;
            IsCandidate = state.ViewedMap == null;
            Displayed = IsCandidate ? model.LastCandidate : frozenMap ??
                throw new InvalidOperationException("只读地图尚未完成后台读取。");
            Caption = (IsCandidate ? state.Document != null ? state.Document.name : "未命名地图" :
                "只读 · " + AssetDatabase.GetAssetPath(state.ViewedMap)) + " · 种子 " + Displayed.Seed;
            DisplayedIdentity = IsCandidate ? model.LastCandidateIdentity : state.ViewedMap.ContentSha256;
            Map.OpenGenerated(Displayed.Blueprint());
            var document = IsCandidate && !owner.Generation.HasChanges ? state.Document : null;
            Design.Open(Displayed, DisplayedIdentity, document);
            Design.Minerals(state.Minerals); Design.Prepare(); designOverlay.Capture();
            string geometry = Displayed.Background.ReferenceHash;
            if (DesignMode) { waitingForReplacementFrame = false; pendingStyle = pendingEnvironment = false; }
            else if (stage.CanReplaceCandidate && renderedSeed == Displayed.Seed &&
                renderedTerrainStyleIdentity == owner.Styles.TerrainIdentity)
            {
                if (renderedGeometry == geometry) stage.ReplaceMinerals(Displayed);
                else stage.ReplaceCandidate(Displayed, owner.Style, owner.Styles);
                if (renderedStyleIdentity != owner.Styles.VisualIdentity)
                {
                    stage.ReplaceEnvironment(owner.Style, owner.Styles, Displayed.Background);
                    renderedStyleIdentity = owner.Styles.VisualIdentity;
                    pendingEnvironment = false;
                }
                renderedGeometry = geometry;
            }
            else OpenStyle();
            if (first || owner.State.Page == "flow" || owner.State.Page == "minerals" && priorRooms == 0) owner.RebuildPage();
        }

        private void OpenStyle()
        {
            if (Displayed == null || owner.Style == null) return;
            var style = owner.Styles.Draft(owner.Style);
            var background = owner.Styles.Draft(style.Background);
            if (background != null && background.ContourStatic && background.ContentHash != BackgroundBakeDescriptor.StyleContentHash)
                throw new InvalidOperationException("背景样式内容身份不匹配。");
            var position = stage.Camera != null ? stage.Camera.transform.localPosition : new Vector3(160, -96, -10);
            float size = stage.Camera != null ? stage.Camera.orthographicSize : 96;
            waitingForReplacementFrame = true;
            stage.Open(owner.Generation.Definition, Displayed.Blueprint(), owner.Style, owner.Styles,
                Map.CopyMaterials(), Map.CopyShapes(), Displayed.Background, cameraPosition: position, cameraSize: size,
                planet: owner.Generation.Selected.Freeze());
            stage.ReplaceMinerals(Displayed);
            presenter.SynchronizeCamera();
            renderedGeometry = Displayed.Background.ReferenceHash; renderedSeed = Displayed.Seed;
            renderedStyleIdentity = owner.Styles.VisualIdentity;
            renderedTerrainStyleIdentity = owner.Styles.TerrainIdentity;
            pendingStyle = pendingEnvironment = false; styleAfter = double.PositiveInfinity;
        }

        internal void RefreshMap()
        {
            var changes = Map.DrainChangedCells(); Design.Apply(changes); Design.Minerals(owner.State.Minerals); Design.Prepare();
            if (stage.Source != null) { stage.Source.ApplyChanges(changes); stage.Tick(); }
            owner.Refresh(); RepaintRequested?.Invoke();
        }

        internal void SurfaceCamera(float column)
        {
            if (Displayed == null) return;
            int x = Mathf.Clamp(Mathf.RoundToInt(column), 3, 316), row = 0;
            while (row < 191 && Displayed.Material(x, row) == 0) row++;
            if (DesignMode) { FocusCell(x, row); return; }
            stage.SetSurfaceCamera(column, row); RepaintRequested?.Invoke();
        }

        internal void FullMapCamera()
        { Fit(); RepaintRequested?.Invoke(); }

        internal void CancelPaint()
        {
            StopStroke(); Map.Cancel(); UsePan(); RefreshMap();
            owner.Notice = "临时拆填已取消；原地图与作者资产未改变。";
        }

        internal void Draw(Rect rect) => presenter.Draw(rect);
        internal void DrawJourney(Rect rect) => journey.Draw(rect, owner.Generation.Selected);

        internal void SetRenderMode(bool render)
        {
            if (owner.State.RenderMode == render) return;
            StopStroke(); Compare = false; previous.Dispose(); owner.State.RenderMode = render;
            if (render) { if (Displayed != null && !InputPending) { BeginUpdate(); OpenStyle(); } }
            else
            {
                stage.Dispose(); renderedGeometry = renderedSeed = renderedStyleIdentity = renderedTerrainStyleIdentity = null;
                pendingStyle = pendingEnvironment = waitingForReplacementFrame = false;
                if (!InputPending) { owner.Progress.Finish("已返回设计模式"); updateStarted = -1; }
            }
            owner.Refresh(); RepaintRequested?.Invoke();
        }

        internal void Suspend() { StopStroke(); stage.Dispose(); Design.Dispose(); previous.Dispose(); journey.Stop(); }
        public void Dispose() { Suspend(); study.Dispose(); previous.Dispose(); }
    }
}
