using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 在已有 Image 上绘制原主题圆角和内边框，并同步显式绑定的按钮文字状态色。
    /// 状态仅属于本地指针／可交互性；重入清除按压状态，不改变布局或生成绑定的对象身份。
    /// </summary>
    [ExecuteAlways]
    public sealed class NativePanelTheme : BaseMeshEffect, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private NativePanelStyle normal, hover, pressed, disabled;
        [SerializeField] private Text label;
        [SerializeField] private Color normalText = Color.white, hoverText = Color.white,
            pressedText = Color.white, disabledText = Color.gray;
        private bool inside, down;
        private bool interactable = true;

        private int State => button == null ? 0 : !button.IsInteractable() ? 3 :
            inside && down ? 2 : inside ? 1 : 0;

        public void Configure(Button target, NativePanelStyle normalStyle, NativePanelStyle hoverStyle,
            NativePanelStyle pressedStyle, NativePanelStyle disabledStyle)
        {
            button = target;
            normal = normalStyle;
            hover = hoverStyle;
            pressed = pressedStyle;
            disabled = disabledStyle;
            if (button != null) button.transition = Selectable.Transition.None;
            Refresh();
        }

        public void ConfigureText(Text target, Color normalColor, Color hoverColor,
            Color pressedColor, Color disabledColor)
        {
            label = target;
            normalText = normalColor;
            hoverText = hoverColor;
            pressedText = pressedColor;
            disabledText = disabledColor;
            Refresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            inside = down = false;
            Refresh();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Refresh();
        }
#endif

        private void Update()
        {
            bool next = button == null || button.IsInteractable();
            if (next == interactable) return;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData data) { inside = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { inside = false; Refresh(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || button == null || !button.IsInteractable()) return;
            down = true;
            Refresh();
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            down = false;
            Refresh();
        }

        private void Refresh()
        {
            interactable = button == null || button.IsInteractable();
            if (!interactable) down = false;
            if (label != null) label.color = State switch
            {
                3 => disabledText, 2 => pressedText, 1 => hoverText, _ => normalText
            };
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            NativePanelStyle style = State switch
            {
                3 => disabled, 2 => pressed, 1 => hover, _ => normal
            };
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
