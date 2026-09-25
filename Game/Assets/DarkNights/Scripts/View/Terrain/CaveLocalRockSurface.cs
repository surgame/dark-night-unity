using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace DarkNights.View.Terrain
{
    /// <summary>热编辑的有限像素补丁优先于冷页；显示版本单调前进，最终就绪只接受当前输入版本。</summary>
    internal sealed class CaveLocalRockSurface : IDisposable
    {
        private sealed class Patch
        {
            internal int Key, Version;
            internal RectInt Rect;
            internal Func<byte[]> Bake;
            internal byte[] Pixels;
        }
        private readonly RenderTexture texture;
        private readonly Texture2D upload;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly HashSet<int> visible = new HashSet<int>(), interactive = new HashSet<int>();
        private readonly Dictionary<int, RectInt> dirty = new Dictionary<int, RectInt>();
        private readonly int[] versions = new int[60], bakedVersions = new int[60];
        private readonly CaveLocalRockGeometry geometry;
        private readonly double inlineBudget;
        private CancellationTokenSource workCancellation;
        private Task<Patch[]> task;
        private int generation, workGeneration;
        private bool disposed, workIsInteractive;
        private Exception fault;
        public int BuildCount { get; private set; }
        public long UploadedBytes { get; private set; }
        public long DiscardedBatches { get; private set; }
        public long InlineBatches { get; private set; }
        public bool Ready => fault == null && geometry.Initialized && task == null &&
            visible.All(k => bakedVersions[k] == versions[k]);
        public Exception Fault => fault;

        public CaveLocalRockSurface(Material material, string seed, CaveTerrainStyle style)
        {
            inlineBudget = Math.Max(0, Math.Min(8, style.InteractiveBakeBudgetMs));
            geometry = new CaveLocalRockGeometry(seed, style.CaptureOutline(), style.CaptureModifiers(), style.StoneSize, lifetime.Token);
            var descriptor = new RenderTextureDescriptor(2560, 1536, GraphicsFormat.R8G8B8A8_SRGB, 0)
            { msaaSamples = 1, useMipMap = false, autoGenerateMips = false };
            texture = new RenderTexture(descriptor)
            { name = "LocalV2 cave rock", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.Create();
            var previous = RenderTexture.active;
            RenderTexture.active = texture; GL.Clear(false, true, Color.clear); RenderTexture.active = previous;
            upload = new Texture2D(1, 1, TextureFormat.RGBA32, false, false) { name = "Cave patch staging" };
            material.SetTexture("_RockSurface", texture);
        }
        public void Replace(Color32[] cells)
        {
            geometry.Replace(cells); generation++; workCancellation?.Cancel();
            for (int key = 0; key < versions.Length; key++)
            { versions[key] = checked(versions[key] + 1); dirty[key] = CaveLocalRockGeometry.PageRect(key); }
        }
        public void ApplyChanges(Color32[] cells, IReadOnlyList<int> indexes)
        {
            if (!geometry.Initialized || indexes == null || indexes.Count == 0) return;
            geometry.ApplyCells(cells, indexes);
            var touched = new HashSet<int>();
            foreach (int index in indexes)
            {
                var affected = geometry.ChangedRect(index);
                var pages = new HashSet<int>(); geometry.AddAffectedPages(index, pages);
                foreach (int key in pages)
                {
                    var page = CaveLocalRockGeometry.PageRect(key);
                    var patch = Intersect(page, affected);
                    if (bakedVersions[key] == 0) patch = page;
                    dirty[key] = dirty.TryGetValue(key, out var previous) ? Union(previous, patch) : patch;
                    touched.Add(key); interactive.Add(key);
                }
            }
            foreach (int key in touched) versions[key] = checked(versions[key] + 1);
            // 只抢占冷页。每来一笔就取消热作业，会让连续绘制永远没有提交机会。
            if (!workIsInteractive) workCancellation?.Cancel();
        }
        public void SetVisible(GridBounds bounds)
        {
            if (!bounds.IsValid)
            { visible.Clear(); generation++; workCancellation?.Cancel(); return; }
            var next = new HashSet<int>();
            for (int y = Math.Max(0, (1 - (int)bounds.MaxVExclusive) / 32); y <= Math.Min(5, -bounds.MinV / 32); y++)
                for (int x = Math.Max(0, bounds.MinU / 32); x <= Math.Min(9, ((int)bounds.MaxUExclusive - 1) / 32); x++)
                    next.Add(y * 10 + x);
            visible.Clear(); foreach (int key in next) visible.Add(key);
        }
        public void Tick()
        {
            if (disposed || fault != null || !geometry.Initialized) return;
            try
            {
                if (task != null)
                {
                    if (!task.IsCompleted) return;
                    var completed = task; task = null;
                    bool cancelled = workCancellation.IsCancellationRequested;
                    workCancellation.Dispose(); workCancellation = null;
                    try
                    {
                        var result = completed.GetAwaiter().GetResult();
                        if (!cancelled && workGeneration == generation) Commit(result);
                        else DiscardedBatches++;
                    }
                    catch (OperationCanceledException) { DiscardedBatches++; }
                    workIsInteractive = false;
                }
                var keys = dirty.Keys.Where(visible.Contains).Where(k => bakedVersions[k] != versions[k]).OrderBy(k => k).ToArray();
                if (keys.Length == 0) return;
                var hot = keys.Where(interactive.Contains).ToArray();
                bool isHot = hot.Length != 0;
                if (isHot) keys = hot;
                else keys = keys.Take(2).ToArray();
                workCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                var token = workCancellation.Token;
                var clock = Stopwatch.StartNew();
                bool inline = isHot && inlineBudget > 0 && keys.All(k => bakedVersions[k] != 0) && keys.Length <= 4;
                var patches = new Patch[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    int key = keys[i]; var rect = dirty[key];
                    patches[i] = new Patch { Key = key, Version = versions[key], Rect = rect,
                        Bake = geometry.CaptureRegionBake(rect, () =>
                        {
                            token.ThrowIfCancellationRequested();
                            if (inline && clock.Elapsed.TotalMilliseconds >= inlineBudget) throw new TimeoutException("inline budget");
                        }) };
                }
                if (inline)
                {
                    try
                    {
                        BakeAll(patches, token); Commit(patches); InlineBatches++;
                        workCancellation.Dispose(); workCancellation = null;
                        return;
                    }
                    catch (TimeoutException) { foreach (var patch in patches) patch.Pixels = null; }
                }
                inline = false; workGeneration = generation; workIsInteractive = isHot;
                task = Task.Run(() => { BakeAll(patches, token); return patches; }, token);
            }
            catch (Exception error) { fault = error; throw; }
        }
        private static void BakeAll(Patch[] patches, CancellationToken token)
        {
            foreach (var patch in patches) { token.ThrowIfCancellationRequested(); patch.Pixels = patch.Bake(); }
        }
        private void Commit(Patch[] patches)
        {
            // 热批次允许单调前进的中间显示；绝不回退已显示版本，旧世界/撤权批次由 generation 拦截。
            foreach (var patch in patches)
                if (patch.Version < bakedVersions[patch.Key] || patch.Version > versions[patch.Key])
                { DiscardedBatches++; return; }
            foreach (var patch in patches)
            {
                if (!visible.Contains(patch.Key) || patch.Version <= bakedVersions[patch.Key]) continue;
                var rect = patch.Rect;
                if ((upload.width != rect.width || upload.height != rect.height) && !upload.Reinitialize(rect.width, rect.height))
                    throw new InvalidOperationException("局部上传纹理无法调整尺寸。");
                upload.LoadRawTextureData(patch.Pixels); upload.Apply(false, false);
                Graphics.CopyTexture(upload, 0, 0, 0, 0, rect.width, rect.height, texture, 0, 0, rect.x, rect.y);
                UploadedBytes += patch.Pixels.Length;
                bakedVersions[patch.Key] = patch.Version;
                if (patch.Version == versions[patch.Key])
                { dirty.Remove(patch.Key); interactive.Remove(patch.Key); }
                // 若输入又有变化，保留脏范围（包括未显示的后续变化），下一批继续追上。
                BuildCount++;
            }
        }
        private static RectInt Intersect(RectInt a, RectInt b)
        {
            int x = Math.Max(a.xMin, b.xMin), y = Math.Max(a.yMin, b.yMin);
            return new RectInt(x, y, Math.Min(a.xMax, b.xMax) - x, Math.Min(a.yMax, b.yMax) - y);
        }
        private static RectInt Union(RectInt a, RectInt b)
        {
            int x = Math.Min(a.xMin, b.xMin), y = Math.Min(a.yMin, b.yMin);
            return new RectInt(x, y, Math.Max(a.xMax, b.xMax) - x, Math.Max(a.yMax, b.yMax) - y);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; generation++; lifetime.Cancel(); workCancellation?.Cancel();
            if (task != null)
                _ = task.ContinueWith(completed => { var observed = completed.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            workCancellation?.Dispose(); lifetime.Dispose(); task = null;
            texture.Release(); DestroyOwned(texture); DestroyOwned(upload); dirty.Clear(); visible.Clear(); interactive.Clear();
        }
        private static void DestroyOwned(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
