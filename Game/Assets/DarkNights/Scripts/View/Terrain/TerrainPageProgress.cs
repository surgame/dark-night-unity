using AnyRules.Next;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>当前视野、当前输入版本的分页完成量；未知总量与零工作量分开，不使用宿主累计构建次数推算进度。</summary>
    public readonly struct TerrainPageProgress
    {
        public int Completed { get; }
        public int Total { get; }
        public bool Known { get; }
        public bool Finished => Known && Completed == Total;
        public float Percent => !Known ? 0 : Total == 0 ? 100 : 100f * Completed / Total;

        internal TerrainPageProgress(int completed, int total, bool known = true)
        { Completed = completed; Total = total; Known = known; }

        internal static TerrainPageProgress Capture(ARDMapController controller, GridBounds visible)
        {
            if (controller?.Renderer == null || !visible.IsValid) return default;
            int size = controller.Descriptor.PageSize, completed = 0, total = 0;
            // DualGrid前景边界还依赖左／下侧一格，与原生ShowRegion的页范围一致。
            int left = GridMath.FloorDiv(visible.MinU - 1, size), bottom = GridMath.FloorDiv(visible.MinV - 1, size);
            int right = GridMath.FloorDiv((int)visible.MaxUExclusive - 1, size);
            int top = GridMath.FloorDiv((int)visible.MaxVExclusive - 1, size);
            for (int v = bottom; v <= top; v++) for (int u = left; u <= right; u++)
            {
                total++;
                var page = new PageCoord(u, v);
                if (controller.Renderer.IsExternallyPresented(page) ||
                    controller.TryGetPageInfo(page, out var info) && info.Status == PagePresentationStatus.Presented &&
                    info.PresentedInputRevision == info.InputRevision) completed++;
            }
            return new TerrainPageProgress(completed, total);
        }
    }
}
