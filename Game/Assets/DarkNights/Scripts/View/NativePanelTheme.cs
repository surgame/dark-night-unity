using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 在已有 Image 上绘制原主题圆角和内边框，保留 Graphic、Button 以及生成绑定的对象身份。
    /// 状态仅属于本地指针／可交互性；不改变 RectTransform，编辑器预览与运行共用同一网格。
    /// </summary>
    [ExecuteAlways]
    public sealed class NativePanelTheme : BaseMeshEffect, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private NativePanelStyle normal, hover, pressed, disabled;
        private bool inside, down;
        private bool interactable = true;

        public void Configure(Button target, NativePanelStyle normalStyle, NativePanelStyle hoverStyle,
            NativePanelStyle pressedStyle, NativePanelStyle disabledStyle)
        {
            button = target;
            normal = normalStyle;
            hover = hoverStyle;
            pressed = pressedStyle;
            disabled = disabledStyle;
            if (button != null) button.transition = Selectable.Transition.None;
            graphic.SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            inside = down = false;
            interactable = button == null || button.IsInteractable();
        }

        private void Update()
        {
            bool next = button == null || button.IsInteractable();
            if (next == interactable) return;
            interactable = next;
            if (!next) down = false;
            graphic.SetVerticesDirty();
        }

        public void OnPointerEnter(PointerEventData data) { inside = true; graphic.SetVerticesDirty(); }
        public void OnPointerExit(PointerEventData data) { inside = false; graphic.SetVerticesDirty(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            down = true;
            graphic.SetVerticesDirty();
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            down = false;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            NativePanelStyle style = button == null ? normal : !button.IsInteractable() ? disabled :
                inside && down ? pressed : inside ? hover : normal;
            Rect bounds = graphic.rectTransform.rect;
            float width = Mathf.Clamp(style.BorderWidth, 0, Mathf.Min(bounds.width, bounds.height) * .5f);
            float radius = Mathf.Clamp(style.Radius, 0, Mathf.Min(bounds.width, bounds.height) * .5f);
            mesh.Clear();
            if (width > 0) Ring(mesh, bounds, radius, width, style.Border);
            Rect inner = new Rect(bounds.x + width, bounds.y + width, bounds.width - width * 2, bounds.height - width * 2);
            Fill(mesh, inner, Mathf.Max(0, radius - width), style.Background);
        }

        private static Vector2 Point(Rect bounds, float radius, int index)
        {
            const int arcSteps = 6;
            int corner = index / (arcSteps + 1);
            float angle = (corner * 90 + index % (arcSteps + 1) * 90f / arcSteps) * Mathf.Deg2Rad;
            var center = new Vector2(corner == 0 || corner == 3 ? bounds.xMax - radius : bounds.xMin + radius,
                corner < 2 ? bounds.yMax - radius : bounds.yMin + radius);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static void Fill(VertexHelper mesh, Rect bounds, float radius, Color color)
        {
            if (bounds.width <= 0 || bounds.height <= 0 || color.a <= 0) return;
            int start = mesh.currentVertCount;
            mesh.AddVert(bounds.center, color, Vector2.zero);
            for (int i = 0; i < 28; i++) mesh.AddVert(Point(bounds, radius, i), color, Vector2.zero);
            for (int i = 0; i < 28; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 28);
        }

        private static void Ring(VertexHelper mesh, Rect bounds, float radius, float width, Color color)
        {
            var inner = new Rect(bounds.x + width, bounds.y + width, bounds.width - width * 2, bounds.height - width * 2);
            int start = mesh.currentVertCount;
            for (int i = 0; i < 28; i++)
            {
                mesh.AddVert(Point(bounds, radius, i), color, Vector2.zero);
                mesh.AddVert(Point(inner, Mathf.Max(0, radius - width), i), color, Vector2.zero);
            }
            for (int i = 0; i < 28; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % 28 * 2;
                mesh.AddTriangle(a, b, a + 1);
                mesh.AddTriangle(a + 1, b, b + 1);
            }
        }
    }
}
