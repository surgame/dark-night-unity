using DarkNights.Core.ViewData;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DarkNights.View
{
    /// <summary>
    /// 复刻原小地图的像素几何，实体位置来自只读投影，视窗与点击仅控制本机镜头。
    /// 编辑态保留地形底图；未绑定或未 Ready 时不接收定位，断开后不持有旧世界。
    /// </summary>
    public sealed class CampMap : UiShapeGraphic, IPointerDownHandler
    {
        [SerializeField] private Color sky = new Color32(12, 25, 31, 255);
        [SerializeField] private Color ground = new Color32(37, 60, 53, 255);
        [SerializeField] private Color forest = new Color32(52, 81, 69, 255);
        [SerializeField] private Color groundLine = new Color32(113, 131, 94, 255);
        private SessionViewData frame;
        private PinewatchStage stage;
        private float worldWidth;
        private bool ready;

        public void Present(SessionViewData value, PinewatchStage scene, float width, bool canFocus)
        {
            frame = value; stage = scene; worldWidth = width; ready = canFocus;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float width = rectTransform.rect.width, height = rectTransform.rect.height;
            Box(mesh, new Rect(0, 0, width, height), sky);
            Box(mesh, new Rect(0, height * .53f, width, height * .47f), ground);
            for (int i = 0; i < 20; i++)
                Line(mesh, new Vector2(width * i / 20, height * .54f), new Vector2(width * i / 20 + 3, height * .27f + i % 3 * 4), forest, 4);
            Line(mesh, new Vector2(0, height * .75f), new Vector2(width, height * .75f), groundLine, 1);
            if (frame == null || stage == null) return;
            float ratio = width / Mathf.Max(1, worldWidth);
            foreach (BuildingViewData building in frame.World.Buildings)
                Box(mesh, new Rect(building.X * ratio - 3, height * .75f - 10, 6, 10), building.Progress >= 1
                    ? new Color32(188, 166, 120, 255) : new Color32(111, 128, 110, 255));
            foreach (ActorViewData actor in frame.World.Actors)
                Ellipse(mesh, new Vector2(actor.X * ratio, height * .79f), Vector2.one * 1.8f,
                    actor.Enemy ? new Color32(210, 126, 107, 255) : actor.Kind == "worker"
                        ? new Color32(218, 202, 153, 255) : new Color32(140, 189, 197, 255));
            float half = Screen.width / stage.Zoom * .5f;
            Border(mesh, new Rect((stage.CameraX - half) * ratio, 2, half * 2 * ratio, height - 4), new Color(.8f, .85f, .7f, .65f), 1);
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!ready || frame == null || data.button != PointerEventData.InputButton.Left) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out Vector2 point))
                stage.Focus((point.x - rectTransform.rect.xMin) / rectTransform.rect.width * worldWidth);
        }
    }
}
