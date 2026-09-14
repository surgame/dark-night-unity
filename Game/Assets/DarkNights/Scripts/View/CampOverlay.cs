using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 读取投影与本地选择，在原生 HUD 下层绘制生命、施工、生产指示和框选。
    /// 锚点取自实体的显式视觉绑定；仅表现受伤和进度，不判定命中、生产或施工完成。
    /// </summary>
    public sealed class CampOverlay : UiShapeGraphic
    {
        private SessionViewData frame;
        private CampInput input;
        private IEntityVisuals visuals;
        private PinewatchStage stage;
        private GameCatalog catalog;

        public void Present(SessionViewData value, CampInput controls, IEntityVisuals entities, PinewatchStage scene, GameCatalog rules)
        {
            frame = value; input = controls; visuals = entities; stage = scene; catalog = rules;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (frame == null || input == null) return;
            float zoom = stage.Zoom;
            foreach (ActorViewData actor in frame.World.Actors)
            {
                EntityView visual = visuals.Visual(actor.Id);
                if (visual == null) continue;
                bool selected = input.Selected.Contains(actor.Id), hover = input.Hover == actor.Id;
                Vector2 root = Point(visual.transform.position);
                if (selected || hover)
                    Ellipse(mesh, root + new Vector2(0, -.5f) * zoom, new Vector2(7, 2.1f) * zoom,
                        selected ? new Color32(238, 221, 160, 255) : new Color32(192, 210, 187, 255), (selected ? 1.4f : 1) * zoom);
                double maximum = catalog.Balance.Units[actor.Kind].Hp;
                if (selected || hover || actor.Hp < maximum)
                    Bar(mesh, Point(visual.StatusAnchor.position), 14, 2, actor.Hp / maximum,
                        new Color32(21, 33, 38, 255), actor.Enemy ? new Color32(193, 119, 112, 255) : new Color32(162, 199, 149, 255));
                if (actor.Kind == "worker" && actor.Activity == "Idle")
                    Ellipse(mesh, root + new Vector2(0, -17) * zoom, Vector2.one * zoom, new Color32(223, 198, 139, 255));
            }
            foreach (BuildingViewData building in frame.World.Buildings)
            {
                EntityView visual = visuals.Visual(building.Id);
                if (visual == null) continue;
                BuildingDefinition definition = catalog.Balance.Buildings[building.Kind];
                Vector2 root = Point(visual.transform.position), status = Point(visual.StatusAnchor.position);
                bool chosen = input.Selected.Contains(building.Id) || input.Hover == building.Id;
                if (chosen) Line(mesh, root + new Vector2(-definition.Width * .5f, 1) * zoom,
                    root + new Vector2(definition.Width * .5f, 1) * zoom, new Color32(225, 201, 142, 255), zoom);
                if (chosen || building.Progress < 1 || building.Hp < definition.Hp)
                {
                    Bar(mesh, status, 30, 3, building.Hp / definition.Hp, new Color32(23, 33, 39, 255), new Color32(166, 195, 138, 255));
                    if (building.Progress < 1) Bar(mesh, status + new Vector2(0, 5) * zoom, 30, 2, building.Progress,
                        new Color32(23, 33, 39, 255), new Color32(226, 197, 138, 255));
                }
                if (building.Training.Count > 0) Bar(mesh, root + new Vector2(0, 5) * zoom, 32, 2,
                    1 - building.Training[0].Remaining / catalog.Balance.Economy.TrainingSeconds,
                    new Color32(23, 33, 39, 255), new Color32(159, 191, 209, 255));
            }
            foreach (WorksiteViewData site in frame.World.Worksites)
            {
                EntityView visual = visuals.Visual(site.Id);
                if (visual == null) continue;
                Vector2 root = Point(visual.transform.position);
                if (input.Selected.Contains(site.Id) || input.Hover == site.Id)
                    Line(mesh, root + new Vector2(-12, 1) * zoom, root + new Vector2(12, 1) * zoom, new Color32(217, 196, 132, 255), zoom);
                if (site.WorkerId != 0) Bar(mesh, root + new Vector2(0, 5) * zoom, 20, 2,
                    site.Progress / catalog.Balance.Worksites[site.Kind].Interval, new Color32(24, 38, 43, 255), new Color32(163, 198, 139, 255));
            }
            if (input.BuildKind.Length > 0)
            {
                var definition = catalog.Balance.Buildings[input.BuildKind];
                Vector2 root = Point(new Vector3(input.PlacementX / 100, 0));
                Color tint = input.PlacementValid ? new Color(.69f, .91f, .65f, .68f) : new Color(1, .35f, .28f, .65f);
                Line(mesh, root + new Vector2(-definition.Width * .5f, 2) * zoom,
                    root + new Vector2(definition.Width * .5f, 2) * zoom, tint, 2 * zoom);
                if (input.BuildKind == "tower")
                {
                    tint.a = .3f;
                    Line(mesh, root + new Vector2(-(float)definition.Range, 6) * zoom,
                        root + new Vector2((float)definition.Range, 6) * zoom, tint, zoom);
                }
            }
            if (input.Dragging && Vector2.Distance(input.DragStart, input.Pointer) > 5)
            {
                Vector2 a = new Vector2(input.DragStart.x, Screen.height - input.DragStart.y);
                Vector2 b = new Vector2(input.Pointer.x, Screen.height - input.Pointer.y);
                Rect box = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                Box(mesh, box, new Color(.75f, .83f, .66f, .12f));
                Border(mesh, box, new Color32(213, 216, 163, 255), .5f * zoom);
            }
        }

        private Vector2 Point(Vector3 position)
        {
            Vector3 screen = stage.SceneCamera.WorldToScreenPoint(position);
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        protected override Color VertexTint(Vector2 point, Color tint)
        {
            Vector3 world = stage.SceneCamera.ScreenToWorldPoint(new Vector3(point.x, Screen.height - point.y,
                -stage.SceneCamera.transform.position.z));
            // UGUI 接收 sRGB 颜色编码；在编码前按场景的 Linear 光照合成。
            return (tint.linear * stage.IlluminationAt(world)).gamma;
        }

        private void Bar(VertexHelper mesh, Vector2 center, float width, float height, double value, Color background, Color fill)
        {
            Rect bar = new Rect(center.x - width * stage.Zoom * .5f, center.y, width * stage.Zoom, height * stage.Zoom);
            Box(mesh, bar, background);
            bar.width *= Mathf.Clamp01((float)value);
            Box(mesh, bar, fill);
        }
    }
}
