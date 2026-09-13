using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 小地图与状态叠层共用的 UGUI 几何绘制，坐标以控件左上角为原点，颜色由各原生组件维护。
    /// 只追加当前帧网格，不缓存或改变游戏状态；遵循父 Canvas 的裁剪与生命周期。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public abstract class UiShapeGraphic : MaskableGraphic
    {
        protected void Box(VertexHelper mesh, Rect rect, Color tint)
        {
            int start = mesh.currentVertCount;
            Vertex(mesh, new Vector2(rect.xMin, rect.yMin), tint);
            Vertex(mesh, new Vector2(rect.xMax, rect.yMin), tint);
            Vertex(mesh, new Vector2(rect.xMax, rect.yMax), tint);
            Vertex(mesh, new Vector2(rect.xMin, rect.yMax), tint);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        protected void Border(VertexHelper mesh, Rect rect, Color tint, float width)
        {
            Line(mesh, rect.min, new Vector2(rect.xMax, rect.yMin), tint, width);
            Line(mesh, new Vector2(rect.xMax, rect.yMin), rect.max, tint, width);
            Line(mesh, rect.max, new Vector2(rect.xMin, rect.yMax), tint, width);
            Line(mesh, new Vector2(rect.xMin, rect.yMax), rect.min, tint, width);
        }

        protected void Line(VertexHelper mesh, Vector2 from, Vector2 to, Color tint, float width)
        {
            Vector2 edge = to - from;
            Vector2 normal = new Vector2(-edge.y, edge.x).normalized * width * 0.5f;
            int start = mesh.currentVertCount;
            Vertex(mesh, from + normal, tint); Vertex(mesh, to + normal, tint);
            Vertex(mesh, to - normal, tint); Vertex(mesh, from - normal, tint);
            mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
        }

        protected void Ellipse(VertexHelper mesh, Vector2 center, Vector2 radius, Color tint, float stroke = 0)
        {
            int segments = stroke > 0 ? 19 : 20;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Vector2 p = center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
                Vector2 q = center + new Vector2(Mathf.Cos(b) * radius.x, Mathf.Sin(b) * radius.y);
                if (stroke > 0)
                {
                    Vector2 edge = new Vector2(Mathf.Cos(b) - Mathf.Cos(a), Mathf.Sin(b) - Mathf.Sin(a));
                    Vector2 normal = new Vector2(-edge.y, edge.x).normalized * stroke * .5f;
                    normal.y *= radius.y / radius.x;
                    int start = mesh.currentVertCount;
                    Vertex(mesh, p + normal, tint); Vertex(mesh, q + normal, tint);
                    Vertex(mesh, q - normal, tint); Vertex(mesh, p - normal, tint);
                    mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
                }
                else
                {
                    int start = mesh.currentVertCount;
                    Vertex(mesh, center, tint); Vertex(mesh, p, tint); Vertex(mesh, q, tint);
                    mesh.AddTriangle(start, start + 1, start + 2);
                }
            }
        }

        private void Vertex(VertexHelper mesh, Vector2 point, Color tint)
        {
            Rect bounds = rectTransform.rect;
            mesh.AddVert(new Vector3(bounds.xMin + point.x, bounds.yMax - point.y), VertexTint(point, tint), Vector2.zero);
        }

        protected virtual Color VertexTint(Vector2 point, Color tint) => tint;
    }
}
