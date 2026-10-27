using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.View.Expedition
{
    /// <summary>
    /// 只消费权威勘测位图的个人星图；平移、缩放、星体和坐标选择均不改变业务。
    /// 未发现节点不创建按钮，默认相机不使用隐藏目录计算范围，避免泄露位置与总数。
    /// </summary>
    public sealed class StarMapElement : VisualElement, IDisposable
    {
        private readonly VisualElement nodeLayer = new VisualElement();
        private readonly VisualElement routes = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly StarMapFogElement fog = new StarMapFogElement();
        private readonly Label ship = new Label("▲ 本船") { pickingMode = PickingMode.Ignore };
        private readonly Label waypoint = new Label("＋ 航点") { pickingMode = PickingMode.Ignore };
        private readonly List<Button> buttons = new List<Button>();
        private readonly StarMapManipulator gestures;
        private JourneyViewData journey;
        private NavigationDefinition definition;
        private List<NavigationNodeDefinition> known = new List<NavigationNodeDefinition>();
        private string selected = "";
        private Vector2 center, selectedCoordinate;
        private float zoom = 1;
        private bool needsFit = true, coordinateSelected;
        public event Action<string> Selected;
        public event Action<float, float> CoordinateSelected;
        public event Action ViewChanged;
        public int ZoomPercent => Mathf.RoundToInt(zoom * 100);
        private float Scale => 2.8f * zoom;

        public StarMapElement()
        {
            AddToClassList("star-map"); focusable = true;
            foreach (var layer in new[] { nodeLayer, routes }) layer.AddToClassList("star-map-layer");
            nodeLayer.pickingMode = PickingMode.Ignore;
            ship.name = "ship-position"; ship.AddToClassList("star-ship");
            waypoint.AddToClassList("star-waypoint");
            Add(fog); Add(routes); Add(nodeLayer); Add(ship); Add(waypoint);
            generateVisualContent += DrawBackground; routes.generateVisualContent += DrawRoutes;
            gestures = new StarMapManipulator(this); this.AddManipulator(gestures);
            RegisterCallback<GeometryChangedEvent>(Resize);
        }

        public void Present(JourneyViewData value, string selection, bool coordinate = false, float x = 0, float y = 0)
        {
            journey = value; selected = selection; coordinateSelected = coordinate;
            selectedCoordinate = new Vector2(x, y);
            if (value?.Navigation == null)
            {
                nodeLayer.Clear(); buttons.Clear(); known.Clear(); definition = null; needsFit = true;
                ship.style.display = waypoint.style.display = DisplayStyle.None;
                fog.ClearSurvey(); MarkDirtyRepaint(); routes.MarkDirtyRepaint(); return;
            }
            var n = value.Navigation;
            if (definition == null || !definition.Matches(n.Definition)) { definition = n.Definition; needsFit = true; }
            var visible = definition.Nodes.Where(v => n.Survey.IsRevealed(v.X, v.Y)).ToList();
            if (!known.Select(v => v.Id).SequenceEqual(visible.Select(v => v.Id)))
            {
                nodeLayer.Clear(); buttons.Clear(); known = visible;
                foreach (var node in known)
                {
                    string id = node.Id;
                    var b = new Button(() => Choose(id)) { name = "star-node-" + id, userData = id, text = node.Name };
                    b.AddToClassList("star-node"); nodeLayer.Add(b); buttons.Add(b);
                }
            }
            if (needsFit) FitAll();
            Reproject();
        }

        public void FitAll()
        {
            if (journey?.Navigation == null) return;
            center = new Vector2(journey.Navigation.X + 20, journey.Navigation.Y);
            zoom = 1; needsFit = false; Reproject();
        }

        public void FocusShip()
        {
            if (journey?.Navigation == null) return;
            center = new Vector2(journey.Navigation.X, journey.Navigation.Y); Reproject();
        }

        public void FocusSelection()
        {
            var node = known.FirstOrDefault(v => v.Id == selected);
            if (node == null) return;
            center = new Vector2(node.X, node.Y); Reproject();
        }

        public void ZoomBy(float factor) => ZoomAt(contentRect.center, factor);
        internal void ZoomAt(Vector2 anchor, float factor)
        {
            Vector2 before = WorldAt(anchor);
            zoom = Mathf.Clamp(zoom * factor, .6f, 3f);
            center += before - WorldAt(anchor); Reproject();
        }
        internal void Pan(Vector2 delta)
        {
            center += new Vector2(-delta.x, delta.y) / Scale;
            center.x = Mathf.Clamp(center.x, -4096, 4096); center.y = Mathf.Clamp(center.y, -4096, 4096);
            Reproject();
        }
        internal void Choose(string id) { if (known.Any(n => n.Id == id)) Selected?.Invoke(id); }
        internal void ChooseCoordinate(Vector2 local)
        {
            Vector2 p = WorldAt(local);
            CoordinateSelected?.Invoke(Mathf.Clamp(Mathf.Round(p.x), -4096, 4096), Mathf.Clamp(Mathf.Round(p.y), -4096, 4096));
        }
        public void CancelGesture() => gestures.Cancel();
        private Vector2 WorldAt(Vector2 p) => center + new Vector2(p.x - contentRect.center.x, contentRect.center.y - p.y) / Scale;
        private Vector2 Position(float x, float y) => contentRect.center + new Vector2(x - center.x, center.y - y) * Scale;
        private void Resize(GeometryChangedEvent evt) { if (needsFit) FitAll(); Reproject(); }

        private void Reproject()
        {
            if (journey?.Navigation == null || contentRect.width < 1) return;
            for (int i = 0; i < buttons.Count; i++)
            {
                Vector2 p = Position(known[i].X, known[i].Y);
                buttons[i].style.translate = new Translate(p.x - 62, p.y - 27);
                buttons[i].EnableInClassList("is-selected", !coordinateSelected && known[i].Id == selected);
            }
            var n = journey.Navigation; Vector2 position = Position(n.X, n.Y);
            ship.style.display = DisplayStyle.Flex;
            ship.style.translate = new Translate(position.x - 30, position.y + 13);
            waypoint.style.display = coordinateSelected ? DisplayStyle.Flex : DisplayStyle.None;
            Vector2 target = Position(selectedCoordinate.x, selectedCoordinate.y);
            waypoint.style.translate = new Translate(target.x - 25, target.y - 12);
            fog.Present(n.Survey, center, Scale, contentRect.size);
            MarkDirtyRepaint(); routes.MarkDirtyRepaint(); ViewChanged?.Invoke();
        }

        private void DrawBackground(MeshGenerationContext ctx)
        {
            if (contentRect.width > 0 && contentRect.height > 0) StarMapArtwork.DrawStars(ctx.painter2D, contentRect);
        }
        private void DrawRoutes(MeshGenerationContext ctx)
        {
            if (journey?.Navigation == null || contentRect.width < 1) return;
            var n = journey.Navigation; var painter = ctx.painter2D; Vector2 p = Position(n.X, n.Y);
            var active = definition.Find(journey.PlanetId);
            if (n.CoordinateFlight) StarMapArtwork.Route(painter, p, Position(n.TargetX, n.TargetY), false, contentRect);
            else if (active != null && journey.Phase != JourneyPhase.Landed)
                StarMapArtwork.Route(painter, p, Position(active.X, active.Y), false, contentRect);
            var chosen = known.FirstOrDefault(v => v.Id == selected);
            if (coordinateSelected) StarMapArtwork.Route(painter, p, Position(selectedCoordinate.x, selectedCoordinate.y), true, contentRect);
            else if (chosen != null && chosen.Id != journey.PlanetId)
                StarMapArtwork.Route(painter, p, Position(chosen.X, chosen.Y), true, contentRect);
            foreach (var node in known)
            {
                Vector2 point = Position(node.X, node.Y);
                if (new Rect(-40, -40, contentRect.width + 80, contentRect.height + 80).Contains(point))
                    StarMapArtwork.Planet(painter, point, node.Danger, !coordinateSelected && node.Id == selected);
            }
            StarMapArtwork.Ship(painter, p);
        }
        public void Dispose()
        {
            CancelGesture(); fog.Dispose(); Selected = null; CoordinateSelected = null; ViewChanged = null;
        }
        public static string DangerLabel(int danger) => danger == 1 ? "低" : danger == 2 ? "中" : "高";
    }
}
