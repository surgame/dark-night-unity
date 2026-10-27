using System;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>单宿主可替换的作者背景参考；制作窗口可保留冻结输入至地形首帧后再启动，换图退休旧缓存，不混用旧摘要。</summary>
    internal sealed class CaveBackgroundReference : IDisposable
    {
        private CaveBackgroundCache cache;
        private readonly CaveBackgroundStyle style;
        private readonly Material wall;
        private readonly Transform parent;
        private readonly CaveOutlineSettings outline;
        private readonly int decorationStartRow;
        private readonly bool surfaceSky;
        private readonly GridBounds localRegion;
        private readonly CaveForegroundCoverage coverage;
        private readonly Func<int, bool> knownPage;
        private readonly bool foregroundFirst;
        private BackgroundBakeDescriptor pendingSource;
        private GridBounds visible;
        internal bool Ready => cache?.Ready ?? false;
        internal bool BaseReady => cache?.BaseReady ?? false;
        internal TerrainPageProgress Progress => cache?.Progress ?? default;
        internal int BuildCount => cache?.BuildCount ?? 0;
        internal int BaseBuildCount => cache?.BaseBuildCount ?? 0;
        internal int DecorationBuildCount => cache?.DecorationBuildCount ?? 0;
        internal long UploadedBytes => cache?.UploadedBytes ?? 0;
        internal int ResidentPages => cache?.ResidentPages ?? 0;
        internal long ResidentBytes => cache?.ResidentBytes ?? 0;
        internal int OccludedPages => cache?.OccludedPages ?? 0;

        internal CaveBackgroundReference(BackgroundBakeDescriptor source, CaveBackgroundStyle style, Material wall,
            Transform parent, CaveOutlineSettings outline, int decorationStartRow, bool surfaceSky,
            GridBounds localRegion, CaveForegroundCoverage coverage, Func<int, bool> knownPage, bool foregroundFirst = false)
        {
            this.style = UnityEngine.Object.Instantiate(style);
            this.wall = wall; this.parent = parent; this.outline = outline;
            this.decorationStartRow = decorationStartRow; this.surfaceSky = surfaceSky;
            this.localRegion = localRegion; this.coverage = coverage; this.knownPage = knownPage;
            this.foregroundFirst = foregroundFirst;
            Replace(source);
        }

        internal void Replace(BackgroundBakeDescriptor source)
        {
            pendingSource = source ?? throw new ArgumentNullException(nameof(source));
            if (!foregroundFirst) { Start(); return; }
            cache?.Dispose(); cache = null;
        }
        internal bool Start()
        {
            if (pendingSource == null) return false;
            var next = new CaveBackgroundCache(pendingSource, style, wall, parent, outline, decorationStartRow,
                surfaceSky, localRegion, coverage, knownPage);
            cache?.Dispose(); cache = next; pendingSource = null; cache.SetVisible(visible);
            return true;
        }
        internal bool CanPresent(int key) => foregroundFirst || (cache?.CanPresent(key) ?? false);
        internal void SetVisible(GridBounds bounds) { visible = bounds; cache?.SetVisible(bounds); }
        internal void Tick(int maximumPages = 1) => cache?.Tick(maximumPages);
        internal void UpdateOcclusion() => cache?.UpdateOcclusion();
        public void Dispose()
        {
            cache?.Dispose(); pendingSource = null;
            if (Application.isPlaying) UnityEngine.Object.Destroy(style); else UnityEngine.Object.DestroyImmediate(style);
        }
    }
}
