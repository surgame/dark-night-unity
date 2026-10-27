using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>单宿主静态背景缓存；后台串行且批次有界，默认每帧一页，制作预览可在4ms预算内上传至多四页。</summary>
    internal sealed class CaveBackgroundCache : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Dictionary<int, CaveBackgroundPage> pages = new Dictionary<int, CaveBackgroundPage>();
        private readonly HashSet<int> visible = new HashSet<int>();
        private readonly Material wall;
        private readonly Transform parent;
        private readonly CaveBackgroundStyle style;
        private readonly bool requiresBase;
        private readonly CaveForegroundCoverage coverage;
        private readonly Func<int, bool> knownPage;
        private CaveEntranceBackdrop entrance;
        private Task<ICaveBackgroundLayout> layoutTask;
        private Task<(int Key, bool Base, byte[][] Pixels)[]> pageTask;
        private readonly Queue<(int Key, bool Base, byte[][] Pixels)> completedPages =
            new Queue<(int Key, bool Base, byte[][] Pixels)>();
        private readonly HashSet<(int Key, bool Base)> queuedPages = new HashSet<(int Key, bool Base)>();
        private ICaveBackgroundLayout layout;
        private int clock;
        private long coverageRevision = -1;
        private bool disposed;
        public int BuildCount { get; private set; }
        public int BaseBuildCount { get; private set; }
        public int DecorationBuildCount { get; private set; }
        public long UploadedBytes { get; private set; }
        public double UploadMilliseconds { get; private set; }
        public double PeakUploadMilliseconds { get; private set; }
        public int ResidentPages => pages.Count;
        public long ResidentBytes => pages.Values.Sum(p => p.Bytes);
        public int OccludedPages => visible.Count(Covered);
        public bool BaseReady => !disposed && layout != null && visible.All(CanPresent);
        public bool Ready => BaseReady && visible.All(k => !NeedsDecorations(k) ||
            (pages.TryGetValue(k, out var page) && page.HasDecorations));
        internal TerrainPageProgress Progress
        {
            get
            {
                if (layout == null) return default;
                int completed = 0, total = 0;
                foreach (int key in visible)
                {
                    pages.TryGetValue(key, out var page);
                    if (requiresBase) { total++; if (page?.HasBase == true) completed++; }
                    if (NeedsDecorations(key)) { total++; if (page?.HasDecorations == true) completed++; }
                }
                return new TerrainPageProgress(completed, total);
            }
        }

        public bool CanPresent(int key) => !requiresBase || (pages.TryGetValue(key, out var page) && page.HasBase);

        public CaveBackgroundCache(BackgroundBakeDescriptor source, CaveBackgroundStyle style, Material wall, Transform parent,
            CaveOutlineSettings outline = null, int decorationStartRow = 43, bool surfaceSky = false, GridBounds localRegion = default,
            CaveForegroundCoverage coverage = null, Func<int, bool> knownPage = null)
        {
            if (source == null) throw new InvalidOperationException("静态背景缺少初始参考；禁止从当前格子重建。");
            if (style.ContentHash != BackgroundBakeDescriptor.StyleContentHash) throw new InvalidOperationException("静态背景样式内容身份不匹配。");
            this.style = UnityEngine.Object.Instantiate(style); this.wall = wall; this.parent = parent;
            requiresBase = surfaceSky; this.coverage = coverage; this.knownPage = knownPage;
            bool withEntrance = surfaceSky;
            int rimReach = style.EntranceRimReach * 8, rearDepth = style.EntranceRearDepth * 8;
            var generator = style.CaptureGenerator(); var modifiers = style.CaptureModifiers();
            if (localRegion.IsValid && (!(generator is ContourBackgroundGenerator) || surfaceSky))
                throw new NotSupportedException("局部调参预览只支持当前洞穴轮廓背景，不回退整图求解。");
            var token = cancellation.Token;
            layoutTask = Task.Run<ICaveBackgroundLayout>(() =>
            {
                if (localRegion.IsValid)
                {
                    var contour = (ContourBackgroundGenerator)generator;
                    int halo = ((contour.RegionHalo + BackgroundRegionLayout.RequiredHalo(modifiers) + 7) / 8) * 8;
                    int targetLeft = localRegion.MinU * 8, targetTop = Math.Max(0, (1 - (int)localRegion.MaxVExclusive) * 8);
                    int targetRight = (int)localRegion.MaxUExclusive * 8, targetBottom = Math.Min(source.Height * 8, (1 - localRegion.MinV) * 8);
                    int left = Math.Max(0, targetLeft - halo), top = Math.Max(0, targetTop - halo);
                    int right = Math.Min(source.Width * 8, targetRight + halo);
                    int bottom = Math.Min(source.Height * 8, targetBottom + halo);
                    bottom = Math.Min(source.Height * 8, (bottom + 7) / 8 * 8);
                    var local = contour.BuildRegion(source, left, top, right - left, bottom - top, outline,
                        token.ThrowIfCancellationRequested, decorationStartRow);
                    return new BackgroundRegionLayout(local, left, top, targetLeft, targetTop, targetRight - targetLeft, targetBottom - targetTop,
                        source.Width * 8, source.Height * 8, decorationStartRow * 8, modifiers, source.LayoutSeed, token.ThrowIfCancellationRequested);
                }
                var result = new ModifiedBackgroundLayout(generator.Build(source, outline, token.ThrowIfCancellationRequested, decorationStartRow),
                    modifiers, source.LayoutSeed, token.ThrowIfCancellationRequested);
                if (withEntrance) entrance = new CaveEntranceBackdrop(source, result, rimReach, rearDepth, token.ThrowIfCancellationRequested);
                return result;
            }, token);
        }
        public void SetVisible(GridBounds bounds)
        {
            visible.Clear();
            if (!bounds.IsValid)
            {
                foreach (var page in pages.Values) page.Show(false);
                Trim();
                return;
            }
            int left = Math.Max(0, bounds.MinU / 32), right = Math.Min(9, ((int)bounds.MaxUExclusive - 1) / 32);
            int top = Math.Max(0, (1 - (int)bounds.MaxVExclusive) / 32), bottom = Math.Min(5, -bounds.MinV / 32);
            for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++) visible.Add(y * 10 + x);
            RefreshVisibility();
            Trim();
        }
        public void UpdateOcclusion()
        {
            if (coverage == null || coverageRevision == coverage.Revision) return;
            coverageRevision = coverage.Revision; RefreshVisibility();
        }
        private bool Covered(int key) => coverage != null && (knownPage?.Invoke(key) ?? true) && coverage.FullyOpaque(key);
        private bool NeedsDecorations(int key) => (style.Near || style.Middle || style.Deep) &&
            (coverage == null || (coverage.HasPage(key) && (knownPage?.Invoke(key) ?? true) && !coverage.FullyOpaque(key)));
        private void RefreshVisibility()
        {
            foreach (var entry in pages)
            {
                bool inView = visible.Contains(entry.Key);
                entry.Value.Show(inView && !Covered(entry.Key));
                if (inView) entry.Value.LastUsed = ++clock;
            }
        }
        public void Tick(int maximumPages = 1)
        {
            if (disposed) return;
            maximumPages = Math.Max(1, Math.Min(4, maximumPages));
            UpdateOcclusion();
            if (layout == null)
            {
                if (!layoutTask.IsCompleted) return;
                layout = layoutTask.GetAwaiter().GetResult(); layoutTask = null;
            }
            if (pageTask != null)
            {
                if (pageTask.IsCompleted)
                {
                    foreach (var result in pageTask.GetAwaiter().GetResult()) completedPages.Enqueue(result);
                    pageTask = null;
                }
            }
            var uploadWatch = System.Diagnostics.Stopwatch.StartNew();
            for (int uploaded = 0; completedPages.Count > 0 && uploaded < maximumPages; uploaded++)
            {
                var result = completedPages.Dequeue();
                int key = result.Key;
                queuedPages.Remove((key, result.Base));
                if (!result.Base && (!visible.Contains(key) || !NeedsDecorations(key))) continue;
                pages.TryGetValue(key, out var page);
                long previous = page?.Bytes ?? 0;
                if (page == null)
                {
                    page = new CaveBackgroundPage(key % 10, key / 10, result.Pixels, wall, parent, style, result.Base ? 3 : 0);
                    pages.Add(key, page);
                }
                else page.InstallDecorations(result.Pixels);
                page.LastUsed = ++clock;
                page.Show(visible.Contains(key) && !Covered(key));
                BuildCount++; UploadedBytes += page.Bytes - previous;
                if (result.Base) BaseBuildCount++; else DecorationBuildCount++;
                Trim();
                if (uploadWatch.Elapsed.TotalMilliseconds >= 4) break;
            }
            double uploadTime = uploadWatch.Elapsed.TotalMilliseconds;
            UploadMilliseconds += uploadTime;
            PeakUploadMilliseconds = Math.Max(PeakUploadMilliseconds, uploadTime);
            if (pageTask != null || completedPages.Count >= maximumPages) return;
            int[] keys = visible.OrderBy(k => k).ToArray();
            var batch = keys.Where(k => !CanPresent(k)).Select(k => (Key: k, Base: true))
                .Concat(keys.Where(k => CanPresent(k) && NeedsDecorations(k) &&
                    (!pages.TryGetValue(k, out var page) || !page.HasDecorations)).Select(k => (Key: k, Base: false)))
                .Where(p => !queuedPages.Contains(p)).Take(maximumPages).ToArray();
            if (batch.Length != 0) StartPages(batch);
        }
        private void StartPages((int Key, bool Base)[] batch)
        {
            foreach (var page in batch) queuedPages.Add(page);
            var token = cancellation.Token; var source = layout; int softness = style.MiddleSoftness;
            pageTask = Task.Run(() =>
            {
                var results = new (int Key, bool Base, byte[][] Pixels)[batch.Length];
                for (int i = 0; i < batch.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    int key = batch[i].Key;
                    byte[][] pixels = batch[i].Base ?
                        new[] { entrance.BakeBase(key % 10 * 256, key / 10 * 256, 256, 256, token.ThrowIfCancellationRequested) } :
                        BackgroundPageBaker.Bake(source, key % 10 * 256, key / 10 * 256, 256, 256, softness, token.ThrowIfCancellationRequested);
                    results[i] = (key, batch[i].Base, pixels);
                }
                return results;
            }, token);
        }
        private void Trim()
        {
            // 基础全图最多15 MiB；预算为24 MiB或已备基础加当前视野装饰，避免往返等待基础重建。
            long baseBytes = pages.Values.Count(p => p.HasBase) * 256L * 256 * 4;
            long visibleBytes = pages.Where(p => visible.Contains(p.Key)).Sum(p => p.Value.Bytes - (p.Value.HasBase ? 256L * 256 * 4 : 0));
            long limit = Math.Max(24L * 1024 * 1024, baseBytes + visibleBytes);
            foreach (int key in pages.Where(p => !visible.Contains(p.Key)).OrderBy(p => p.Value.LastUsed).Select(p => p.Key).ToArray())
            {
                if (ResidentBytes <= limit) break;
                if (requiresBase) pages[key].DropDecorations();
                else { pages[key].Dispose(); pages.Remove(key); }
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; cancellation.Cancel(); cancellation.Dispose();
            foreach (var page in pages.Values) page.Dispose();
            Observe(pageTask); Observe(layoutTask);
            pages.Clear(); visible.Clear(); completedPages.Clear(); queuedPages.Clear(); layout = null; pageTask = null; layoutTask = null;
            DestroyOwned(style);
        }
        private static void Observe(Task task)
        {
            if (task != null) _ = task.ContinueWith(t => { var observed = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
        private static void DestroyOwned(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
