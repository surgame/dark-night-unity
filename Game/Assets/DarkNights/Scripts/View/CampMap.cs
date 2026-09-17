using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
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
        private IReadOnlyGrid terrain;
        private int[] terrainSurface;
        private ulong terrainCommit;
        private float worldWidth;
        private bool ready;

        public void Present(SessionViewData value, PinewatchStage scene, float width, bool canFocus, IReadOnlyGrid terrainSource = null)
        {
            frame = value; stage = scene; worldWidth = width; ready = canFocus;
            if (!ReferenceEquals(terrain, terrainSource) || terrainSource != null && terrainCommit != terrainSource.CommitId)
            {
                terrain = terrainSource;
                terrainCommit = terrainSource?.CommitId ?? 0;
                terrainSurface = terrainSource == null ? null : CaptureTerrainProfile(terrainSource);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float width = rectTransform.rect.width, height = rectTransform.rect.height;
            Box(mesh, new Rect(0, 0, width, height), sky);
            if (terrainSurface == null) DrawFixedBackdrop(mesh, width, height);
            else DrawTerrain(mesh, width, height);
            if (frame == null || stage == null) return;
            float ratio = width / Mathf.Max(1, worldWidth);
            float groundY = terrainSurface == null ? height * .75f : TerrainY(PlayableTerrain.CampRow, height);
            foreach (BuildingViewData building in frame.World.Buildings)
                Box(mesh, new Rect(building.X * ratio - 3, groundY - 10, 6, 10), building.Progress >= 1
                    ? new Color32(188, 166, 120, 255) : new Color32(111, 128, 110, 255));
            foreach (ActorViewData actor in frame.World.Actors)
                Ellipse(mesh, new Vector2(actor.X * ratio, terrainSurface == null ? height * .79f :
                    TerrainY((PlayableTerrain.OriginY - actor.Height) / PlayableTerrain.CellPixels + .5f, height) - 2), Vector2.one * 1.8f,
                    actor.Enemy ? new Color32(210, 126, 107, 255) : actor.Kind == "worker"
                        ? new Color32(218, 202, 153, 255) : new Color32(140, 189, 197, 255));
            float half = Screen.width / stage.Zoom * .5f;
            Border(mesh, new Rect((stage.CameraX - half) * ratio, 2, half * 2 * ratio, height - 4), new Color(.8f, .85f, .7f, .65f), 1);
        }

        private void DrawFixedBackdrop(VertexHelper mesh, float width, float height)
        {
            Box(mesh, new Rect(0, height * .53f, width, height * .47f), ground);
            for (int i = 0; i < 20; i++)
                Line(mesh, new Vector2(width * i / 20, height * .54f), new Vector2(width * i / 20 + 3,
                    height * .27f + i % 3 * 4), forest, 4);
            Line(mesh, new Vector2(0, height * .75f), new Vector2(width, height * .75f), groundLine, 1);
        }

        private void DrawTerrain(VertexHelper mesh, float width, float height)
        {
            Vector2 previous = default;
            bool connected = false;
            for (int i = 0; i < terrainSurface.Length; i++)
            {
                int row = terrainSurface[i];
                if (row < 0) { connected = false; continue; }
                float x0 = width * i / terrainSurface.Length;
                float x1 = width * (i + 1) / terrainSurface.Length;
                float y = TerrainY(row, height);
                Box(mesh, new Rect(x0, y, Mathf.Max(1, x1 - x0), height - y), ground);
                var point = new Vector2((x0 + x1) * .5f, y);
                if (connected) Line(mesh, previous, point, groundLine, 1);
                previous = point; connected = true;
            }
            float campEnd = width * PlayableTerrain.CampColumns / TerrainGenerationSettings.Width;
            Line(mesh, new Vector2(campEnd, 2), new Vector2(campEnd, height - 2), new Color(forest.r, forest.g, forest.b, .7f), 1);
        }

        private static float TerrainY(float row, float height) =>
            2 + Mathf.Clamp01(row / (TerrainGenerationSettings.Height - 1f)) * (height - 4);

        /// <summary>从完整只读地图提取每列最高实体格，供小地图绘制全局地表轮廓。</summary>
        public static int[] CaptureTerrainProfile(IReadOnlyGrid map)
        {
            if (map == null) throw new System.ArgumentNullException(nameof(map));
            var result = new int[TerrainGenerationSettings.Width];
            for (int x = 0; x < result.Length; x++)
            {
                result[x] = -1;
                for (int y = 0; y < TerrainGenerationSettings.Height; y++)
                {
                    GridSample sample = map.Read(new CellCoord(x, -y));
                    if (!sample.TryGetCell(out GridCell cell) || cell.IsEmpty) continue;
                    result[x] = y;
                    break;
                }
            }
            return result;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!ready || frame == null || data.button != PointerEventData.InputButton.Left) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out Vector2 point))
                stage.Focus((point.x - rectTransform.rect.xMin) / rectTransform.rect.width * worldWidth);
        }
    }
}
