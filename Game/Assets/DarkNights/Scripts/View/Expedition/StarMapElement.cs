using System;
using System.Collections.Generic;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.View.Expedition
{
    /// <summary>
    /// 星图的被动绘制与个人节点选择；只保留冻结资料和本地浏览，不发送业务请求。
    /// 节点按钮与飞船位置共用固定目录坐标转换，尺寸变化仅重排本地表现。
    /// </summary>
    public sealed class StarMapElement : VisualElement
    {
        private readonly List<Button> nodes = new List<Button>();
        private readonly Label shipMarker = new Label("▲ 本船") { pickingMode = PickingMode.Ignore };
        private JourneyViewData journey;
        private NavigationDefinition definition;
        private string selected = "";
        public event Action<string> Selected;

        public StarMapElement()
        {
            AddToClassList("star-map");
            shipMarker.name = "ship-position"; shipMarker.AddToClassList("star-ship");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => LayoutNodes());
        }

        public void Present(JourneyViewData value, string selection)
        {
            journey = value; selected = selection;
            if (value?.Navigation == null) return;
            var incoming = value.Navigation.Definition;
            if (definition == null || !definition.Matches(incoming))
            {
                definition = incoming; Clear(); nodes.Clear();
                foreach (var node in definition.Nodes)
                {
                    string id = node.Id;
                    var button = new Button(() => Selected?.Invoke(id)) { name = "star-node-" + id };
                    button.AddToClassList("star-node");
                    button.text = node.Name + "\n危险 " + DangerLabel(node.Danger);
                    button.tooltip = node.Risk;
                    Add(button); nodes.Add(button);
                }
                Add(shipMarker);
            }
            LayoutNodes(); MarkDirtyRepaint();
        }

        private void LayoutNodes()
        {
            if (definition == null || contentRect.width < 1) return;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = definition.Nodes[i]; var position = Position(node.X, node.Y);
                nodes[i].style.left = position.x - 64;
                nodes[i].style.top = position.y - 18;
                nodes[i].EnableInClassList("is-selected", node.Id == selected);
                nodes[i].EnableInClassList("is-target", node.Id == journey?.PlanetId);
            }
            var positionShip = Position(journey.Navigation.X, journey.Navigation.Y);
            shipMarker.style.left = positionShip.x - 23;
            float markerTop = positionShip.y - 24;
            foreach (var node in definition.Nodes)
            {
                var p = Position(node.X, node.Y);
                var nodeBounds = new Rect(p.x - 68, p.y - 22, 136, 56);
                if (nodeBounds.Overlaps(new Rect(positionShip.x - 23, markerTop, 76, 20)))
                    markerTop = p.y + 60 <= contentRect.height ? p.y + 40 : p.y - 46;
            }
            shipMarker.style.top = Math.Clamp(markerTop, 0, Math.Max(0, contentRect.height - 20));
        }

        private Vector2 Position(float x, float y)
        {
            float minX = -20, maxX = 110, minY = -20, maxY = 90;
            if (definition != null)
                foreach (var node in definition.Nodes)
                {
                    minX = Math.Min(minX, node.X - 10); maxX = Math.Max(maxX, node.X + 10);
                    minY = Math.Min(minY, node.Y - 10); maxY = Math.Max(maxY, node.Y + 10);
                }
            return new Vector2(70 + (x - minX) / (maxX - minX) * Math.Max(1, contentRect.width - 140),
                34 + (1 - (y - minY) / (maxY - minY)) * Math.Max(1, contentRect.height - 80));
        }

        private void Draw(MeshGenerationContext context)
        {
            if (journey?.Navigation == null || contentRect.width < 1) return;
            var n = journey.Navigation; var painter = context.painter2D;
            var ship = Position(n.X, n.Y);
            var target = definition.Find(journey.PlanetId);
            if (target != null && journey.Phase != JourneyPhase.Landed)
            {
                painter.strokeColor = new Color(.75f, .65f, .4f); painter.lineWidth = 2;
                painter.BeginPath(); painter.MoveTo(ship); painter.LineTo(Position(target.X, target.Y)); painter.Stroke();
            }
        }

        public static string DangerLabel(int danger) => danger == 1 ? "低" : danger == 2 ? "中" : "高";
    }
}
