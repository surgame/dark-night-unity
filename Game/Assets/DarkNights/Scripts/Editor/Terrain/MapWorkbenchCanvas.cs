using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图工作台双模式共用的IMGUI画布；所有导航、查格、叠层和临时笔触沿同一坐标变换。
    /// 当前白模垫在尚未就绪的渲染下，旧候选只供有标识的导航／比较，不能向旧截图提交编辑。
    /// </summary>
    internal sealed class MapWorkbenchCanvas
    {
        private readonly MapWorkbenchWindow owner;
        private readonly TerrainStylePreviewStage stage;
        private readonly TerrainStylePreviewCanvas canvas;
        private readonly TerrainPreviewComparison previous;
        private readonly MapWorkbenchPicking picking;
        private readonly TerrainDesignOverlay overlay;
        private Vector2Int? focus;
        private Rect lastRect;
        internal MapWorkbenchCanvas(MapWorkbenchWindow owner, TerrainStylePreviewStage stage, TerrainStylePreviewCanvas canvas,
            TerrainPreviewComparison previous, MapWorkbenchPicking picking, TerrainDesignOverlay overlay)
        {
            this.owner = owner; this.stage = stage; this.canvas = canvas;
            this.previous = previous; this.picking = picking; this.overlay = overlay;
        }

        internal void Focus(int u, int row) { focus = new Vector2Int(u, row); }

        internal void SynchronizeCamera()
        {
            if (lastRect.width > 0 && stage.Image != null)
                stage.SetCanvasCamera(canvas.CameraCenter(lastRect, stage.Image), 96 / canvas.Zoom);
        }

        internal void Draw(Rect rect)
        {
            var preview = owner.Preview;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { GUI.Label(rect, "编辑器Play期间暂停地图制作。"); return; }
            if (rect.width < 1 || rect.height < 1) return;
            lastRect = rect;
            if (previous.Image != null && (preview.Compare || preview.InputPending))
            {
                canvas.Input(rect, previous.Image, (_, __) => false, () => { }, navigationOnly: true);
                previous.Draw(rect, canvas);
                GUI.Label(new Rect(10, 8, 300, 22), preview.Compare ? "比较：上一次有效画面" : "正在读取／计算；上一幅画面", EditorStyles.whiteLabel);
                return;
            }
            if (owner.State.Page == "advanced" && owner.State.ShowStudy && preview.Study.Enabled)
            { preview.Study.Draw(rect); return; }
            if (owner.State.Page == "advanced" && owner.State.ShowJourney)
            { preview.DrawJourney(rect); return; }
            bool white = preview.DesignMode || preview.AwaitingRender || stage.Image == null;
            Texture image = white ? preview.Design.Image : stage.Image;
            if (!white && stage.SurfaceCameraActive && image != null)
                canvas.AdoptCamera(stage.Camera.transform.localPosition, stage.Camera.orthographicSize, rect, image);
            if (focus.HasValue && image != null)
            { canvas.FocusCell(focus.Value.x, focus.Value.y, rect, image); focus = null; }
            if (image == null)
            { canvas.Draw(rect, null, 0, 0, true, showStatus: false); return; }
            var bounds = canvas.ImageScreenBounds(rect, image);
            overlay.Input(rect, bounds, !preview.InputPending);
            if (!preview.NavigationOnly && picking.Handle(rect, bounds)) return;
            canvas.HandleGridShortcut(Event.current, EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0);
            canvas.Input(rect, image, (x, y) => preview.Map.Paint(x, y,
                canvas.ActiveTool == TerrainStylePreviewTool.Fill, owner.State.FillMaterial), preview.RefreshMap,
                preview.Map.BeginStroke, preview.Map.EndStroke, preview.Map.Undo, preview.Map.Redo,
                navigationOnly: preview.NavigationOnly);
            if (!preview.DesignMode && stage.Image != null)
                stage.SetCanvasCamera(canvas.CameraCenter(rect, image), 96 / canvas.Zoom);
            canvas.Draw(rect, image, 0, stage.LastCameraMilliseconds, preview.Pending,
                planet: null, showStatus: false, cameraFramed: !white);
            bounds = canvas.ImageScreenBounds(rect, image);
            if (preview.IsCandidate && preview.DisplayedIdentity == owner.Generation.LastCandidateIdentity)
            {
                CaveRuleOverlay.Configure(owner.State.Regions, owner.State.Connections, !white && owner.State.Minerals, false);
                CaveRuleOverlay.Draw(rect, bounds, owner.Generation);
                picking.DrawRegion(rect, bounds);
            }
            if (white)
            {
                overlay.Draw(rect, bounds);
                if (Event.current.type == EventType.Repaint) preview.Design.ConfirmDraw();
                GUI.Label(new Rect(10, 8, 360, 22), preview.DesignMode ? "设计模式 · 原始结构" : "渲染加载中 · 当前地图白模", EditorStyles.whiteLabel);
            }
            else if (owner.State.Sites) picking.Draw(rect, bounds);
            if (preview.ShowGeologyDraft) TerrainGeologyOverlay.Draw(rect, bounds, owner.Draft.Config.Geology);
        }
    }
}
