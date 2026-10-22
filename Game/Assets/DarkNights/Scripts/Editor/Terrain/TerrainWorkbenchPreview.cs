using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 同一地图工作台的只读显示快照与原渲染宿主；候选和正式引用仅切换查看，不结算或保存业务状态。
    /// IMGUI只保留已有地图画布及实验回放，任务栏和属性由UITK管理；关闭或Play时释放被动渲染资源。
    /// </summary>
    internal sealed class TerrainWorkbenchPreview : IDisposable
    {
        private readonly TerrainStylePreviewWindow owner;
        private readonly TerrainStylePreviewStage stage = new TerrainStylePreviewStage();
        private readonly TerrainStylePreviewCanvas canvas = new TerrainStylePreviewCanvas();
        private readonly TerrainVeinStudyPreview study = new TerrainVeinStudyPreview();
        private readonly TerrainJourneyPreview journey = new TerrainJourneyPreview();
        private readonly TerrainWorkbenchPicking picking;
        private bool pendingOpen, pendingStyle;
        private double nextPaint;
        private double fpsStart;
        private int frames, canvasFps;
        private string renderedStyleIdentity;
        private Vector2Int? focus;
        internal TerrainMapDraft Map { get; } = new TerrainMapDraft();
        internal PlayableTerrain Displayed { get; private set; }
        internal string Caption { get; private set; } = "尚未加载地图";
        internal string DisplayedIdentity { get; private set; }
        internal bool IsCandidate { get; private set; }
        internal TerrainStylePreviewCanvas Canvas => canvas;
        internal TerrainVeinStudyPreview Study => study;
        internal bool Pending => pendingOpen || pendingStyle || !stage.Ready;
        internal string CanvasStatus => Pending ? stage.Progress ?? "画面更新中…"
            : "预览 " + canvasFps + " FPS · 相机 " + stage.LastCameraMilliseconds + " ms · 缩放 " +
                canvas.Zoom.ToString("0.0") + "×";
        internal bool ShowGeologyDraft => IsCandidate && owner.State.Sites && owner.State.Page == "extras" &&
            owner.State.Extra == "collapse" && owner.Draft.Config.Geology?.Enabled == true;
        internal string OverlayLegend => ShowGeologyDraft
            ? "塌方编辑草图（不是已生成站点）：青＝源岩 · 橙＝进入区 · 黄＝支撑 · 绿＝退避 · 灰＝允许改形 · 棕＝落地；可定位放大。"
            : owner.State.Sites ? "实际站点：金色方块＝巢穴 · 棕色方块＝塌方点；点击标记选择。塌方编辑草图仅在对应设计页显示。"
            : "站点叠层已隐藏；区域／连接为规划诊断，不代表人物合法路线。";
        internal event Action RepaintRequested;

        internal TerrainWorkbenchPreview(TerrainStylePreviewWindow owner)
        {
            this.owner = owner; picking = new TerrainWorkbenchPicking(owner);
            canvas.ShowGrid = owner.State.Grid;
            canvas.GridMode = owner.State.LogicalGrid ? TerrainGridMode.Logical : TerrainGridMode.Render;
        }

        internal void RequestOpen() { pendingOpen = true; }
        internal void RequestStyle() { pendingStyle = true; }
        internal void RestoreStyleAfterUndo()
        {
            if (renderedStyleIdentity != null && renderedStyleIdentity != owner.Styles.VisualIdentity) RequestStyle();
        }
        internal void CancelPending() { pendingOpen = pendingStyle = false; }
        internal void StopStroke() => canvas.Stop(Map.EndStroke);
        internal void UsePan() { StopStroke(); canvas.ActiveTool = TerrainStylePreviewTool.Pan; }
        internal void FocusCell(int u, int row) { focus = new Vector2Int(u, row); }
        internal void Fit() => canvas.Fit();
        internal void ToggleJourney(bool value) { owner.State.ShowJourney = value; if (!value) journey.Stop(); }
        internal void PlayJourney() { owner.State.ShowJourney = true; journey.Play(); }

        internal void Tick()
        {
            if (pendingOpen) { pendingOpen = false; Open(); }
            if (pendingStyle) { pendingStyle = false; OpenStyle(); }
            journey.Tick(owner.Generation.Selected);
            study.Tick(owner.Generation, owner.State.Page == "advanced" && owner.State.ShowStudy);
            bool ready = stage.Ready;
            stage.Tick();
            if (stage.Error != null) throw stage.Error;
            if (!ready && stage.Ready && owner.State.Pick == 0) owner.Notice = "地图画布已就绪；当前对象：" + Caption;
            if (EditorApplication.timeSinceStartup < nextPaint) return;
            nextPaint = EditorApplication.timeSinceStartup + 1.0 / 30;
            RepaintRequested?.Invoke();
        }

        private void Open()
        {
            if (Map.HasChanges) throw new InvalidOperationException("先取消临时拆填，再切换查看对象。");
            var model = owner.Generation; var state = owner.State;
            var installed = model.InstalledConfig.ReviewedMap;
            IsCandidate = !state.ViewInstalled && state.Version == null;
            if (state.ViewInstalled && installed != null)
            {
                Displayed = installed.Freeze(TerrainGenerationPreview.WorldId);
                Caption = "正式新局引用 · " + installed.name;
                DisplayedIdentity = installed.Fingerprint(); IsCandidate = false;
            }
            else if (state.Version != null && !state.ViewInstalled)
            {
                Displayed = state.Version.Freeze(TerrainGenerationPreview.WorldId);
                Caption = "只读冻结版本 · " + AssetDatabase.GetAssetPath(state.Version);
                DisplayedIdentity = state.Version.Fingerprint(); IsCandidate = false;
            }
            else
            {
                if (model.GenerationRequest > 0 || model.LastCandidate == null)
                {
                    model.JointDraft = true;
                    state.CandidateMode = true;
                    model.Generate();
                }
                Displayed = model.LastCandidate;
                Caption = "草稿候选 · " + (model.RuleMode ? RoadLabel(model.Draft.Config.CaveMap.Rules.Road) :
                    model.Draft.Config.MineMap != null ? model.Draft.Config.MineMap.name : "旧随机来源");
                Caption += " · 种子 " + Displayed.Seed;
                DisplayedIdentity = model.LastCandidateIdentity;
                state.ViewInstalled = false; IsCandidate = true;
            }
            Map.OpenGenerated(Displayed.Blueprint());
            OpenStyle(); owner.RebuildPage();
        }

        private void OpenStyle()
        {
            if (Displayed == null || owner.Style == null) return;
            var style = owner.Styles.Draft(owner.Style);
            var background = owner.Styles.Draft(style.Background);
            if (background != null && background.ContourStatic && background.ContentHash != BackgroundBakeDescriptor.StyleContentHash)
                throw new InvalidOperationException("背景样式内容身份不匹配。");
            stage.Open(owner.Generation.Definition, Displayed.Blueprint(), owner.Style, owner.Styles,
                Map.CopyMaterials(), Map.CopyShapes(), Displayed.Background);
            renderedStyleIdentity = owner.Styles.VisualIdentity;
        }

        private static string RoadLabel(string id)
        {
            int index = Array.IndexOf(CaveRuleSettings.Roads, id);
            return "新规则 / " + (index < 0 ? id : new[] { "侵蚀曲廊", "断层折返", "洞厅星簇", "裂谷侧坡", "密岔根网" }[index]);
        }

        internal void RefreshMap()
        {
            if (stage.Source != null) { stage.Source.ApplyChanges(Map.DrainChangedCells()); stage.Tick(); }
            owner.Refresh(); RepaintRequested?.Invoke();
        }

        internal void CancelPaint()
        {
            StopStroke(); Map.Cancel(); UsePan(); RefreshMap();
            owner.Notice = "临时拆填已取消；原地图与作者资产未改变。";
        }

        internal void Draw(Rect rect)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { GUI.Label(rect, "编辑器Play期间暂停地图制作。"); return; }
            if (rect.width < 1 || rect.height < 1) return;
            if (Event.current.type == EventType.Repaint)
            {
                frames++; double now = EditorApplication.timeSinceStartup;
                if (fpsStart == 0) fpsStart = now;
                if (now - fpsStart >= 1) { canvasFps = Mathf.RoundToInt((float)(frames / (now - fpsStart))); frames = 0; fpsStart = now; }
            }
            if (owner.State.Page == "advanced" && owner.State.ShowStudy && study.Enabled)
            { study.Draw(rect); return; }
            if (owner.State.Page == "advanced" && owner.State.ShowJourney)
            { journey.Draw(rect, owner.Generation.Selected); return; }
            if (focus.HasValue && stage.Image != null)
            { canvas.FocusCell(focus.Value.x, focus.Value.y, rect, stage.Image); focus = null; }
            if (stage.Image != null && picking.Handle(rect, canvas.ImageScreenBounds(rect, stage.Image))) return;
            canvas.HandleGridShortcut(Event.current, EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0);
            canvas.Input(rect, stage.Image, (x, y) => Map.Paint(x, y,
                canvas.ActiveTool == TerrainStylePreviewTool.Fill, owner.State.FillMaterial), RefreshMap,
                Map.BeginStroke, Map.EndStroke, Map.Undo, Map.Redo);
            canvas.Draw(rect, stage.Image, canvasFps, stage.LastCameraMilliseconds, Pending, stage.Progress,
                owner.State.LandingMarkers ? owner.Generation.Selected : null, false);
            if (stage.Image == null) return;
            Rect bounds = canvas.ImageScreenBounds(rect, stage.Image);
            if (IsCandidate && DisplayedIdentity == owner.Generation.LastCandidateIdentity)
            {
                CaveRuleOverlay.Configure(owner.State.Regions, owner.State.Connections, owner.State.Minerals, false);
                CaveRuleOverlay.Draw(rect, bounds, owner.Generation);
            }
            if (ShowGeologyDraft) TerrainGeologyOverlay.Draw(rect, bounds, owner.Draft.Config.Geology);
            if (owner.State.Sites) picking.Draw(rect, bounds);
        }

        internal void Suspend() { StopStroke(); stage.Dispose(); journey.Stop(); }
        public void Dispose() { Suspend(); study.Dispose(); }
    }
}
