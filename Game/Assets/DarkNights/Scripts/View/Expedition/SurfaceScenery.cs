using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>
    /// 单地图近景装饰的只读表现；从持久作者布局捕获稳定锚点，仅绘制可见且已确认支撑的条目。
    /// 未知格隐藏，已破坏支撑不会因重新订阅重抽；换图／epoch由原TerrainPreview统一退休。
    /// </summary>
    public sealed class SurfaceScenery : IDisposable
    {
        private readonly Transform parent;
        private readonly SurfaceSceneryPlacement[] placements = Array.Empty<SurfaceSceneryPlacement>();
        private readonly Dictionary<(Texture2D, int), SurfaceSceneryBatch> batches =
            new Dictionary<(Texture2D, int), SurfaceSceneryBatch>();
        private readonly SurfaceSceneryBatch[] placementBatches = Array.Empty<SurfaceSceneryBatch>();
        private readonly bool[] visible = Array.Empty<bool>();
        public int VisibleCount { get; private set; }

        public SurfaceScenery(Transform parent, SurfaceEnvironmentSettings settings, BackgroundBakeDescriptor reference)
        {
            this.parent = parent;
            if (settings?.Enabled != true || reference == null) return;
            var layout = SurfaceSceneryLayout.Find(settings, reference);
            if (layout == null) return;
            if (settings.DecorationMaterial == null) throw new InvalidOperationException("逐图装饰缺少显式受光材质。");
            placements = layout.Capture().Where(p => !SurfaceSceneryLayout.IsGroundTrim(p) &&
                SurfaceSceneryLayout.IsOutdoor(p, reference)).ToArray();
            visible = new bool[placements.Length];
            var groups = new HashSet<(Texture2D, int)>();
            foreach (var p in placements) groups.Add((p.Texture, p.Order));
            if (groups.Count > 32) throw new InvalidOperationException("逐图装饰最多32种贴图／排序组合。");
            placementBatches = new SurfaceSceneryBatch[placements.Length];
            for (int i = 0; i < placements.Length; i++)
            {
                var p = placements[i];
                var key = (p.Texture, p.Order);
                if (!batches.TryGetValue(key, out var batch))
                {
                    batches.Add(key, batch = new SurfaceSceneryBatch(parent, p.Texture, p.Order, settings.DecorationMaterial));
                }
                placementBatches[i] = batch;
            }
        }

        public bool Render(Camera camera, CaveVisualSource source, bool confirmed, GridBounds loaded)
        {
            if (camera == null || source == null || placements.Length == 0) return false;
            Vector3 center = parent.InverseTransformPoint(camera.transform.position);
            float half = camera.orthographicSize / Mathf.Abs(parent.lossyScale.y);
            float width = half * camera.aspect;
            bool changed = false;
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                var p = placements[i];
                bool next = confirmed && p.Enabled && Mathf.Abs(p.Column - center.x) < width + 8 &&
                    -p.Row < center.y + half + 2 && -p.Row > center.y - half - 12 && Supported(p, source, loaded);
                changed |= next != visible[i]; visible[i] = next;
                if (next) count++;
            }
            if (!changed) return false;
            VisibleCount = count;
            foreach (var batch in batches.Values) batch.Clear();
            for (int i = 0; i < placements.Length; i++)
                if (visible[i]) placementBatches[i].Add(placements[i]);
            foreach (var batch in batches.Values) batch.Upload();
            return true;
        }

        private static bool Supported(SurfaceSceneryPlacement p, CaveVisualSource source, GridBounds loaded)
        {
            if (!loaded.IsValid || p.Column - p.SupportRadius < loaded.MinU ||
                p.Column + p.SupportRadius >= loaded.MaxUExclusive || -p.Row < loaded.MinV ||
                -p.Row + 1 >= loaded.MaxVExclusive) return false;
            for (int x = p.Column - p.SupportRadius; x <= p.Column + p.SupportRadius; x++)
            {
                byte cell = source.LightingCell(x, p.Row);
                if ((cell & 192) != 192) return false;
                if (x == p.Column && (cell & 15) != p.Shape) return false;
                byte above = source.LightingCell(x, p.Row - 1);
                if ((above & 128) == 0 || (above & 64) != 0) return false;
            }
            return true;
        }

        public void Dispose()
        {
            foreach (var batch in batches.Values) batch.Dispose();
            batches.Clear();
        }
    }
}
