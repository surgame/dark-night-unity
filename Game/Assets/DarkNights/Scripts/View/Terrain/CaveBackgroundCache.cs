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
    /// <summary>单预览拥有的静态背景页缓存；纯计算后台串行，主线程每帧最多上传一页，换图取消旧任务并释放资源。</summary>
    internal sealed class CaveBackgroundCache : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly Dictionary<int, CaveBackgroundPage> pages = new Dictionary<int, CaveBackgroundPage>();
        private readonly HashSet<int> visible = new HashSet<int>();
        private readonly Material wall;
        private readonly Transform parent;
        private readonly CaveBackgroundStyle style;
        private Task<ICaveBackgroundLayout> layoutTask;
        private Task<byte[][]> pageTask;
        private ICaveBackgroundLayout layout;
        private int pendingKey;
        private int clock;
        private bool disposed;
        public int BuildCount { get; private set; }
        public long UploadedBytes { get; private set; }
        public int ResidentPages => pages.Count;
        public bool Ready => !disposed && layout != null && visible.All(pages.ContainsKey);

        public CaveBackgroundCache(BackgroundBakeDescriptor source, CaveBackgroundStyle style, Material wall, Transform parent, CaveOutlineSettings outline = null)
        {
            if (source == null) throw new InvalidOperationException("静态背景缺少初始参考；禁止从当前格子重建。");
            if (style.ContentHash != BackgroundBakeDescriptor.StyleContentHash) throw new InvalidOperationException("静态背景样式内容身份不匹配。");
            this.style = UnityEngine.Object.Instantiate(style); this.wall = wall; this.parent = parent;
            var generator = style.CaptureGenerator(); var modifiers = style.CaptureModifiers();
            var token = cancellation.Token;
            layoutTask = Task.Run<ICaveBackgroundLayout>(() => new ModifiedBackgroundLayout(
                generator.Build(source, outline, token.ThrowIfCancellationRequested), modifiers, source.LayoutSeed, token.ThrowIfCancellationRequested), token);
        }
        public void SetVisible(GridBounds bounds)
        {
            visible.Clear();
            int left = Math.Max(0, bounds.MinU / 32), right = Math.Min(9, ((int)bounds.MaxUExclusive - 1) / 32);
            int top = Math.Max(0, (1 - (int)bounds.MaxVExclusive) / 32), bottom = Math.Min(5, -bounds.MinV / 32);
            for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++) visible.Add(y * 10 + x);
            foreach (var entry in pages)
            {
                bool show = visible.Contains(entry.Key); entry.Value.Show(show);
                if (show) entry.Value.LastUsed = ++clock;
            }
        }
        public void Tick()
        {
            if (disposed) return;
            if (layout == null)
            {
                if (!layoutTask.IsCompleted) return;
                layout = layoutTask.GetAwaiter().GetResult(); layoutTask = null;
            }
            if (pageTask != null)
            {
                if (!pageTask.IsCompleted) return;
                byte[][] pixels = pageTask.GetAwaiter().GetResult(); pageTask = null;
                var page = new CaveBackgroundPage(pendingKey % 10, pendingKey / 10, pixels, wall, parent, style) { LastUsed = ++clock };
                page.Show(visible.Contains(pendingKey)); pages.Add(pendingKey, page);
                BuildCount++; UploadedBytes += 256 * 256 * 4 * 3;
                Trim();
                return;
            }
            foreach (int key in visible.OrderBy(k => k))
            {
                if (pages.ContainsKey(key)) continue;
                pendingKey = key;
                var token = cancellation.Token; var source = layout; int softness = style.MiddleSoftness;
                pageTask = Task.Run(() => BackgroundPageBaker.Bake(source, key % 10 * 256, key / 10 * 256, 256, 256, softness, token.ThrowIfCancellationRequested), token);
                break;
            }
        }
        private void Trim()
        {
            // 24 cached pages normally; a full-map debug camera may show all 60, still a fixed 45 MiB GPU ceiling.
            foreach (int key in pages.Where(p => !visible.Contains(p.Key)).OrderBy(p => p.Value.LastUsed).Select(p => p.Key).ToArray())
            {
                if (pages.Count <= Math.Max(24, visible.Count)) break;
                pages[key].Dispose(); pages.Remove(key);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; cancellation.Cancel(); cancellation.Dispose();
            foreach (var page in pages.Values) page.Dispose();
            pages.Clear(); visible.Clear(); layout = null; pageTask = null; layoutTask = null;
            UnityEngine.Object.Destroy(style);
        }
    }
}
