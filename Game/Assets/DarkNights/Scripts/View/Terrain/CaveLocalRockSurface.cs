using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace DarkNights.View.Terrain
{
    /// <summary>当前岩壁的持久 GPU 纹理与有限页队列；作业冻结依赖格，过期页不提交，上传使用局部 staging 拷贝。</summary>
    internal sealed class CaveLocalRockSurface : IDisposable
    {
        private sealed class PageBake
        {
            internal readonly int Key, Version;
            internal readonly byte[] Pixels;
            internal PageBake(int key, int version, byte[] pixels) { Key = key; Version = version; Pixels = pixels; }
        }

        private readonly RenderTexture texture;
        private readonly Texture2D upload;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;
        private readonly HashSet<int> visible = new HashSet<int>();
        private readonly HashSet<int> dirty = new HashSet<int>();
        private readonly int[] versions = new int[60], bakedVersions = new int[60];
        private readonly CaveLocalRockGeometry geometry;
        private Task<PageBake[]> task;
        private bool disposed;
        private Exception fault;
        public int BuildCount { get; private set; }
        public bool Ready => fault == null && geometry.Initialized && task == null && visible.All(k => bakedVersions[k] == versions[k]);
        public Exception Fault => fault;

        public CaveLocalRockSurface(Material material, string seed, CaveTerrainStyle style)
        {
            cancellationToken = cancellation.Token;
            geometry = new CaveLocalRockGeometry(seed, style.CaptureOutline(), style.CaptureModifiers(), style.StoneSize, cancellationToken);
            var descriptor = new RenderTextureDescriptor(2560, 1536, GraphicsFormat.R8G8B8A8_SRGB, 0)
            { msaaSamples = 1, useMipMap = false, autoGenerateMips = false };
            texture = new RenderTexture(descriptor) { name = "Live cave rock surface", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.Create();
            var active = RenderTexture.active; RenderTexture.active = texture; GL.Clear(false, true, Color.clear); RenderTexture.active = active;
            upload = new Texture2D(256, 256, TextureFormat.RGBA32, false, false) { name = "Cave rock page upload" };
            material.SetTexture("_RockSurface", texture);
        }

        /// <summary>首次只读区块装载时创建岩壁输入快照；每页随后独立参与初次烘焙。</summary>
        public void Replace(Color32[] cells)
        {
            geometry.Replace(cells);
            for (int i = 0; i < versions.Length; i++) { versions[i] = checked(versions[i] + 1); dirty.Add(i); }
        }

        /// <summary>应用实际变化格并仅使其依赖半径命中的岩壁页失效。</summary>
        public void ApplyChanges(Color32[] cells, IReadOnlyList<int> changedIndexes)
        {
            if (!geometry.Initialized || changedIndexes == null || changedIndexes.Count == 0) return;
            geometry.ApplyCells(cells, changedIndexes); var affected = new HashSet<int>();
            foreach (int index in changedIndexes) geometry.AddAffectedPages(index, affected);
            foreach (int page in affected) { versions[page] = checked(versions[page] + 1); dirty.Add(page); }
        }

        public void SetVisible(GridBounds bounds)
        {
            var next = new HashSet<int>();
            if (!bounds.IsValid) { visible.Clear(); return; }
            for (int y = Math.Max(0, (1 - (int)bounds.MaxVExclusive) / 32); y <= Math.Min(5, -bounds.MinV / 32); y++)
                for (int x = Math.Max(0, bounds.MinU / 32); x <= Math.Min(9, ((int)bounds.MaxUExclusive - 1) / 32); x++) next.Add(y * 10 + x);
            foreach (int key in next)
                if (!visible.Contains(key) && bakedVersions[key] != versions[key]) dirty.Add(key);
            visible.Clear(); foreach (int key in next) visible.Add(key);
        }

        public void Tick()
        {
            if (disposed || fault != null || !geometry.Initialized) return;
            if (task != null)
            {
                if (!task.IsCompleted) return;
                var completed = task; task = null;
                PageBake[] pages;
                try { pages = completed.GetAwaiter().GetResult(); }
                catch (Exception error) { fault = error; throw; }
                bool stale = false;
                foreach (var page in pages)
                    if (visible.Contains(page.Key) && page.Version != versions[page.Key]) stale = true;
                if (stale)
                {
                    foreach (var page in pages)
                        if (visible.Contains(page.Key) && bakedVersions[page.Key] != versions[page.Key]) dirty.Add(page.Key);
                    return;
                }
                foreach (var page in pages)
                {
                    if (!visible.Contains(page.Key)) continue;
                    upload.LoadRawTextureData(page.Pixels); upload.Apply(false, false);
                    Graphics.CopyTexture(upload, 0, 0, 0, 0, 256, 256, texture, 0, 0, page.Key % 10 * 256, page.Key / 10 * 256);
                    bakedVersions[page.Key] = page.Version; dirty.Remove(page.Key); BuildCount++;
                }
                return;
            }
            var keys = dirty.Where(visible.Contains).Where(key => bakedVersions[key] != versions[key]).OrderBy(key => key).ToArray();
            if (keys.Length == 0) return;
            var bakes = new Func<byte[]>[keys.Length]; var capturedVersions = new int[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                capturedVersions[i] = versions[keys[i]]; bakes[i] = geometry.CapturePageBake(keys[i]); dirty.Remove(keys[i]);
            }
            task = Task.Run(() =>
            {
                var result = new PageBake[bakes.Length];
                for (int i = 0; i < bakes.Length; i++)
                { cancellationToken.ThrowIfCancellationRequested(); result[i] = new PageBake(keys[i], capturedVersions[i], bakes[i]()); }
                return result;
            }, cancellationToken);
        }

        public void Dispose()
        {
            if (disposed) return; disposed = true; cancellation.Cancel(); cancellation.Dispose();
            texture.Release(); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(upload);
            task = null; dirty.Clear(); visible.Clear();
        }
    }
}
