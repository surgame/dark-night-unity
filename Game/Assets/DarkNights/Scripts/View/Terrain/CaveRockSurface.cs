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
    /// <summary>当前地形的可重建岩壁表现缓存；读取冻结格副本，局部烘焙并上传可见页。无地图写入权，纹理随预览释放。</summary>
    internal sealed class CaveRockSurface : IDisposable
    {
        private readonly RenderTexture texture;
        private readonly Texture2D upload;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly HashSet<int> visible = new HashSet<int>();
        private readonly int[] versions = new int[60], bakedVersions = new int[60];
        private readonly string seed;
        private readonly int stoneSize, dirtyCells;
        private readonly CaveOutlineSettings outline;
        private byte[] materials, shapes;
        private Task<byte[]> task;
        private int pendingKey, pendingVersion;
        private bool disposed;
        public int BuildCount { get; private set; }
        public bool Ready => materials != null && visible.All(k => bakedVersions[k] == versions[k]);
        public CaveRockSurface(Material material, string seed, CaveTerrainStyle style)
        {
            this.seed = seed; stoneSize = style.StoneSize; outline = style.CaptureOutline();
            dirtyCells = (CaveRockBaker.DistanceCap + outline.Reach + 3 + 7) / 8;
            var descriptor = new RenderTextureDescriptor(2560, 1536, GraphicsFormat.R8G8B8A8_SRGB, 0)
            { msaaSamples = 1, useMipMap = false, autoGenerateMips = false };
            texture = new RenderTexture(descriptor) { name = "Live cave rock surface", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.Create();
            var active = RenderTexture.active; RenderTexture.active = texture; GL.Clear(false, true, Color.clear); RenderTexture.active = active;
            upload = new Texture2D(256, 256, TextureFormat.RGBA32, false, false) { name = "Cave rock page upload" };
            material.SetTexture("_RockSurface", texture);
            for (int i = 0; i < versions.Length; i++) versions[i] = 1;
        }
        public void Replace(Color32[] cells)
        {
            var nextMaterials = new byte[cells.Length]; var nextShapes = new byte[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                nextMaterials[i] = cells[i].r; nextShapes[i] = cells[i].g;
                if (materials != null && (nextMaterials[i] != materials[i] || nextShapes[i] != shapes[i]))
                {
                    // 距离场和独立轮廓的采样边界相加；失效半径随轮廓参数变化，不重建背景。
                    int x = i % 320, y = i / 320;
                    for (int py = Math.Max(0, (y - dirtyCells) / 32); py <= Math.Min(5, (y + dirtyCells) / 32); py++)
                        for (int px = Math.Max(0, (x - dirtyCells) / 32); px <= Math.Min(9, (x + dirtyCells) / 32); px++) versions[py * 10 + px]++;
                }
            }
            materials = nextMaterials; shapes = nextShapes;
        }
        public void SetVisible(GridBounds bounds)
        {
            visible.Clear();
            for (int y = Math.Max(0, (1 - (int)bounds.MaxVExclusive) / 32); y <= Math.Min(5, -bounds.MinV / 32); y++)
                for (int x = Math.Max(0, bounds.MinU / 32); x <= Math.Min(9, ((int)bounds.MaxUExclusive - 1) / 32); x++) visible.Add(y * 10 + x);
        }
        public void Tick()
        {
            if (disposed || materials == null) return;
            if (task != null)
            {
                if (!task.IsCompleted) return;
                var pixels = task.GetAwaiter().GetResult(); task = null;
                if (pendingVersion != versions[pendingKey]) return;
                upload.LoadRawTextureData(pixels); upload.Apply(false, false);
                Graphics.CopyTexture(upload, 0, 0, 0, 0, 256, 256, texture, 0, 0, pendingKey % 10 * 256, pendingKey / 10 * 256);
                bakedVersions[pendingKey] = pendingVersion; BuildCount++; return;
            }
            foreach (int key in visible.OrderBy(k => k))
            {
                if (bakedVersions[key] == versions[key]) continue;
                pendingKey = key; pendingVersion = versions[key];
                var ownMaterials = materials; var ownShapes = shapes; var token = cancellation.Token;
                task = Task.Run(() => CaveRockBaker.Bake(Solid, 2560, 1536, seed, key % 10 * 256, key / 10 * 256,
                    256, 256, token.ThrowIfCancellationRequested, stoneSize, outline), token);
                break;
                bool Solid(int x, int y)
                {
                    int cell = y / 8 * 320 + x / 8;
                    return ownMaterials[cell] != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)ownShapes[cell],
                        (x % 8 + .5f) / 8, 1 - (y % 8 + .5f) / 8);
                }
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; cancellation.Cancel(); cancellation.Dispose();
            texture.Release(); UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(upload);
            task = null; materials = shapes = null; visible.Clear();
        }
    }
}
