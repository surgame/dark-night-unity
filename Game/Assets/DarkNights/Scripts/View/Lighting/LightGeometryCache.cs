using System;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>单个地图表现绑定的后台距离缓存；只允许一个任务，版本不匹配的完成结果丢弃，Unity 上传留在主线程。</summary>
    public sealed class LightGeometryCache
    {
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Func<LightGeometrySnapshot,Func<bool>,float[]> calculate;
        public LightGeometryCache(Func<LightGeometrySnapshot,Func<bool>,float[]> calculator)
        { calculate = calculator ?? throw new ArgumentNullException(nameof(calculator)); }
        private Task<float[]> pending;
        private CaveVisualSource current, requestedSource;
        private RectInt requestedBounds, installedBounds;
        private long requestedRevision, installedRevision = -1;
        private bool retired;
        public Texture2D Texture { get; private set; }
        public RectInt Bounds => installedBounds;
        public bool Ready => Texture != null;

        public void Tick(CaveVisualSource source, RectInt bounds)
        {
            if (retired) return;
            current = source;
            if (pending != null && pending.IsCompleted)
            {
                try
                {
                    var pixels = pending.GetAwaiter().GetResult();
                    if (current == requestedSource && source.GeometryRevision == requestedRevision && bounds == requestedBounds)
                    {
                        int width = bounds.width * LightGeometrySnapshot.SamplesPerCell;
                        int height = bounds.height * LightGeometrySnapshot.SamplesPerCell;
                        if (Texture == null || Texture.width != width || Texture.height != height)
                        {
                            if (Texture != null) UnityEngine.Object.Destroy(Texture);
                            Texture = new Texture2D(width, height, TextureFormat.RFloat, false, true)
                            { name = "Local wall intrusion distance", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                        }
                        Texture.SetPixelData(pixels, 0); Texture.Apply(false, false);
                        installedBounds = bounds; installedRevision = requestedRevision;
                    }
                }
                catch (OperationCanceledException) { }
                finally { pending = null; }
            }
            if (pending != null || source == null || (Texture != null && current == requestedSource &&
                installedRevision == source.GeometryRevision && installedBounds == bounds)) return;
            var snapshot = source.CaptureLightGeometry(bounds);
            requestedBounds = bounds; requestedSource = source; requestedRevision = source.GeometryRevision;
            CancellationToken token = lifetime.Token;
            pending = Task.Run(() => calculate(snapshot, () => token.IsCancellationRequested), token);
        }

        public async Task RetireAsync()
        {
            if (retired) return;
            retired = true; current = null; lifetime.Cancel();
            try { if (pending != null) await pending; }
            catch (OperationCanceledException) { }
            finally
            {
                pending = null; lifetime.Dispose();
                if (Texture != null) UnityEngine.Object.Destroy(Texture);
                Texture = null;
            }
        }
    }
}
